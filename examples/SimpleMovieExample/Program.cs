namespace SimpleMovieExample;

using Cvoya.Graph.Model;
using Cvoya.Graph.Model.Neo4j;
using Microsoft.Extensions.Hosting;
using Neo4j.Driver;
using RazorConsole.Core;
using SimpleMovieExample.Components;

static partial class Program
{
    static string databaseName = "SimpleMovieExample";

    async static Task Main()
    {
        var hostBuilder = Host.CreateDefaultBuilder()
            .UseRazorConsole<App>();
        hostBuilder.Build().Run();
    }
}