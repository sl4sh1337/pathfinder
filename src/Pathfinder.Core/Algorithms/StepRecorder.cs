using System.Collections.Immutable;

namespace Pathfinder.Core.Algorithms;

/// <summary>
/// Collects the execution trace, keeping memory bounded on large graphs: after
/// <see cref="MaxRecordedSteps"/> steps only a single <see cref="TraceTruncatedStep"/> is kept,
/// while the algorithm itself runs to completion and its result stays exact.
/// </summary>
internal sealed class StepRecorder(int maxRecordedSteps = StepRecorder.MaxRecordedSteps)
{
    public const int MaxRecordedSteps = 100_000;

    private readonly ImmutableArray<AlgorithmStep>.Builder _steps = ImmutableArray.CreateBuilder<AlgorithmStep>();
    private bool _truncated;

    public void Add(AlgorithmStep step)
    {
        if (_truncated)
        {
            return;
        }

        if (_steps.Count >= maxRecordedSteps)
        {
            _steps.Add(new TraceTruncatedStep(maxRecordedSteps));
            _truncated = true;
            return;
        }

        _steps.Add(step);
    }

    /// <summary>Appends the final step (always recorded) and returns the whole trace.</summary>
    public ImmutableArray<AlgorithmStep> Complete(CompletedStep completed)
    {
        _steps.Add(completed);
        return _steps.ToImmutable();
    }
}
