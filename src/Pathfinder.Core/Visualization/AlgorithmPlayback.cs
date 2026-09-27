using Pathfinder.Core.Algorithms;
using Pathfinder.Core.Model;

namespace Pathfinder.Core.Visualization;

/// <summary>Cursor over the steps of an <see cref="AlgorithmRun"/>, advanced by the animation timer.</summary>
public sealed class AlgorithmPlayback(Graph graph, AlgorithmRun run)
{
    public Graph Graph { get; } = graph;

    public AlgorithmRun Run { get; } = run;

    public VisualState State { get; private set; } = VisualState.Empty;

    public int Position { get; private set; }

    public bool IsFinished => Position >= Run.Steps.Length;

    /// <summary>Applies the next step and returns it; <c>null</c> if the run has already finished.</summary>
    public AlgorithmStep? Advance()
    {
        if (IsFinished)
        {
            return null;
        }

        var step = Run.Steps[Position];
        State = VisualStateReducer.Apply(State, Graph, step);
        Position++;
        return step;
    }

    /// <summary>Applies all remaining steps and returns them.</summary>
    public IReadOnlyList<AlgorithmStep> AdvanceToEnd()
    {
        var applied = new List<AlgorithmStep>();
        while (Advance() is { } step)
        {
            applied.Add(step);
        }

        return applied;
    }
}
