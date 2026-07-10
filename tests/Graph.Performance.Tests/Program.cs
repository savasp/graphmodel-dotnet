// Copyright CVOYA LLC. Licensed under the Apache License, Version 2.0.
// See LICENSE in the project root for full license terms.

namespace Graph.Performance.Tests;

using BenchmarkDotNet.Configs;
using BenchmarkDotNet.Jobs;
using BenchmarkDotNet.Running;
using BenchmarkDotNet.Toolchains.InProcess.Emit;

public class Program
{
    /// <summary>
    /// The main entry point for the performance tests.
    /// </summary>
    /// <param name="args">Command line arguments.</param>
    public static void Main(string[] args)
    {
        // Configure for development mode with project references
        // Use only InProcess toolchain to avoid project reference issues
        var config = ManualConfig.Create(DefaultConfig.Instance)
            .AddJob(Job.Default.WithToolchain(InProcessEmitToolchain.Instance))
            .WithOptions(ConfigOptions.DisableOptimizationsValidator);

        // Run all benchmarks automatically (no interactive selection)
        if (args.Length == 0 || args.Contains("--all"))
        {
            Console.WriteLine("Running all benchmarks...");
            // Run all benchmark classes
            BenchmarkRunner.Run<CrudOperationsBenchmark>(config);
            BenchmarkRunner.Run<RelationshipBenchmark>(config);
        }
        else
        {
            // Allow specific benchmark selection via args for local development
            BenchmarkSwitcher.FromAssembly(typeof(Program).Assembly).Run(args, config);
        }
    }
}
