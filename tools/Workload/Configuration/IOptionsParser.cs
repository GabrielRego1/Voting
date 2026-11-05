namespace Workload.Configuration;

/// <summary>
/// Parses the CLI options of contracts/workload-cli.md.
/// </summary>
internal interface IOptionsParser
{
    /// <summary>Usage text shown on invalid arguments.</summary>
    string Usage { get; }

    OptionsParseResult Parse(IReadOnlyList<string> args);
}
