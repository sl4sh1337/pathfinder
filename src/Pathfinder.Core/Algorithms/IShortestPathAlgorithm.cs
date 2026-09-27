using System.Collections.Immutable;
using Pathfinder.Core.Model;

namespace Pathfinder.Core.Algorithms;

/// <summary>Complete execution trace of an algorithm together with its final result.</summary>
public sealed record AlgorithmRun(string AlgorithmName, ImmutableArray<AlgorithmStep> Steps, ShortestPathResult Result);

public interface IShortestPathAlgorithm
{
    string Name { get; }

    /// <summary>Returns a user-facing reason why the algorithm cannot run on the graph, or <c>null</c>.</summary>
    string? GetInapplicabilityReason(Graph graph);

    AlgorithmRun Run(Graph graph, int sourceId);
}

internal static class AlgorithmGuard
{
    public static void EnsureRunnable(IShortestPathAlgorithm algorithm, Graph graph, int sourceId)
    {
        var reason = algorithm.GetInapplicabilityReason(graph);
        if (reason is not null)
        {
            throw new InvalidOperationException(reason);
        }

        if (!graph.Vertices.ContainsKey(sourceId))
        {
            throw new ArgumentException($"Вершина с идентификатором {sourceId} не найдена.", nameof(sourceId));
        }
    }

    public static Dictionary<int, double> InitialDistances(Graph graph, int sourceId)
    {
        var distances = graph.Vertices.Keys.ToDictionary(id => id, _ => double.PositiveInfinity);
        distances[sourceId] = 0;
        return distances;
    }
}
