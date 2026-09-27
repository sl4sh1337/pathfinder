using System.Collections.Immutable;
using Pathfinder.Core.Model;

namespace Pathfinder.Core.Algorithms;

/// <summary>
/// Bellman–Ford algorithm: relax every edge |V| − 1 times (stopping early once a pass changes nothing).
/// Works with negative weights; a further improvement after the last pass reveals a negative cycle,
/// and every vertex reachable from it gets distance −∞.
/// </summary>
public sealed class BellmanFordAlgorithm : IShortestPathAlgorithm
{
    public string Name => "Алгоритм Беллмана–Форда";

    public string? GetInapplicabilityReason(Graph graph) => null;

    public AlgorithmRun Run(Graph graph, int sourceId) => Run(graph, sourceId, StepRecorder.MaxRecordedSteps);

    internal AlgorithmRun Run(Graph graph, int sourceId, int maxRecordedSteps)
    {
        AlgorithmGuard.EnsureRunnable(this, graph, sourceId);

        var steps = new StepRecorder(maxRecordedSteps);
        var distances = AlgorithmGuard.InitialDistances(graph, sourceId);
        var predecessors = new Dictionary<int, Edge>();
        var maxPasses = graph.Vertices.Count - 1;
        var converged = maxPasses == 0;

        steps.Add(new InitializationStep(sourceId));
        for (var pass = 1; pass <= maxPasses && !converged; pass++)
        {
            steps.Add(new PassStartedStep(pass, maxPasses));
            var changed = RelaxAllEdges(graph, distances, predecessors, steps);
            if (!changed)
            {
                steps.Add(new EarlyTerminationStep(pass));
                converged = true;
            }
        }

        var hasNegativeCycle = !converged && MarkNegativeCycle(graph, distances, predecessors, steps);
        var result = new ShortestPathResult(
            sourceId,
            distances.ToImmutableDictionary(),
            predecessors.ToImmutableDictionary(),
            hasNegativeCycle);
        return new AlgorithmRun(Name, steps.Complete(new CompletedStep(result)), result);
    }

    private static bool RelaxAllEdges(
        Graph graph,
        Dictionary<int, double> distances,
        Dictionary<int, Edge> predecessors,
        StepRecorder steps)
    {
        var changed = false;
        foreach (var edge in graph.Edges.Values)
        {
            var fromDistance = distances[edge.From];
            if (double.IsPositiveInfinity(fromDistance))
            {
                continue; // The tail is not reached yet: nothing to relax.
            }

            var oldDistance = distances[edge.To];
            var candidate = fromDistance + edge.Weight;
            var improved = candidate < oldDistance;
            steps.Add(new EdgeCheckedStep(edge.Id, edge.From, edge.To, oldDistance, candidate, improved));

            if (improved)
            {
                distances[edge.To] = candidate;
                predecessors[edge.To] = edge;
                changed = true;
            }
        }

        return changed;
    }

    /// <summary>
    /// Performs the |V|-th check pass. Every vertex that can still be improved lies on or behind
    /// a negative cycle; it and everything reachable from it get distance −∞.
    /// </summary>
    private static bool MarkNegativeCycle(
        Graph graph,
        Dictionary<int, double> distances,
        Dictionary<int, Edge> predecessors,
        StepRecorder steps)
    {
        var witnesses = graph.Edges.Values
            .Where(e => distances[e.From] + e.Weight < distances[e.To])
            .ToList();
        if (witnesses.Count == 0)
        {
            return false;
        }

        var affected = CollectReachable(graph, witnesses.Select(e => e.To));
        foreach (var vertexId in affected)
        {
            distances[vertexId] = double.NegativeInfinity;
            predecessors.Remove(vertexId);
        }

        steps.Add(new NegativeCycleStep(witnesses[0].Id, affected.Order().ToImmutableArray()));
        return true;
    }

    private static HashSet<int> CollectReachable(Graph graph, IEnumerable<int> seeds)
    {
        var reached = new HashSet<int>();
        var pending = new Stack<int>(seeds);
        while (pending.TryPop(out var vertexId))
        {
            if (!reached.Add(vertexId))
            {
                continue;
            }

            foreach (var edge in graph.OutgoingEdges(vertexId))
            {
                pending.Push(edge.To);
            }
        }

        return reached;
    }
}
