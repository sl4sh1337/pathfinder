using Pathfinder.Core.Algorithms;
using Pathfinder.Core.IO;

namespace Pathfinder.Core.Tests;

public class SampleFilesTests
{
    public static TheoryData<string> SampleFiles =>
        [.. Directory.GetFiles(SamplesDirectory(), "*.graph").Select(Path.GetFileName)!];

    [Theory]
    [MemberData(nameof(SampleFiles))]
    public void BundledSampleParsesAndRunsWithBellmanFord(string fileName)
    {
        var text = File.ReadAllText(Path.Combine(SamplesDirectory(), fileName));

        var result = GraphFileFormat.Parse(text);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        var graph = result.Graph!;
        Assert.False(graph.IsEmpty);
        var run = new BellmanFordAlgorithm().Run(graph, graph.Vertices.Keys.First());
        Assert.Equal(fileName == "negative-cycle.graph", run.Result.HasNegativeCycle);
    }

    private static string SamplesDirectory()
    {
        var directory = new DirectoryInfo(AppContext.BaseDirectory);
        while (directory is not null && !File.Exists(Path.Combine(directory.FullName, "Pathfinder.sln")))
        {
            directory = directory.Parent;
        }

        return Path.Combine(
            directory?.FullName ?? throw new DirectoryNotFoundException("Pathfinder.sln not found."),
            "src", "Pathfinder.App", "Samples");
    }
}
