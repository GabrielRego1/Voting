using System.Text.Json;

namespace Workload.Tests;

public sealed class ReportBuilderTests
{
    private static readonly RunParameters Parameters = new(100, 3, [50, 30, 20], 0.1, 8);

    [Fact]
    public void Build_CreatedResponses_CountDistinctAcceptedKeys()
    {
        var builder = new ReportBuilder();
        Send(builder, "k1", 201);
        Send(builder, "k2", 201);

        var report = builder.Build(Guid.NewGuid(), Parameters, TimeSpan.FromSeconds(1), Latency(10), ResultWithTotal(2));

        Assert.Equal(2, report.Counts.Sent);
        Assert.Equal(2, report.Counts.Accepted);
        Assert.Equal(0, report.Counts.Replayed);
    }

    [Fact]
    public void Build_OkForKeyNeverConfirmed_CountsAsAccepted()
    {
        var builder = new ReportBuilder();
        builder.RecordSent();
        builder.RecordResponse("lost-first-attempt", new VoteResponse(200, null), intentionalReplay: false);

        var report = builder.Build(Guid.NewGuid(), Parameters, TimeSpan.FromSeconds(1), Latency(10), ResultWithTotal(1));

        Assert.Equal(1, report.Counts.Accepted);
        Assert.Equal(0, report.Counts.Replayed);
    }

    [Fact]
    public void Build_IntentionalReplays_CountAsReplayedNotAccepted()
    {
        var builder = new ReportBuilder();
        Send(builder, "k1", 201);
        builder.RecordResponse("k1", new VoteResponse(200, null), intentionalReplay: true);
        builder.RecordResponse("k1", new VoteResponse(200, null), intentionalReplay: true);

        var report = builder.Build(Guid.NewGuid(), Parameters, TimeSpan.FromSeconds(1), Latency(10), ResultWithTotal(1));

        Assert.Equal(1, report.Counts.Accepted);
        Assert.Equal(2, report.Counts.Replayed);
        Assert.True(report.Checks.NoDuplicatesFromReplays);
    }

    [Fact]
    public void Build_ReplayAnsweredWithCreated_FailsNoDuplicatesCheck()
    {
        var builder = new ReportBuilder();
        Send(builder, "k1", 201);
        builder.RecordResponse("k1", new VoteResponse(201, null), intentionalReplay: true);

        var report = builder.Build(Guid.NewGuid(), Parameters, TimeSpan.FromSeconds(1), Latency(10), ResultWithTotal(1));

        Assert.False(report.Checks.NoDuplicatesFromReplays);
        Assert.Equal(1, report.ExitCode);
    }

    [Fact]
    public void Build_Rejections_AreGroupedByCode()
    {
        var builder = new ReportBuilder();
        Send(builder, "k1", 409, "voting_closed");
        Send(builder, "k2", 409, "voting_closed");
        Send(builder, "k3", 503, "overloaded");
        builder.RecordSent();
        builder.RecordResponse("k4", VoteResponse.TransportError, intentionalReplay: false);
        builder.RecordRetry();

        var report = builder.Build(Guid.NewGuid(), Parameters, TimeSpan.FromSeconds(1), Latency(10), ResultWithTotal(0));

        Assert.Equal(2, report.Counts.Rejected["voting_closed"]);
        Assert.Equal(1, report.Counts.Rejected["overloaded"]);
        Assert.Equal(1, report.Counts.TransportErrors);
        Assert.Equal(1, report.Counts.Retried);
        Assert.Equal(4, report.Counts.Sent);
    }

    [Fact]
    public void Build_AllChecksPass_ExitCode0()
    {
        var builder = new ReportBuilder();
        Send(builder, "k1", 201);

        var report = builder.Build(Guid.NewGuid(), Parameters, TimeSpan.FromSeconds(2), Latency(200), ResultWithTotal(1));

        Assert.True(report.Checks.AcceptedEqualsTotalVotes);
        Assert.True(report.Checks.NoDuplicatesFromReplays);
        Assert.True(report.Checks.P95WithinTarget);
        Assert.Equal(0, report.ExitCode);
        Assert.Equal(0.5, report.AchievedRate);
    }

    [Fact]
    public void Build_AcceptedDiffersFromTotalVotes_ExitCode1()
    {
        var builder = new ReportBuilder();
        Send(builder, "k1", 201);

        var report = builder.Build(Guid.NewGuid(), Parameters, TimeSpan.FromSeconds(1), Latency(10), ResultWithTotal(2));

        Assert.False(report.Checks.AcceptedEqualsTotalVotes);
        Assert.Equal(1, report.ExitCode);
    }

    [Fact]
    public void Build_NoResult_AcceptedCheckFails()
    {
        var builder = new ReportBuilder();
        Send(builder, "k1", 201);

        var report = builder.Build(Guid.NewGuid(), Parameters, TimeSpan.FromSeconds(1), Latency(10), result: null);

        Assert.False(report.Checks.AcceptedEqualsTotalVotes);
        Assert.Equal(1, report.ExitCode);
    }

    [Fact]
    public void Build_P95Above200Ms_P95CheckFails()
    {
        var builder = new ReportBuilder();
        Send(builder, "k1", 201);

        var report = builder.Build(Guid.NewGuid(), Parameters, TimeSpan.FromSeconds(1), Latency(200.1), ResultWithTotal(1));

        Assert.False(report.Checks.P95WithinTarget);
        Assert.Equal(1, report.ExitCode);
    }

    [Fact]
    public void Write_Json_UsesContractFieldNames()
    {
        var builder = new ReportBuilder();
        Send(builder, "k1", 201);
        Send(builder, "k2", 409, "voting_closed");
        var report = builder.Build(Guid.NewGuid(), Parameters, TimeSpan.FromSeconds(1), Latency(10), ResultWithTotal(1));
        using var writer = new StringWriter();

        ReportWriter.Write(report, OutputFormat.Json, writer);

        using var json = JsonDocument.Parse(writer.ToString());
        var root = json.RootElement;
        Assert.Equal(report.PollId, root.GetProperty("pollId").GetGuid());
        Assert.Equal(100, root.GetProperty("parameters").GetProperty("rate").GetInt32());
        Assert.Equal(3, root.GetProperty("parameters").GetProperty("durationSeconds").GetInt32());
        Assert.Equal(3, root.GetProperty("parameters").GetProperty("distribution").GetArrayLength());
        Assert.Equal(0.1, root.GetProperty("parameters").GetProperty("retryFraction").GetDouble());
        Assert.Equal(8, root.GetProperty("parameters").GetProperty("connections").GetInt32());
        var counts = root.GetProperty("counts");
        Assert.Equal(2, counts.GetProperty("sent").GetInt64());
        Assert.Equal(1, counts.GetProperty("accepted").GetInt64());
        Assert.Equal(0, counts.GetProperty("replayed").GetInt64());
        Assert.Equal(0, counts.GetProperty("retried").GetInt64());
        Assert.Equal(1, counts.GetProperty("rejected").GetProperty("voting_closed").GetInt64());
        Assert.Equal(0, counts.GetProperty("transportErrors").GetInt64());
        Assert.Equal(2, root.GetProperty("achievedRate").GetDouble());
        Assert.Equal(10, root.GetProperty("latencyMs").GetProperty("p95").GetDouble());
        Assert.Equal(1, root.GetProperty("result").GetProperty("totalVotes").GetInt64());
        Assert.True(root.GetProperty("checks").GetProperty("acceptedEqualsTotalVotes").GetBoolean());
        Assert.True(root.GetProperty("checks").GetProperty("noDuplicatesFromReplays").GetBoolean());
        Assert.True(root.GetProperty("checks").GetProperty("p95WithinTarget").GetBoolean());
        Assert.Equal(3, root.GetProperty("checks").EnumerateObject().Count());
        Assert.False(root.TryGetProperty("exitCode", out _));
    }

    [Fact]
    public void Write_Text_ContainsTheSameFields()
    {
        var builder = new ReportBuilder();
        Send(builder, "k1", 201);
        var report = builder.Build(Guid.NewGuid(), Parameters, TimeSpan.FromSeconds(1), Latency(10), ResultWithTotal(1));
        using var writer = new StringWriter();

        ReportWriter.Write(report, OutputFormat.Text, writer);

        var text = writer.ToString();
        foreach (var field in new[] { "pollId", "sent", "accepted", "replayed", "retried", "transportErrors", "achievedRate", "p95", "acceptedEqualsTotalVotes", "noDuplicatesFromReplays", "p95WithinTarget" })
        {
            Assert.Contains(field, text, StringComparison.Ordinal);
        }
    }

    [Fact]
    public void Snapshot_MixedResponses_ReflectsCountersAndInFlight()
    {
        var builder = new ReportBuilder();
        Send(builder, "k1", 201);
        Send(builder, "k2", 409, "voting_closed");
        builder.RecordSent();
        builder.RecordResponse("k3", VoteResponse.TransportError, intentionalReplay: false);
        builder.RecordSent();
        builder.RecordResponse("k1", new VoteResponse(200, null), intentionalReplay: true);
        builder.RecordSent();

        var snapshot = builder.Snapshot();

        Assert.Equal(new ProgressSnapshot(Sent: 5, Accepted: 1, Rejected: 1, TransportErrors: 1, InFlight: 2), snapshot);
    }

    private static void Send(ReportBuilder builder, string key, int status, string? code = null)
    {
        builder.RecordSent();
        builder.RecordResponse(key, new VoteResponse(status, code), intentionalReplay: false);
    }

    private static LatencySummary Latency(double p95)
    {
        return new LatencySummary(1, p95, p95, p95);
    }

    private static JsonElement ResultWithTotal(long totalVotes)
    {
        return JsonDocument.Parse($$"""{"totalVotes": {{totalVotes}}, "outcome": "Eliminated"}""").RootElement.Clone();
    }
}
