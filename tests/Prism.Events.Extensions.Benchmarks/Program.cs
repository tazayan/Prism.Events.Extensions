using BenchmarkDotNet.Running;

namespace Prism.Events.Extensions.Benchmarks;

internal static class Program
{
    private const string CpuUsageSwitch = "--cpu-usage";

    /// <summary>
    /// Runs the benchmarks selected on the command line, for example <c>--filter *Publish*</c>.
    /// </summary>
    /// <remarks>
    /// Pass <c>--cpu-usage</c> on Windows to also collect a Visual Studio CPU usage trace for each
    /// benchmark. Every other argument is passed to BenchmarkDotNet.
    /// </remarks>
    private static void Main(string[] args)
    {
        var collectCpuUsage = args.Contains(CpuUsageSwitch, StringComparer.OrdinalIgnoreCase);
        var benchmarkArguments = args.Where(argument => !string.Equals(argument, CpuUsageSwitch, StringComparison.OrdinalIgnoreCase)).ToArray();

        BenchmarkSwitcher
            .FromAssembly(typeof(Program).Assembly)
            .Run(benchmarkArguments, BenchmarkConfig.Create(collectCpuUsage));
    }
}
