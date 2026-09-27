using System.Collections.Immutable;
using Pathfinder.Core.Model;

namespace Pathfinder.Core.Algorithms;

/// <summary>
/// Shortest distances from <see cref="SourceId"/>. Unreachable vertices have distance +∞;
/// vertices reachable through a negative cycle have distance −∞ and no shortest path.
/// </summary>
public sealed class ShortestPathResult(
    int sourceId,
    ImmutableDictionary<int, double> distances,
    ImmutableDictionary<int, Edge> predecessorEdges,
    bool hasNegativeCycle)
{
    public int SourceId { get; } = sourceId;

    public ImmutableDictionary<int, double> Distances { get; } = distances;

    /// <summary>For each reached vertex (except the source) — the last edge of its shortest path.</summary>
    public ImmutableDictionary<int, Edge> PredecessorEdges { get; } = predecessorEdges;

    public bool HasNegativeCycle { get; } = hasNegativeCycle;

    public double DistanceTo(int vertexId) =>
        Distances.TryGetValue(vertexId, out var distance) ? distance : double.PositiveInfinity;

    /// <summary>
    /// Edges of the shortest path from the source to <paramref name="targetId"/> in travel order;
    /// an empty list for the source itself, <c>null</c> when no shortest path exists.
    /// </summary>
    public ImmutableArray<Edge>? GetPathEdges(int targetId)
    {
        if (!double.IsFinite(DistanceTo(targetId)))
        {
            return null;
        }

        var path = ImmutableArray.CreateBuilder<Edge>();
        var current = targetId;
        while (current != SourceId)
        {
            // A path longer than the vertex count means the predecessor chain is corrupted.
            if (!PredecessorEdges.TryGetValue(current, out var edge) || path.Count >= Distances.Count)
            {
                return null;
            }

            path.Add(edge);
            current = edge.From;
        }

        path.Reverse();
        return path.ToImmutable();
    }

    /// <summary>Vertex ids along the shortest path, starting with the source; <c>null</c> when there is none.</summary>
    public ImmutableArray<int>? GetPathVertices(int targetId)
    {
        var edges = GetPathEdges(targetId);
        return edges is null
            ? null
            : edges.Value.Select(e => e.To).Prepend(SourceId).ToImmutableArray();
    }
}
