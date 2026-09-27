namespace Pathfinder.Core.Model;

/// <summary>Thrown when an operation would break a graph invariant.</summary>
public sealed class GraphRuleException(string message) : InvalidOperationException(message);
