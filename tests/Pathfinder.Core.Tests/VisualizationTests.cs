using Pathfinder.Core.Algorithms;
using Pathfinder.Core.Layout;
using Pathfinder.Core.Visualization;
using static Pathfinder.Core.Tests.TestGraphs;

namespace Pathfinder.Core.Tests;

public class VisualizationTests
{
    [Fact]
    public void PlaybackEndsInStateMatchingResult()
    {
        var graph = Build(("A", "B", 2), ("B", "C", 2), ("A", "C", 5));
        var run = new DijkstraAlgorithm().Run(graph, Id(graph, "A"));
        var playback = new AlgorithmPlayback(graph, run);

        var applied = playback.AdvanceToEnd();

        Assert.True(playback.IsFinished);
        Assert.Equal(run.Steps.Length, applied.Count);
        Assert.Null(playback.Advance());
        Assert.True(playback.State.IsCompleted);
        Assert.Equal(run.Result.Distances, playback.State.Distances);
        Assert.Equal(4, playback.State.Distances[Id(graph, "C")]);
    }

    [Fact]
    public void EdgeCheckHighlightsEdgeAndUpdatesDistanceOnlyWhenImproved()
    {
        var graph = Build(("A", "B", 3));
        var edge = graph.Edges.Values.Single();
        var state = VisualStateReducer.Apply(VisualState.Empty, graph, new InitializationStep(Id(graph, "A")));

        var improved = VisualStateReducer.Apply(
            state, graph, new EdgeCheckedStep(edge.Id, edge.From, edge.To, double.PositiveInfinity, 3, true));
        var rejected = VisualStateReducer.Apply(
            improved, graph, new EdgeCheckedStep(edge.Id, edge.From, edge.To, 3, 7, false));

        Assert.Equal(edge.Id, improved.ActiveEdgeId);
        Assert.True(improved.ActiveEdgeImproved);
        Assert.Equal(3, improved.Distances[edge.To]);
        Assert.True(improved.IsTreeEdge(edge.Id));
        Assert.False(rejected.ActiveEdgeImproved);
        Assert.Equal(3, rejected.Distances[edge.To]);
    }

    [Fact]
    public void InitializationSetsSourceToZeroAndOthersToInfinity()
    {
        var graph = Build(("A", "B", 1));

        var state = VisualStateReducer.Apply(VisualState.Empty, graph, new InitializationStep(Id(graph, "A")));

        Assert.Equal(Id(graph, "A"), state.SourceId);
        Assert.Equal(0, state.Distances[Id(graph, "A")]);
        Assert.Equal(double.PositiveInfinity, state.Distances[Id(graph, "B")]);
    }

    [Fact]
    public void NegativeCycleMarksAffectedVertices()
    {
        var graph = Build(("A", "B", 1), ("B", "C", -3), ("C", "B", 1));

        var state = VisualStateReducer.ApplyAll(graph, new BellmanFordAlgorithm().Run(graph, Id(graph, "A")).Steps);

        Assert.Contains(Id(graph, "B"), state.NegativeCycleVertices);
        Assert.Contains(Id(graph, "C"), state.NegativeCycleVertices);
        Assert.Equal(double.NegativeInfinity, state.Distances[Id(graph, "C")]);
    }

    [Fact]
    public void WithPathToHighlightsShortestPathAfterCompletion()
    {
        var graph = Build(("A", "B", 1), ("B", "C", 1), ("A", "C", 5));
        var state = VisualStateReducer.ApplyAll(graph, new DijkstraAlgorithm().Run(graph, Id(graph, "A")).Steps);

        var highlighted = state.WithPathTo(Id(graph, "C"));
        var cleared = highlighted.WithPathTo(null);

        Assert.Equal(2, highlighted.PathEdges.Count);
        Assert.Equal(3, highlighted.PathVertices.Count);
        Assert.Empty(cleared.PathEdges);
        Assert.Empty(VisualState.Empty.WithPathTo(Id(graph, "C")).PathEdges);
    }

    [Fact]
    public void EveryStepHasRussianDescription()
    {
        var graph = Build(("A", "B", 1), ("B", "C", -3), ("C", "B", 1));
        var steps = new BellmanFordAlgorithm().Run(graph, Id(graph, "A")).Steps
            .Concat(new DijkstraAlgorithm().Run(Build(("A", "B", 1)), 1).Steps);

        Assert.All(steps, step => Assert.False(string.IsNullOrWhiteSpace(StepDescriber.Describe(step, graph))));
    }

    [Theory]
    [InlineData(double.PositiveInfinity, "∞")]
    [InlineData(double.NegativeInfinity, "−∞")]
    [InlineData(2.5, "2,5")]
    [InlineData(-3, "-3")]
    public void FormatsDistances(double distance, string expected)
    {
        Assert.Equal(expected, StepDescriber.FormatDistance(distance));
    }

    [Theory]
    [InlineData(0)]
    [InlineData(1)]
    [InlineData(7)]
    [InlineData(50)]
    public void CircleLayoutProducesDistinctPositions(int count)
    {
        var positions = CircleLayout.Positions(count);

        Assert.Equal(count, positions.Count);
        Assert.Equal(count, positions.Distinct().Count());
    }
}
