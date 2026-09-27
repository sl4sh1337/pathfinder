using System.Collections.Immutable;

namespace Pathfinder.Core.Algorithms;

/// <summary>One observable event in the execution of a shortest-path algorithm, used to animate it.</summary>
public abstract record AlgorithmStep;

/// <summary>All distances are set to +∞, the source distance to 0.</summary>
public sealed record InitializationStep(int SourceId) : AlgorithmStep;

/// <summary>Bellman–Ford: a new pass over all edges begins.</summary>
public sealed record PassStartedStep(int Pass, int MaxPasses) : AlgorithmStep;

/// <summary>Dijkstra: the unvisited vertex with the smallest distance is chosen.</summary>
public sealed record VertexSelectedStep(int VertexId, double Distance) : AlgorithmStep;

/// <summary>Edge relaxation: is <c>dist[from] + weight</c> smaller than <c>dist[to]</c>?</summary>
public sealed record EdgeCheckedStep(
    int EdgeId,
    int FromId,
    int ToId,
    double OldDistance,
    double CandidateDistance,
    bool Improved) : AlgorithmStep;

/// <summary>Dijkstra: all outgoing edges of the vertex were processed; its distance is final.</summary>
public sealed record VertexFinalizedStep(int VertexId) : AlgorithmStep;

/// <summary>Bellman–Ford: a pass changed nothing, so the distances are already final.</summary>
public sealed record EarlyTerminationStep(int Pass) : AlgorithmStep;

/// <summary>Bellman–Ford: a negative cycle is reachable; the listed vertices have distance −∞.</summary>
public sealed record NegativeCycleStep(int WitnessEdgeId, ImmutableArray<int> AffectedVertexIds) : AlgorithmStep;

/// <summary>The algorithm finished.</summary>
public sealed record CompletedStep(ShortestPathResult Result) : AlgorithmStep;

/// <summary>The trace reached <see cref="StepRecorder.MaxRecordedSteps"/>; further steps are computed but not recorded.</summary>
public sealed record TraceTruncatedStep(int Limit) : AlgorithmStep;
