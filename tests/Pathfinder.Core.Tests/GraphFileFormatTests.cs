using Pathfinder.Core.IO;
using Pathfinder.Core.Model;
using static Pathfinder.Core.Tests.TestGraphs;

namespace Pathfinder.Core.Tests;

public class GraphFileFormatTests
{
    [Fact]
    public void ParsesEdgeListWithCommentsIsolatedVerticesAndPositions()
    {
        const string text = """
            # sample graph
            A B 4      # trailing comment
            B C -1.5
            D
            @ A 10 20

            """;

        var result = GraphFileFormat.Parse(text);

        Assert.True(result.IsSuccess, string.Join("; ", result.Errors));
        var graph = result.Graph!;
        Assert.Equal(["A", "B", "C", "D"], graph.Vertices.Values.Select(v => v.Name));
        Assert.Equal(2, graph.Edges.Count);
        Assert.Equal(-1.5, graph.FindEdge(Id(graph, "B"), Id(graph, "C"))!.Weight);
        Assert.Equal((10.0, 20.0), (graph.FindVertex("A")!.X, graph.FindVertex("A")!.Y));
    }

    [Fact]
    public void VerticesWithoutPositionsGetDistinctLayoutPositions()
    {
        var graph = GraphFileFormat.Parse("A B 1\nB C 1\nC A 1").Graph!;

        var positions = graph.Vertices.Values.Select(v => (v.X, v.Y)).ToList();

        Assert.Equal(3, positions.Distinct().Count());
    }

    [Fact]
    public void RoundTripPreservesGraph()
    {
        var original = Build(("A", "B", 0.1), ("B", "A", -3), ("B", "Узел_1", 1e-7));
        original = original.AddVertex("Isolated", 12.3456, -6.789).Graph;

        var restored = GraphFileFormat.Parse(GraphFileFormat.Serialize(original)).Graph!;

        Assert.Equal(original.Vertices.Values.Select(v => v.Name), restored.Vertices.Values.Select(v => v.Name));
        Assert.Equal(12.35, restored.FindVertex("Isolated")!.X);
        foreach (var edge in original.Edges.Values)
        {
            var from = original.GetVertex(edge.From).Name;
            var to = original.GetVertex(edge.To).Name;
            var copy = restored.FindEdge(Id(restored, from), Id(restored, to));
            Assert.Equal(edge.Weight, copy!.Weight);
        }
    }

    [Fact]
    public void EmptyFileGivesEmptyGraph()
    {
        var result = GraphFileFormat.Parse("# nothing here\n\n");

        Assert.True(result.IsSuccess);
        Assert.True(result.Graph!.IsEmpty);
    }

    [Theory]
    [InlineData("A B", 1)]
    [InlineData("A B C D", 1)]
    [InlineData("A B x", 1)]
    [InlineData("A B NaN", 1)]
    [InlineData("A B 1e12", 1)]
    [InlineData("A A 1", 1)]
    [InlineData("A B 1\nA B 2", 2)]
    [InlineData("@ A 1", 1)]
    [InlineData("@ A 1 y", 1)]
    [InlineData("@ A 1 2\n@ A 3 4", 2)]
    [InlineData("A\nB\nA@ B 1", 3)]
    public void ReportsInvalidLinesWithLineNumbers(string text, int expectedLine)
    {
        var result = GraphFileFormat.Parse(text);

        Assert.False(result.IsSuccess);
        Assert.Null(result.Graph);
        Assert.Contains(result.Errors, e => e.Line == expectedLine);
        Assert.StartsWith($"Строка {expectedLine}:", result.Errors.First(e => e.Line == expectedLine).ToString());
    }

    [Fact]
    public void InvalidNamesAreReportedBeforeLoopCheck()
    {
        var result = GraphFileFormat.Parse("A@ A@ 1");

        Assert.NotEmpty(result.Errors);
        Assert.All(result.Errors, e => Assert.Contains("Имя вершины", e.Message));
    }

    [Fact]
    public void StopsCollectingErrorsAfterLimit()
    {
        var text = string.Join('\n', Enumerable.Repeat("bad line with many tokens", 100));

        var result = GraphFileFormat.Parse(text);

        Assert.Equal(GraphFileFormat.MaxReportedErrors, result.Errors.Length);
    }

    [Fact]
    public void RejectsGraphsAboveVertexLimit()
    {
        var text = string.Join('\n', Enumerable.Range(0, GraphRules.MaxVertexCount + 1).Select(i => $"v{i}"));

        var result = GraphFileFormat.Parse(text);

        Assert.False(result.IsSuccess);
        Assert.Contains(result.Errors, e => e.Line == 0);
    }

    [Fact]
    public void HandlesWindowsLineEndings()
    {
        var result = GraphFileFormat.Parse("A B 1\r\nB C 2\r\n");

        Assert.True(result.IsSuccess);
        Assert.Equal(2, result.Graph!.Edges.Count);
    }
}
