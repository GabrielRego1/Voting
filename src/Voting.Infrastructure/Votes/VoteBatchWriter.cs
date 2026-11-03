using System.Diagnostics;
using System.Threading.Channels;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Npgsql;
using Voting.Application.Abstractions;
using Voting.Application.Votes;
using Voting.Domain.Votes;

namespace Voting.Infrastructure.Votes;

/// <summary>
/// Group-commit vote writer: votes are queued in a bounded channel and committed in batches, one
/// transaction per batch. A vote is confirmed only after its batch commits (FR-008).
/// Batching waits use real time, never the business <see cref="TimeProvider"/> (research R-04).
/// </summary>
public sealed partial class VoteBatchWriter(
    NpgsqlDataSource dataSource,
    IOptions<VoteWriterOptions> options,
    ILogger<VoteBatchWriter> logger) : BackgroundService, IVoteWriter
{
    private const string InsertSql = """
        INSERT INTO votes (id, poll_id, participant_id, idempotency_key, received_at)
        SELECT * FROM unnest($1::uuid[], $2::uuid[], $3::uuid[], $4::text[], $5::timestamptz[])
        ON CONFLICT (poll_id, idempotency_key) DO NOTHING
        RETURNING id
        """;

    private const string SelectExistingSql = """
        SELECT v.id, v.poll_id, v.participant_id, v.idempotency_key, v.received_at
        FROM votes v
        JOIN unnest($1::uuid[], $2::text[]) AS k (poll_id, idempotency_key)
          ON v.poll_id = k.poll_id AND v.idempotency_key = k.idempotency_key
        """;

    private readonly VoteWriterOptions _options = options.Value;

    private readonly Channel<PendingVote> _channel = Channel.CreateBounded<PendingVote>(
        new BoundedChannelOptions(options.Value.Capacity)
        {
            SingleReader = true,
            FullMode = BoundedChannelFullMode.Wait,
        });

    public Task<VoteWriteOutcome> WriteAsync(Vote vote, CancellationToken cancellationToken = default)
    {
        ArgumentNullException.ThrowIfNull(vote);
        var pending = new PendingVote(vote);
        if (!_channel.Writer.TryWrite(pending))
        {
            return Task.FromResult(VoteWriteOutcome.Overloaded);
        }

        // Cancelling only stops waiting: the vote may still be committed, and a retry with the same key replays it.
        return pending.Completion.Task.WaitAsync(cancellationToken);
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        // Stop accepting votes and let ExecuteAsync drain what is already queued.
        _channel.Writer.TryComplete();
        await base.StopAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var reader = _channel.Reader;
        var batch = new List<PendingVote>(_options.MaxBatchSize);

        // Not tied to stoppingToken: the loop ends when StopAsync completes the channel and the queue is drained.
        while (await reader.WaitToReadAsync(CancellationToken.None))
        {
            await CollectBatchAsync(reader, batch);
            await WriteBatchAsync(batch);
            batch.Clear();
        }
    }

    private async Task CollectBatchAsync(ChannelReader<PendingVote> reader, List<PendingVote> batch)
    {
        Drain(reader, batch);
        if (batch.Count >= _options.MaxBatchSize || batch.Count == 0)
        {
            return;
        }

        var remaining = _options.MaxBatchDelay - Stopwatch.GetElapsedTime(batch[0].EnqueuedAt);
        if (remaining > TimeSpan.Zero)
        {
            await Task.Delay(remaining, TimeProvider.System);
            Drain(reader, batch);
        }
    }

    private void Drain(ChannelReader<PendingVote> reader, List<PendingVote> batch)
    {
        while (batch.Count < _options.MaxBatchSize && reader.TryRead(out var pending))
        {
            batch.Add(pending);
        }
    }

    private async Task WriteBatchAsync(List<PendingVote> batch)
    {
        if (batch.Count == 0)
        {
            return;
        }

        try
        {
            var outcomes = await CommitAsync(batch);
            for (var i = 0; i < batch.Count; i++)
            {
                batch[i].Completion.TrySetResult(outcomes[i]);
            }
        }
#pragma warning disable CA1031 // Any failure must fail every vote of the batch so none is confirmed.
        catch (Exception ex)
#pragma warning restore CA1031
        {
            LogBatchFailed(logger, ex, batch.Count);
            foreach (var pending in batch)
            {
                pending.Completion.TrySetException(ex);
            }
        }
    }

    private async Task<VoteWriteOutcome[]> CommitAsync(List<PendingVote> batch)
    {
        var count = batch.Count;
        var ids = new Guid[count];
        var pollIds = new Guid[count];
        var participantIds = new Guid[count];
        var keys = new string[count];
        var receivedAts = new DateTimeOffset[count];
        for (var i = 0; i < count; i++)
        {
            var vote = batch[i].Vote;
            ids[i] = vote.Id;
            pollIds[i] = vote.PollId;
            participantIds[i] = vote.ParticipantId;
            keys[i] = vote.IdempotencyKey;
            receivedAts[i] = vote.ReceivedAt;
        }

        await using var connection = await dataSource.OpenConnectionAsync();
        await using var transaction = await connection.BeginTransactionAsync();

        var inserted = new HashSet<Guid>();
        await using (var insert = new NpgsqlCommand(InsertSql, connection, transaction))
        {
            insert.Parameters.AddWithValue(ids);
            insert.Parameters.AddWithValue(pollIds);
            insert.Parameters.AddWithValue(participantIds);
            insert.Parameters.AddWithValue(keys);
            insert.Parameters.AddWithValue(receivedAts);
            await using var reader = await insert.ExecuteReaderAsync();
            while (await reader.ReadAsync())
            {
                inserted.Add(reader.GetGuid(0));
            }
        }

        Dictionary<(Guid PollId, string Key), VoteReceipt>? existing = null;
        if (inserted.Count < count)
        {
            existing = await ReadExistingAsync(connection, transaction, batch.Where(p => !inserted.Contains(p.Vote.Id)));
        }

        await transaction.CommitAsync();

        var outcomes = new VoteWriteOutcome[count];
        for (var i = 0; i < count; i++)
        {
            var vote = batch[i].Vote;
            if (inserted.Contains(vote.Id))
            {
                outcomes[i] = VoteWriteOutcome.Accepted(VoteReceipt.From(vote));
                continue;
            }

            var original = existing![(vote.PollId, vote.IdempotencyKey)];
            outcomes[i] = original.ParticipantId == vote.ParticipantId
                ? VoteWriteOutcome.Replayed(original)
                : VoteWriteOutcome.Conflict;
        }

        return outcomes;
    }

    private static async Task<Dictionary<(Guid PollId, string Key), VoteReceipt>> ReadExistingAsync(
        NpgsqlConnection connection,
        NpgsqlTransaction transaction,
        IEnumerable<PendingVote> notInserted)
    {
        var votes = notInserted.Select(p => p.Vote).ToArray();
        await using var select = new NpgsqlCommand(SelectExistingSql, connection, transaction);
        select.Parameters.AddWithValue(votes.Select(v => v.PollId).ToArray());
        select.Parameters.AddWithValue(votes.Select(v => v.IdempotencyKey).ToArray());

        var existing = new Dictionary<(Guid PollId, string Key), VoteReceipt>();
        await using var reader = await select.ExecuteReaderAsync();
        while (await reader.ReadAsync())
        {
            var receipt = new VoteReceipt(
                reader.GetGuid(0),
                reader.GetGuid(1),
                reader.GetGuid(2),
                reader.GetFieldValue<DateTimeOffset>(4));
            existing[(receipt.PollId, reader.GetString(3))] = receipt;
        }

        return existing;
    }

    [LoggerMessage(Level = LogLevel.Error, Message = "Failed to commit a batch of {Count} votes; none was confirmed.")]
    private static partial void LogBatchFailed(ILogger logger, Exception exception, int count);

    private sealed class PendingVote(Vote vote)
    {
        public Vote Vote { get; } = vote;

        public long EnqueuedAt { get; } = Stopwatch.GetTimestamp();

        public TaskCompletionSource<VoteWriteOutcome> Completion { get; } =
            new(TaskCreationOptions.RunContinuationsAsynchronously);
    }
}
