using System.Collections.Immutable;
using Pathfinder.Core.Model;

namespace Pathfinder.Core.Algorithms;

/// <summary>
/// Dijkstra's algorithm: repeatedly pick the unvisited vertex with the smallest tentative distance,
/// relax its outgoing edges and mark it visited. Requires non-negative edge weights.
/// </summary>
public sealed class DijkstraAlgorithm : IShortestPathAlgorithm
{
    public string Name => "Алгоритм Дейкстры";

    public string? GetInapplicabilityReason(Graph graph) =>
        graph.HasNegativeWeights
            ? "Алгоритм Дейкстры неприменим к графам с рёбрами отрицательного веса. Используйте алгоритм Беллмана–Форда."
            : null;

    public AlgorithmRun Run(Graph graph, int sourceId) => Run(graph, sourceId, StepRecorder.MaxRecordedSteps);

    internal AlgorithmRun Run(Graph graph, int sourceId, int maxRecordedSteps)
    {
        AlgorithmGuard.EnsureRunnable(this, graph, sourceId);

        var steps = new StepRecorder(maxRecordedSteps);
        var distances = AlgorithmGuard.InitialDistances(graph, sourceId);
        var predecessors = new Dictionary<int, Edge>();
        var visited = new HashSet<int>();
        var queue = new PriorityQueue<int, (double Distance, int Id)>();

        steps.Add(new InitializationStep(sourceId));
        queue.Enqueue(sourceId, (0, sourceId));

        while (queue.TryDequeue(out var current, out var priority))
        {
            // Lazy deletion: skip stale queue entries for vertices already processed.
            if (!visited.Add(current))
            {
                continue;
            }

            steps.Add(new VertexSelectedStep(current, priority.Distance));
            foreach (var edge in graph.OutgoingEdges(current).Where(e => !visited.Contains(e.To)).OrderBy(e => e.Id))
            {
                var oldDistance = distances[edge.To];
                var candidate = priority.Distance + edge.Weight;
                var improved = candidate < oldDistance;
                steps.Add(new EdgeCheckedStep(edge.Id, edge.From, edge.To, oldDistance, candidate, improved));

                if (improved)
                {
                    distances[edge.To] = candidate;
                    predecessors[edge.To] = edge;
                    queue.Enqueue(edge.To, (candidate, edge.To));
                }
            }

            steps.Add(new VertexFinalizedStep(current));
        }

        var result = new ShortestPathResult(
            sourceId,
            distances.ToImmutableDictionary(),
            predecessors.ToImmutableDictionary(),
            hasNegativeCycle: false);
        return new AlgorithmRun(Name, steps.Complete(new CompletedStep(result)), result);
    }
}
