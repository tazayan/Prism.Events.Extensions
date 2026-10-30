using BenchmarkDotNet.Columns;
using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Diagnosers;
using BenchmarkDotNet.Reports;
using Microsoft.VSDiagnostics;

namespace Prism.Events.Extensions.Benchmarks;

/// <summary>
/// The configuration shared by every benchmark in this project.
/// </summary>
internal static class BenchmarkConfig
{
    /// <summary>
    /// Creates the benchmark configuration.
    /// </summary>
    /// <param name="collectCpuUsage">
    /// <see langword="true"/> to add the Visual Studio CPU usage diagnoser. It is supported only on
    /// Windows and is ignored elsewhere.
    /// </param>
    public static IConfig Create(bool collectCpuUsage)
    {
        var config = DefaultConfig.Instance
            // Each benchmark class compares the Prism type (the baseline) with its replacements, once
            // per combination of parameters.
            .AddLogicalGroupRules(BenchmarkLogicalGroupRule.ByParams)
            .AddDiagnoser(MemoryDiagnoser.Default)
            .AddDiagnoser(ThreadingDiagnoser.Default)
            .WithSummaryStyle(SummaryStyle.Default.WithRatioStyle(RatioStyle.Trend))
            .WithOrderer(new BenchmarkDotNet.Order.DefaultOrderer(BenchmarkDotNet.Order.SummaryOrderPolicy.Declared));

        if (collectCpuUsage && OperatingSystem.IsWindows())
        {
            config = config.AddDiagnoser(new CPUUsageDiagnoserAttribute().Config.GetDiagnosers().ToArray());
        }

        return config;
    }
}
