namespace Pathfinder.Core.Model;

/// <summary>A graph vertex: a unique name plus its position on the drawing plane.</summary>
public sealed record Vertex(int Id, string Name, double X, double Y);
