using System.Windows;
using Pathfinder.Core.Model;

namespace Pathfinder.App.Controls;

/// <summary>
/// World-space geometry of a drawn edge: a quadratic Bézier from the rim of the tail vertex to the
/// rim of the head vertex. Opposite edges (A→B and B→A) bend to different sides so both stay visible.
/// </summary>
internal readonly record struct EdgeShape(Point Start, Point Control, Point End, Vector EndDirection)
{
    public const double VertexRadius = 22;
    public const double ReverseEdgeBend = 34;
    public const double ArrowLength = 12;
    public const double ArrowHalfWidth = 5.5;

    private const int HitTestSamples = 24;

    /// <summary>Where the stroke ends, so it does not poke through the arrow tip.</summary>
    public Point StrokeEnd => End - EndDirection * (ArrowLength * 0.8);

    public Point LabelPosition => Evaluate(0.5);

    public static EdgeShape? Create(Point from, Point to, bool hasReverse)
    {
        var delta = to - from;
        if (delta.Length < VertexRadius * 2)
        {
            return null; // Overlapping vertices: nothing sensible to draw.
        }

        var direction = delta / delta.Length;
        var normal = new Vector(-direction.Y, direction.X);
        var control = from + delta / 2 + normal * (hasReverse ? ReverseEdgeBend : 0);

        var startDirection = Normalize(control - from);
        var endDirection = Normalize(to - control);
        return new EdgeShape(
            from + startDirection * VertexRadius,
            control,
            to - endDirection * (VertexRadius + 1),
            endDirection);
    }

    public static EdgeShape? Create(Graph graph, Edge edge, Func<int, Point> positionOf) =>
        Create(positionOf(edge.From), positionOf(edge.To), graph.HasReverseEdge(edge));

    public Point Evaluate(double t)
    {
        var u = 1 - t;
        return new Point(
            u * u * Start.X + 2 * u * t * Control.X + t * t * End.X,
            u * u * Start.Y + 2 * u * t * Control.Y + t * t * End.Y);
    }

    public (Point Tip, Point Left, Point Right) ArrowHead()
    {
        var normal = new Vector(-EndDirection.Y, EndDirection.X);
        var back = End - EndDirection * ArrowLength;
        return (End, back + normal * ArrowHalfWidth, back - normal * ArrowHalfWidth);
    }

    public double DistanceTo(Point point)
    {
        var best = double.MaxValue;
        var previous = Start;
        for (var i = 1; i <= HitTestSamples; i++)
        {
            var current = Evaluate((double)i / HitTestSamples);
            best = Math.Min(best, DistanceToSegment(point, previous, current));
            previous = current;
        }

        return best;
    }

    private static double DistanceToSegment(Point p, Point a, Point b)
    {
        var ab = b - a;
        var lengthSquared = ab.LengthSquared;
        var t = lengthSquared == 0 ? 0 : Math.Clamp(Vector.Multiply(p - a, ab) / lengthSquared, 0, 1);
        return (p - (a + ab * t)).Length;
    }

    private static Vector Normalize(Vector v) => v.Length == 0 ? v : v / v.Length;
}
