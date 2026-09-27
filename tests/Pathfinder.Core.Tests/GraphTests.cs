using Pathfinder.Core.Model;
using static Pathfinder.Core.Tests.TestGraphs;

namespace Pathfinder.Core.Tests;

public class GraphTests
{
    [Fact]
    public void ModificationsReturnNewInstancesAndKeepOriginalIntact()
    {
        var (original, a) = Graph.Empty.AddVertex("A", 1, 2);

        var renamed = original.RenameVertex(a.Id, "B");
        var moved = original.MoveVertex(a.Id, 5, 6);

        Assert.Equal("A", original.GetVertex(a.Id).Name);
        Assert.Equal("B", renamed.GetVertex(a.Id).Name);
        Assert.Equal((5.0, 6.0), (moved.GetVertex(a.Id).X, moved.GetVertex(a.Id).Y));
        Assert.Equal((1.0, 2.0), (original.GetVertex(a.Id).X, original.GetVertex(a.Id).Y));
    }

    [Fact]
    public void RemovingVertexRemovesIncidentEdges()
    {
        var graph = Build(("A", "B", 1), ("B", "C", 1), ("A", "C", 1));

        var result = graph.RemoveVertex(Id(graph, "B"));

        Assert.Equal(2, result.Vertices.Count);
        var remaining = Assert.Single(result.Edges.Values);
        Assert.Equal((Id(graph, "A"), Id(graph, "C")), (remaining.From, remaining.To));
    }

    [Fact]
    public void IdsAreNotReusedAfterRemoval()
    {
        var (graph, a) = Graph.Empty.AddVertex("A", 0, 0);

        var (_, b) = graph.RemoveVertex(a.Id).AddVertex("B", 0, 0);

        Assert.NotEqual(a.Id, b.Id);
    }

    [Fact]
    public void EdgeWeightCanBeChangedAndEdgeRemoved()
    {
        var graph = Build(("A", "B", 1));
        var edge = graph.Edges.Values.Single();

        var reweighted = graph.SetEdgeWeight(edge.Id, -7.5);
        var removed = graph.RemoveEdge(edge.Id);

        Assert.Equal(-7.5, reweighted.GetEdge(edge.Id).Weight);
        Assert.True(reweighted.HasNegativeWeights);
        Assert.Empty(removed.Edges);
    }

    [Fact]
    public void FindEdgeAndReverseEdgeLookup()
    {
        var graph = Build(("A", "B", 1), ("B", "A", 2), ("B", "C", 3));
        var ab = graph.FindEdge(Id(graph, "A"), Id(graph, "B"))!;
        var bc = graph.FindEdge(Id(graph, "B"), Id(graph, "C"))!;

        Assert.Equal(1, ab.Weight);
        Assert.True(graph.HasReverseEdge(ab));
        Assert.False(graph.HasReverseEdge(bc));
        Assert.Null(graph.FindEdge(Id(graph, "C"), Id(graph, "B")));
    }

    [Theory]
    [InlineData("")]
    [InlineData("A B")]
    [InlineData("A#")]
    [InlineData("@A")]
    [InlineData("123456789012345678901234567890123")]
    public void InvalidVertexNamesAreRejected(string name)
    {
        Assert.NotNull(GraphRules.ValidateVertexName(Graph.Empty, name));
        Assert.Throws<GraphRuleException>(() => Graph.Empty.AddVertex(name, 0, 0));
    }

    [Fact]
    public void DuplicateNamesAreRejectedExceptForTheVertexItself()
    {
        var graph = Build(("A", "B", 1));

        Assert.NotNull(GraphRules.ValidateVertexName(graph, "B", Id(graph, "A")));
        Assert.Null(GraphRules.ValidateVertexName(graph, "A", Id(graph, "A")));
        Assert.Throws<GraphRuleException>(() => graph.RenameVertex(Id(graph, "A"), "B"));
    }

    [Fact]
    public void SelfLoopsAndParallelEdgesAreRejected()
    {
        var graph = Build(("A", "B", 1));
        var a = Id(graph, "A");
        var b = Id(graph, "B");

        Assert.Throws<GraphRuleException>(() => graph.AddEdge(a, a, 1));
        Assert.Throws<GraphRuleException>(() => graph.AddEdge(a, b, 2));
        Assert.Throws<GraphRuleException>(() => graph.AddEdge(a, 999, 2));
        Assert.Null(GraphRules.ValidateNewEdge(graph, b, a));
    }

    [Theory]
    [InlineData("5", 5)]
    [InlineData("-2,5", -2.5)]
    [InlineData(" 3.25 ", 3.25)]
    [InlineData("1e3", 1000)]
    public void WeightsAreParsedWithEitherDecimalSeparator(string text, double expected)
    {
        Assert.True(GraphRules.TryParseWeight(text, out var weight, out var error));
        Assert.Null(error);
        Assert.Equal(expected, weight);
    }

    [Theory]
    [InlineData("")]
    [InlineData("abc")]
    [InlineData("NaN")]
    [InlineData("Infinity")]
    [InlineData("1e10")]
    public void InvalidWeightsAreRejected(string text)
    {
        Assert.False(GraphRules.TryParseWeight(text, out _, out var error));
        Assert.NotNull(error);
    }

    [Fact]
    public void NonFinitePositionsAreRejected()
    {
        Assert.Throws<GraphRuleException>(() => Graph.Empty.AddVertex("A", double.NaN, 0));
    }

    [Fact]
    public void UnknownIdsThrow()
    {
        Assert.Throws<GraphRuleException>(() => Graph.Empty.GetVertex(1));
        Assert.Throws<GraphRuleException>(() => Graph.Empty.RemoveEdge(1));
    }
}
