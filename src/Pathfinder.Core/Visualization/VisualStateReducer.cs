using System.Collections.Immutable;
using Pathfinder.Core.Algorithms;
using Pathfinder.Core.Model;

namespace Pathfinder.Core.Visualization;

/// <summary>Pure function <c>(state, step) → state</c> that drives the animation.</summary>
public static class VisualStateReducer
{
    public static VisualState Apply(VisualState state, Graph graph, AlgorithmStep step)
    {
        // Highlights of the previous step are transient.
        var cleared = state with { ActiveEdgeId = null, ActiveEdgeImproved = false };

        return step switch
        {
            InitializationStep s => Initialize(graph, s.SourceId),
            PassStartedStep => cleared with { CurrentVertexId = null },
            VertexSelectedStep s => cleared with { CurrentVertexId = s.VertexId },
            EdgeCheckedStep s => ApplyEdgeCheck(cleared, s),
            VertexFinalizedStep s => cleared with
            {
                CurrentVertexId = null,
                FinalizedVertices = cleared.FinalizedVertices.Add(s.VertexId),
            },
            EarlyTerminationStep => cleared with { CurrentVertexId = null },
            TraceTruncatedStep => cleared,
            NegativeCycleStep s => ApplyNegativeCycle(cleared, s),
            CompletedStep s => cleared with
            {
                CurrentVertexId = null,
                Result = s.Result,
                Distances = s.Result.Distances,
                TreeEdges = s.Result.PredecessorEdges.ToImmutableDictionary(p => p.Key, p => p.Value.Id),

                // Derived from the result, so it is right even if the trace was truncated.
                NegativeCycleVertices = s.Result.Distances
                    .Where(p => double.IsNegativeInfinity(p.Value))
                    .Select(p => p.Key)
                    .ToImmutableHashSet(),
            },
            _ => throw new ArgumentOutOfRangeException(nameof(step), step, "Неизвестный шаг алгоритма."),
        };
    }

    public static VisualState ApplyAll(Graph graph, IEnumerable<AlgorithmStep> steps) =>
        steps.Aggregate(VisualState.Empty, (state, step) => Apply(state, graph, step));

    private static VisualState Initialize(Graph graph, int sourceId) =>
        VisualState.Empty with
        {
            SourceId = sourceId,
            Distances = graph.Vertices.Keys.ToImmutableDictionary(
                id => id,
                id => id == sourceId ? 0 : double.PositiveInfinity),
        };

    private static VisualState ApplyEdgeCheck(VisualState state, EdgeCheckedStep step)
    {
        var active = state with { ActiveEdgeId = step.EdgeId, ActiveEdgeImproved = step.Improved };
        return step.Improved
            ? active with
            {
                Distances = state.Distances.SetItem(step.ToId, step.CandidateDistance),
                TreeEdges = state.TreeEdges.SetItem(step.ToId, step.EdgeId),
            }
            : active;
    }

    private static VisualState ApplyNegativeCycle(VisualState state, NegativeCycleStep step) =>
        state with
        {
            CurrentVertexId = null,
            ActiveEdgeId = step.WitnessEdgeId,
            NegativeCycleVertices = step.AffectedVertexIds.ToImmutableHashSet(),
            Distances = state.Distances.SetItems(
                step.AffectedVertexIds.Select(id => KeyValuePair.Create(id, double.NegativeInfinity))),
            TreeEdges = state.TreeEdges.RemoveRange(step.AffectedVertexIds),
        };
}
