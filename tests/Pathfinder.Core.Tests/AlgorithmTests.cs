using Pathfinder.Core.Algorithms;
using Pathfinder.Core.Model;
using static Pathfinder.Core.Tests.TestGraphs;

namespace Pathfinder.Core.Tests;

public class AlgorithmTests
{
    public static TheoryData<IShortestPathAlgorithm> Algorithms => [new DijkstraAlgorithm(), new BellmanFordAlgorithm()];

    [Theory]
    [MemberData(nameof(Algorithms))]
    public void FindsShortestDistancesAndPaths(IShortestPathAlgorithm algorithm)
    {
        // Arrange: the direct edge A→D is longer than the detour A→B→C→D.
        var graph = Build(("A", "B", 1), ("B", "C", 2), ("C", "D", 1), ("A", "D", 10), ("A", "C", 5));

        // Act
        var result = algorithm.Run(graph, Id(graph, "A")).Result;

        // Assert
        Assert.Equal(0, result.DistanceTo(Id(graph, "A")));
        Assert.Equal(1, result.DistanceTo(Id(graph, "B")));
        Assert.Equal(3, result.DistanceTo(Id(graph, "C")));
        Assert.Equal(4, result.DistanceTo(Id(graph, "D")));
        var path = result.GetPathVertices(Id(graph, "D"))!.Value.Select(id => graph.GetVertex(id).Name);
        Assert.Equal(["A", "B", "C", "D"], path);
    }

    [Theory]
    [MemberData(nameof(Algorithms))]
    public void UnreachableVerticesHaveInfiniteDistanceAndNoPath(IShortestPathAlgorithm algorithm)
    {
        var graph = Build(("A", "B", 1), ("C", "A", 1));

        var result = algorithm.Run(graph, Id(graph, "A")).Result;

        Assert.Equal(double.PositiveInfinity, result.DistanceTo(Id(graph, "C")));
        Assert.Null(result.GetPathEdges(Id(graph, "C")));
        Assert.Empty(result.GetPathEdges(Id(graph, "A"))!.Value);
    }

    [Theory]
    [MemberData(nameof(Algorithms))]
    public void TraceStartsWithInitializationAndEndsWithCompletion(IShortestPathAlgorithm algorithm)
    {
        var graph = Build(("A", "B", 1));

        var run = algorithm.Run(graph, Id(graph, "A"));

        Assert.IsType<InitializationStep>(run.Steps[0]);
        var completed = Assert.IsType<CompletedStep>(run.Steps[^1]);
        Assert.Same(run.Result, completed.Result);
    }

    [Theory]
    [MemberData(nameof(Algorithms))]
    public void RejectsUnknownSource(IShortestPathAlgorithm algorithm)
    {
        var graph = Build(("A", "B", 1));

        Assert.Throws<ArgumentException>(() => algorithm.Run(graph, 999));
    }

    [Fact]
    public void DijkstraRefusesNegativeWeights()
    {
        var graph = Build(("A", "B", -1));
        var dijkstra = new DijkstraAlgorithm();

        Assert.NotNull(dijkstra.GetInapplicabilityReason(graph));
        Assert.Throws<InvalidOperationException>(() => dijkstra.Run(graph, Id(graph, "A")));
    }

    [Fact]
    public void DijkstraFinalizesEachReachableVertexOnce()
    {
        var graph = Build(("A", "B", 1), ("A", "C", 4), ("B", "C", 1), ("C", "D", 1));

        var steps = new DijkstraAlgorithm().Run(graph, Id(graph, "A")).Steps;

        var finalized = steps.OfType<VertexFinalizedStep>().Select(s => graph.GetVertex(s.VertexId).Name);
        Assert.Equal(["A", "B", "C", "D"], finalized);
    }

    [Fact]
    public void BellmanFordHandlesNegativeWeightsWithoutCycle()
    {
        var graph = Build(("A", "B", 4), ("A", "C", 2), ("C", "B", -3), ("B", "D", 1));

        var result = new BellmanFordAlgorithm().Run(graph, Id(graph, "A")).Result;

        Assert.False(result.HasNegativeCycle);
        Assert.Equal(-1, result.DistanceTo(Id(graph, "B")));
        Assert.Equal(0, result.DistanceTo(Id(graph, "D")));
    }

    [Fact]
    public void BellmanFordStopsEarlyWhenNothingChanges()
    {
        var graph = Build(("A", "B", 1), ("B", "C", 1), ("C", "D", 1));

        var steps = new BellmanFordAlgorithm().Run(graph, Id(graph, "A")).Steps;

        // Edges are listed in path order, so one pass settles everything and the second confirms it.
        Assert.Equal(2, steps.OfType<PassStartedStep>().Count());
        Assert.Single(steps.OfType<EarlyTerminationStep>());
    }

    [Fact]
    public void BellmanFordMarksVerticesBehindNegativeCycle()
    {
        // B ⇄ C is a cycle of weight −1; D hangs off it; E is reachable only from A.
        var graph = Build(("A", "B", 1), ("B", "C", -2), ("C", "B", 1), ("C", "D", 1), ("A", "E", 3));

        var run = new BellmanFordAlgorithm().Run(graph, Id(graph, "A"));
        var result = run.Result;

        Assert.True(result.HasNegativeCycle);
        foreach (var name in new[] { "B", "C", "D" })
        {
            Assert.Equal(double.NegativeInfinity, result.DistanceTo(Id(graph, name)));
            Assert.Null(result.GetPathEdges(Id(graph, name)));
        }

        Assert.Equal(3, result.DistanceTo(Id(graph, "E")));
        Assert.Single(run.Steps.OfType<NegativeCycleStep>());
    }

    [Fact]
    public void LongTraceIsTruncatedButResultStaysExact()
    {
        var graph = Build(("A", "B", 1), ("B", "C", -3), ("C", "B", 1), ("C", "D", 1));
        var source = Id(graph, "A");
        var full = new BellmanFordAlgorithm().Run(graph, source);

        var truncated = new BellmanFordAlgorithm().Run(graph, source, maxRecordedSteps: 3);

        Assert.Equal(5, truncated.Steps.Length); // 3 recorded + truncation marker + completion
        Assert.IsType<TraceTruncatedStep>(truncated.Steps[3]);
        Assert.IsType<CompletedStep>(truncated.Steps[4]);
        Assert.Equal(full.Result.Distances, truncated.Result.Distances);
        var state = Visualization.VisualStateReducer.ApplyAll(graph, truncated.Steps);
        Assert.Contains(Id(graph, "D"), state.NegativeCycleVertices);
    }

    [Fact]
    public void SingleVertexGraphIsTrivial()
    {
        var graph = EnsureVertex(Graph.Empty, "A");

        var result = new BellmanFordAlgorithm().Run(graph, Id(graph, "A")).Result;

        Assert.Equal(0, result.DistanceTo(Id(graph, "A")));
        Assert.False(result.HasNegativeCycle);
    }

    [Theory]
    [InlineData(1)]
    [InlineData(2)]
    [InlineData(3)]
    [InlineData(4)]
    [InlineData(5)]
    public void DijkstraAndBellmanFordMatchReferenceOnRandomNonNegativeGraphs(int seed)
    {
        var graph = RandomGraph(new Random(seed), vertexCount: 12, edgeProbability: 0.25, minWeight: 0, maxWeight: 9);
        var source = graph.Vertices.Keys.First();
        var expected = ReferenceDistances(graph, source);

        var dijkstra = new DijkstraAlgorithm().Run(graph, source).Result;
        var bellmanFord = new BellmanFordAlgorithm().Run(graph, source).Result;

        foreach (var (id, distance) in expected)
        {
            Assert.Equal(distance, dijkstra.DistanceTo(id));
            Assert.Equal(distance, bellmanFord.DistanceTo(id));
            AssertPathMatchesDistance(graph, dijkstra, id);
        }
    }

    [Theory]
    [InlineData(10)]
    [InlineData(11)]
    [InlineData(12)]
    [InlineData(13)]
    [InlineData(14)]
    [InlineData(15)]
    public void BellmanFordMatchesReferenceOnRandomGraphsWithNegativeWeights(int seed)
    {
        var graph = RandomGraph(new Random(seed), vertexCount: 9, edgeProbability: 0.3, minWeight: -3, maxWeight: 8);
        var source = graph.Vertices.Keys.First();
        var expected = ReferenceDistances(graph, source);

        var result = new BellmanFordAlgorithm().Run(graph, source).Result;

        Assert.Equal(expected.Values.Any(double.IsNegativeInfinity), result.HasNegativeCycle);
        foreach (var (id, distance) in expected)
        {
            Assert.Equal(distance, result.DistanceTo(id));
            AssertPathMatchesDistance(graph, result, id);
        }
    }

    private static void AssertPathMatchesDistance(Graph graph, ShortestPathResult result, int target)
    {
        var path = result.GetPathEdges(target);
        if (path is null)
        {
            Assert.False(double.IsFinite(result.DistanceTo(target)));
            return;
        }

        Assert.Equal(result.DistanceTo(target), path.Value.Sum(e => e.Weight));
        Assert.All(path.Value, e => Assert.Same(graph.GetEdge(e.Id), e));
    }
}
