using Workload.Configuration;

namespace Workload.Reporting;

/// <summary>
/// Writes the run report in the requested format.
/// </summary>
internal interface IReportWriter
{
    void Write(Report report, OutputFormat format, TextWriter writer);
}
