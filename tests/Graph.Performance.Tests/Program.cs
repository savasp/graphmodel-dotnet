// Copyright 2025 Savas Parastatidis
//
// Licensed under the Apache License, Version 2.0 (the "License");
// you may not use this file except in compliance with the License.
// You may obtain a copy of the License at
//
//      https://www.apache.org/licenses/LICENSE-2.0
//
// Unless required by applicable law or agreed to in writing, software
// distributed under the License is distributed on an "AS IS" BASIS,
// WITHOUT WARRANTIES OR CONDITIONS OF ANY KIND, either express or implied.
// See the License for the specific language governing permissions and
// limitations under the License.

namespace Graph.Model.Performance.Tests;

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
