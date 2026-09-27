using System.Collections.Immutable;
using Pathfinder.Core.Algorithms;

namespace Pathfinder.Core.Visualization;

/// <summary>
/// What the canvas has to show at a given moment of an algorithm run.
/// Semantic rather than visual: the renderer decides colours and shapes.
/// </summary>
public sealed record VisualState
{
    public static VisualState Empty { get; } = new();

    public int? SourceId { get; init; }

    /// <summary>Tentative (or final) distance label of every vertex.</summary>
    public ImmutableDictionary<int, double> Distances { get; init; } = ImmutableDictionary<int, double>.Empty;

    /// <summary>Current shortest-path tree: vertex id → id of the edge leading into it.</summary>
    public ImmutableDictionary<int, int> TreeEdges { get; init; } = ImmutableDictionary<int, int>.Empty;

    /// <summary>Dijkstra: vertices whose distance is final.</summary>
    public ImmutableHashSet<int> FinalizedVertices { get; init; } = [];

    /// <summary>Vertices with distance −∞ due to a negative cycle.</summary>
    public ImmutableHashSet<int> NegativeCycleVertices { get; init; } = [];

    /// <summary>The vertex being processed right now.</summary>
    public int? CurrentVertexId { get; init; }

    /// <summary>The edge being relaxed right now and whether the relaxation improved a distance.</summary>
    public int? ActiveEdgeId { get; init; }

    public bool ActiveEdgeImproved { get; init; }

    /// <summary>A highlighted shortest path (e.g. to the vertex under the cursor after the run).</summary>
    public ImmutableHashSet<int> PathEdges { get; init; } = [];

    public ImmutableHashSet<int> PathVertices { get; init; } = [];

    public ShortestPathResult? Result { get; init; }

    public bool IsCompleted => Result is not null;

    public bool IsTreeEdge(int edgeId) => TreeEdges.ContainsValue(edgeId);

    public VisualState WithPathTo(int? targetId)
    {
        if (Result is null || targetId is null)
        {
            return this with { PathEdges = [], PathVertices = [] };
        }

        var edges = Result.GetPathEdges(targetId.Value);
        var vertices = Result.GetPathVertices(targetId.Value);
        return this with
        {
            PathEdges = edges?.Select(e => e.Id).ToImmutableHashSet() ?? [],
            PathVertices = vertices?.ToImmutableHashSet() ?? [],
        };
    }
}
