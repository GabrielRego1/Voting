namespace Workload;

internal enum OutputFormat
{
    Text,
    Json,
}

/// <summary>
/// Options of the load generator, as defined in contracts/workload-cli.md.
/// </summary>
internal sealed record WorkloadOptions
{
    public Uri BaseUrl { get; init; } = new("http://localhost:5080");

    public Guid? PollId { get; init; }

    public IReadOnlyList<string> Participants { get; init; } = ["Ana", "Bruno", "Carla"];

    public int Rate { get; init; } = 10000;

    public int DurationSeconds { get; init; } = 35;

    public IReadOnlyList<int> Distribution { get; init; } = [50, 30, 20];

    public double RetryFraction { get; init; } = 0.05;

    public int Connections { get; init; } = 256;

    public int ResultTimeoutSeconds { get; init; } = 15;

    public OutputFormat Output { get; init; } = OutputFormat.Text;
}
