namespace Pathfinder.Core.Model;

/// <summary>A directed weighted edge <c>From → To</c>.</summary>
public sealed record Edge(int Id, int From, int To, double Weight);
