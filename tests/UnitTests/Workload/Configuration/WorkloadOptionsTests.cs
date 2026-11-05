using Microsoft.Extensions.DependencyInjection;
using Workload;
using Workload.Configuration;

namespace UnitTests.Workload.Configuration;

public sealed class WorkloadOptionsTests
{
    [Fact]
    public void Parse_NoArguments_AppliesContractDefaults()
    {
        var result = new OptionsParser().Parse([]);

        Assert.Null(result.Error);
        var options = result.Options!;
        Assert.Equal(new Uri("http://localhost:5080"), options.BaseUrl);
        Assert.Null(options.PollId);
        Assert.Equal(["Ana", "Bruno", "Carla"], options.Participants);
        Assert.Equal(10000, options.Rate);
        Assert.Equal(35, options.DurationSeconds);
        Assert.Equal([50, 30, 20], options.Distribution);
        Assert.Equal(0.05, options.RetryFraction);
        Assert.Equal(256, options.Connections);
        Assert.Equal(15, options.ResultTimeoutSeconds);
        Assert.Equal(OutputFormat.Text, options.Output);
    }

    [Fact]
    public void Parse_AllOptions_ReadsEveryValue()
    {
        var pollId = Guid.NewGuid();

        var result = new OptionsParser().Parse([
            "--base-url", "http://api:9000", "--poll-id", pollId.ToString(), "--participants", "X,Y,Z",
            "--rate", "100", "--duration", "3", "--distribution", "60,40,0", "--retry-fraction", "0.1",
            "--connections", "8", "--result-timeout", "20", "--output", "json",
        ]);

        Assert.Null(result.Error);
        var options = result.Options!;
        Assert.Equal(new Uri("http://api:9000"), options.BaseUrl);
        Assert.Equal(pollId, options.PollId);
        Assert.Equal(["X", "Y", "Z"], options.Participants);
        Assert.Equal(100, options.Rate);
        Assert.Equal(3, options.DurationSeconds);
        Assert.Equal([60, 40, 0], options.Distribution);
        Assert.Equal(0.1, options.RetryFraction);
        Assert.Equal(8, options.Connections);
        Assert.Equal(20, options.ResultTimeoutSeconds);
        Assert.Equal(OutputFormat.Json, options.Output);
    }

    [Theory]
    [InlineData("--distribution", "50,30")]
    [InlineData("--distribution", "50,30,30")]
    [InlineData("--distribution", "50,60,-10")]
    [InlineData("--distribution", "a,b,c")]
    [InlineData("--retry-fraction", "1.5")]
    [InlineData("--retry-fraction", "-0.1")]
    [InlineData("--rate", "0")]
    [InlineData("--rate", "-5")]
    [InlineData("--duration", "0")]
    [InlineData("--connections", "0")]
    [InlineData("--result-timeout", "0")]
    [InlineData("--output", "xml")]
    [InlineData("--poll-id", "not-a-guid")]
    [InlineData("--base-url", "not a url")]
    [InlineData("--participants", "A,B")]
    [InlineData("--unknown", "1")]
    public void Parse_InvalidValue_ReturnsError(string option, string value)
    {
        var result = new OptionsParser().Parse([option, value]);

        Assert.Null(result.Options);
        Assert.False(string.IsNullOrWhiteSpace(result.Error));
    }

    [Fact]
    public void Parse_OptionWithoutValue_ReturnsError()
    {
        var result = new OptionsParser().Parse(["--rate"]);

        Assert.NotNull(result.Error);
    }

    [Fact]
    public async Task RunAsync_UsageError_ExitsWithCode2()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        await using var services = new ServiceCollection().AddWorkload().BuildServiceProvider();

        var exitCode = await services.GetRequiredService<IWorkloadApp>()
            .RunAsync(["--rate", "0"], output, error, cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, exitCode);
        Assert.Contains("--rate", error.ToString(), StringComparison.Ordinal);
    }

    [Fact]
    public async Task RunAsync_ApiUnreachable_ExitsWithCode2()
    {
        using var output = new StringWriter();
        using var error = new StringWriter();

        await using var services = new ServiceCollection().AddWorkload().BuildServiceProvider();

        var exitCode = await services.GetRequiredService<IWorkloadApp>().RunAsync(
            ["--base-url", "http://127.0.0.1:1", "--duration", "1", "--rate", "1"],
            output,
            error,
            cancellationToken: TestContext.Current.CancellationToken);

        Assert.Equal(2, exitCode);
    }
}
