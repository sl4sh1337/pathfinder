using System.Globalization;
using System.Windows;
using System.Windows.Media;
using Pathfinder.Core.Model;
using Pathfinder.Core.Visualization;
using VisualState = Pathfinder.Core.Visualization.VisualState;

namespace Pathfinder.App.Controls;

public sealed partial class GraphCanvas
{
    private const double GridSpacing = 40;
    private const double MinGridSpacingOnScreen = 14;
    private const double LabelHitRadius = 10;
    private const double NameFontSize = 13;
    private const double MinNameFontSize = 8;
    private const double LabelFontSize = 12;
    private const double BadgeFontSize = 11.5;

    private static readonly Typeface NameTypeface = new(
        new FontFamily("Segoe UI"), FontStyles.Normal, FontWeights.SemiBold, FontStretches.Normal);

    private static readonly Typeface LabelTypeface = new(
        new FontFamily("Cascadia Mono, Consolas"), FontStyles.Normal, FontWeights.Normal, FontStretches.Normal);

    private enum EdgeRole
    {
        Normal,
        Tree,
        Checking,
        Improved,
        Rejected,
        Path,
    }

    protected override void OnRender(DrawingContext dc)
    {
        var bounds = new Rect(0, 0, ActualWidth, ActualHeight);
        dc.DrawRectangle(GraphPalette.Paper, null, bounds); // Also makes the whole area hit-testable.
        DrawGrid(dc, bounds);

        dc.PushTransform(new MatrixTransform(_scale, 0, 0, _scale, _pan.X, _pan.Y));
        var edges = _graph.Edges.Values.Select(e => (Edge: e, Role: RoleOf(e))).OrderBy(x => x.Role);
        foreach (var (edge, role) in edges)
        {
            DrawEdge(dc, edge, role);
        }

        if (_gesture == Gesture.DrawingEdge && _pressVertexId is { } fromId)
        {
            DrawTemporaryEdge(dc, PositionOf(fromId), _dragWorld);
        }

        foreach (var vertex in _graph.Vertices.Values)
        {
            DrawVertex(dc, vertex);
        }

        dc.Pop();
    }

    private void DrawGrid(DrawingContext dc, Rect bounds)
    {
        var spacing = GridSpacing * _scale;
        while (spacing < MinGridSpacingOnScreen)
        {
            spacing *= 2;
        }

        var dot = new GeometryDrawing(GraphPalette.GridDot, null, new EllipseGeometry(new Point(0, 0), 1.1, 1.1));
        var brush = new DrawingBrush(dot)
        {
            TileMode = TileMode.Tile,
            ViewportUnits = BrushMappingMode.Absolute,
            Viewport = new Rect(Mod(_pan.X, spacing), Mod(_pan.Y, spacing), spacing, spacing),
            ViewboxUnits = BrushMappingMode.Absolute,
            Viewbox = new Rect(-spacing / 2, -spacing / 2, spacing, spacing),
            Stretch = Stretch.None,
        };
        dc.DrawRectangle(brush, null, bounds);
    }

    private EdgeRole RoleOf(Edge edge)
    {
        var state = _visualState;
        if (state.PathEdges.Contains(edge.Id))
        {
            return EdgeRole.Path;
        }

        if (state.ActiveEdgeId == edge.Id)
        {
            return state.NegativeCycleVertices.IsEmpty
                ? (state.ActiveEdgeImproved ? EdgeRole.Improved : EdgeRole.Rejected)
                : EdgeRole.Checking;
        }

        return state.IsTreeEdge(edge.Id) ? EdgeRole.Tree : EdgeRole.Normal;
    }

    private void DrawEdge(DrawingContext dc, Edge edge, EdgeRole role)
    {
        if (EdgeShape.Create(_graph, edge, PositionOf) is not { } shape)
        {
            return;
        }

        var (brush, thickness) = role switch
        {
            EdgeRole.Path => (GraphPalette.Path, 4.5),
            EdgeRole.Checking => (GraphPalette.NegativeCycle, 4.0),
            EdgeRole.Improved => (GraphPalette.Improved, 4.0),
            EdgeRole.Rejected => (GraphPalette.Rejected, 3.5),
            EdgeRole.Tree => (GraphPalette.Tree, 3.0),
            _ => (_visualState.SourceId is null ? GraphPalette.Ink : GraphPalette.InkMuted, 1.6),
        };

        var pen = new Pen(brush, thickness) { StartLineCap = PenLineCap.Round, EndLineCap = PenLineCap.Flat };
        var figure = new PathFigure { StartPoint = shape.Start };
        figure.Segments.Add(new QuadraticBezierSegment(shape.Control, shape.StrokeEnd, true));
        dc.DrawGeometry(null, pen, new PathGeometry([figure]));

        var (tip, left, right) = shape.ArrowHead();
        var arrow = new StreamGeometry();
        using (var ctx = arrow.Open())
        {
            ctx.BeginFigure(tip, true, true);
            ctx.PolyLineTo([left, right], true, true);
        }

        dc.DrawGeometry(brush, null, arrow);
        DrawEdgeLabel(dc, shape.LabelPosition, StepDescriber.FormatNumber(edge.Weight), brush, role != EdgeRole.Normal);
    }

    private void DrawEdgeLabel(DrawingContext dc, Point center, string text, Brush accent, bool emphasized)
    {
        var formatted = CreateText(text, LabelTypeface, LabelFontSize, emphasized ? accent : GraphPalette.Ink);
        var box = new Rect(
            center.X - formatted.Width / 2 - 5,
            center.Y - formatted.Height / 2 - 1,
            formatted.Width + 10,
            formatted.Height + 2);
        var border = emphasized ? new Pen(accent, 1.2) : null;
        dc.DrawRoundedRectangle(GraphPalette.LabelBackground, border, box, 4, 4);
        dc.DrawText(formatted, new Point(box.X + 5, box.Y + 1));
    }

    private static void DrawTemporaryEdge(DrawingContext dc, Point from, Point to)
    {
        var pen = new Pen(GraphPalette.Tree, 2) { DashStyle = new DashStyle([4, 3], 0) };
        var direction = to - from;
        if (direction.Length <= EdgeShape.VertexRadius)
        {
            return;
        }

        direction.Normalize();
        dc.DrawLine(pen, from + direction * EdgeShape.VertexRadius, to);
        dc.DrawEllipse(GraphPalette.Tree, null, to, 3.5, 3.5);
    }

    private void DrawVertex(DrawingContext dc, Vertex vertex)
    {
        var center = PositionOf(vertex.Id);
        var (fill, stroke, strokeThickness) = VertexColors(vertex.Id);

        if (_hoveredVertexId == vertex.Id)
        {
            dc.DrawEllipse(GraphPalette.Selection, null, center, EdgeShape.VertexRadius + 7, EdgeShape.VertexRadius + 7);
        }

        dc.DrawEllipse(fill, new Pen(stroke, strokeThickness), center, EdgeShape.VertexRadius, EdgeShape.VertexRadius);
        DrawVertexName(dc, vertex.Name, center);

        if (_visualState.Distances.TryGetValue(vertex.Id, out var distance))
        {
            DrawDistanceBadge(dc, center, distance);
        }
    }

    private (Brush Fill, Brush Stroke, double Thickness) VertexColors(int vertexId)
    {
        var state = _visualState;
        if (state.NegativeCycleVertices.Contains(vertexId))
        {
            return (GraphPalette.NegativeCycleLight, GraphPalette.NegativeCycle, 3);
        }

        if (state.PathVertices.Contains(vertexId))
        {
            return (GraphPalette.PathLight, GraphPalette.Path, 3.5);
        }

        if (state.CurrentVertexId == vertexId)
        {
            return (GraphPalette.ActiveLight, GraphPalette.Active, 3.5);
        }

        if (state.SourceId == vertexId)
        {
            return (GraphPalette.TreeLight, GraphPalette.Tree, 3.5);
        }

        if (state.ActiveEdgeId is { } edgeId && _graph.Edges.TryGetValue(edgeId, out var edge) && edge.To == vertexId)
        {
            return (GraphPalette.VertexFill, state.ActiveEdgeImproved ? GraphPalette.Improved : GraphPalette.Rejected, 3);
        }

        if (state.FinalizedVertices.Contains(vertexId))
        {
            return (GraphPalette.Finalized, GraphPalette.Ink, 2);
        }

        return (GraphPalette.VertexFill, GraphPalette.Ink, 2);
    }

    private void DrawVertexName(DrawingContext dc, string name, Point center)
    {
        const double innerWidth = EdgeShape.VertexRadius * 2 - 8;
        var text = CreateText(name, NameTypeface, NameFontSize, GraphPalette.Ink);
        if (text.Width > innerWidth)
        {
            var fontSize = Math.Max(MinNameFontSize, NameFontSize * innerWidth / text.Width);
            text = CreateText(name, NameTypeface, fontSize, GraphPalette.Ink);
        }

        dc.DrawText(text, new Point(center.X - text.Width / 2, center.Y - text.Height / 2));
    }

    private void DrawDistanceBadge(DrawingContext dc, Point center, double distance)
    {
        var isInfinite = double.IsPositiveInfinity(distance);
        var background = _visualState.NegativeCycleVertices.Count > 0 && double.IsNegativeInfinity(distance)
            ? GraphPalette.NegativeCycle
            : isInfinite ? GraphPalette.InkMuted : GraphPalette.Ink;

        var text = CreateText(StepDescriber.FormatDistance(distance), LabelTypeface, BadgeFontSize, GraphPalette.BadgeText);
        var width = Math.Max(text.Width + 12, 24);
        var box = new Rect(
            center.X - width / 2,
            center.Y - EdgeShape.VertexRadius - text.Height - 8,
            width,
            text.Height + 3);
        dc.DrawRoundedRectangle(background, null, box, box.Height / 2, box.Height / 2);
        dc.DrawText(text, new Point(center.X - text.Width / 2, box.Y + 1.5));
    }

    private FormattedText CreateText(string text, Typeface typeface, double size, Brush brush) =>
        new(text, CultureInfo.CurrentUICulture, FlowDirection.LeftToRight, typeface, size, brush,
            VisualTreeHelper.GetDpi(this).PixelsPerDip);

    private static double Mod(double value, double modulus) => ((value % modulus) + modulus) % modulus;
}
