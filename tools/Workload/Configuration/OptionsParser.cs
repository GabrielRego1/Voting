using System.Globalization;

namespace Workload.Configuration;

/// <summary>
/// Hand-written parser for the CLI options of contracts/workload-cli.md (no extra dependency).
/// </summary>
internal static class OptionsParser
{
    public const string Usage = """
        Usage: dotnet run --project tools/Workload -- [options]
          --base-url <url>          API address (default http://localhost:5080)
          --poll-id <guid>          existing poll; when omitted a poll is created and opened
          --participants <a,b,c>    names used when creating the poll (default Ana,Bruno,Carla)
          --rate <n>                votes per second (default 10000)
          --duration <s>            sending duration in seconds (default 35)
          --distribution <p1,p2,p3> percentage per participant, summing 100 (default 50,30,20)
          --retry-fraction <0..1>   fraction of accepted votes resent with the same key (default 0.05)
          --connections <n>         maximum concurrent HTTP connections (default 256)
          --result-timeout <s>      maximum wait for GET /result (default 15)
          --output <text|json>      report format (default text)
        """;

    public static OptionsParseResult Parse(IReadOnlyList<string> args)
    {
        ArgumentNullException.ThrowIfNull(args);
        var options = new WorkloadOptions();
        for (var i = 0; i < args.Count; i++)
        {
            var name = args[i];
            if (i + 1 >= args.Count)
            {
                return Fail($"Option '{name}' requires a value.");
            }

            var value = args[++i];
            var (updated, error) = Apply(options, name, value);
            if (error is not null)
            {
                return Fail(error);
            }

            options = updated!;
        }

        return new OptionsParseResult(options, null);
    }

    private static (WorkloadOptions? Options, string? Error) Apply(WorkloadOptions options, string name, string value)
    {
        switch (name)
        {
            case "--base-url":
                return Uri.TryCreate(value, UriKind.Absolute, out var url) && url.Scheme is "http" or "https"
                    ? (options with { BaseUrl = url }, null)
                    : (null, "--base-url must be an absolute http(s) URL.");
            case "--poll-id":
                return Guid.TryParse(value, out var pollId)
                    ? (options with { PollId = pollId }, null)
                    : (null, "--poll-id must be a GUID.");
            case "--participants":
                var names = value.Split(',', StringSplitOptions.TrimEntries);
                return names.Length == 3 && names.All(n => n.Length > 0)
                    ? (options with { Participants = names }, null)
                    : (null, "--participants must have exactly 3 non-empty names separated by commas.");
            case "--rate":
                return Positive(value, "--rate", v => options with { Rate = v });
            case "--duration":
                return Positive(value, "--duration", v => options with { DurationSeconds = v });
            case "--connections":
                return Positive(value, "--connections", v => options with { Connections = v });
            case "--result-timeout":
                return Positive(value, "--result-timeout", v => options with { ResultTimeoutSeconds = v });
            case "--distribution":
                var parts = value.Split(',', StringSplitOptions.TrimEntries);
                var weights = new int[parts.Length];
                var valid = parts.Length == 3
                    && parts.Select((p, i) => int.TryParse(p, NumberStyles.Integer, CultureInfo.InvariantCulture, out weights[i]) && weights[i] >= 0).All(ok => ok)
                    && weights.Sum() == 100;
                return valid
                    ? (options with { Distribution = weights }, null)
                    : (null, "--distribution must have 3 non-negative integers summing 100.");
            case "--retry-fraction":
                return double.TryParse(value, NumberStyles.Float, CultureInfo.InvariantCulture, out var fraction) && fraction is >= 0 and <= 1
                    ? (options with { RetryFraction = fraction }, null)
                    : (null, "--retry-fraction must be a number between 0 and 1.");
            case "--output":
                return value switch
                {
                    "text" => (options with { Output = OutputFormat.Text }, null),
                    "json" => (options with { Output = OutputFormat.Json }, null),
                    _ => (null, "--output must be 'text' or 'json'."),
                };
            default:
                return (null, $"Unknown option '{name}'.");
        }
    }

    private static (WorkloadOptions? Options, string? Error) Positive(string value, string name, Func<int, WorkloadOptions> apply)
    {
        return int.TryParse(value, NumberStyles.Integer, CultureInfo.InvariantCulture, out var parsed) && parsed > 0
            ? (apply(parsed), null)
            : (null, $"{name} must be an integer greater than 0.");
    }

    private static OptionsParseResult Fail(string error)
    {
        return new OptionsParseResult(null, error);
    }
}
