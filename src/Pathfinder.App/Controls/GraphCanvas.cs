using System.Windows;
using System.Windows.Input;
using Pathfinder.Core.Model;
using Pathfinder.Core.Visualization;
using VisualState = Pathfinder.Core.Visualization.VisualState;

namespace Pathfinder.App.Controls;

public enum CanvasMode
{
    /// <summary>Clicks add and edit vertices and edges; dragging from a vertex draws an edge.</summary>
    Edit,

    /// <summary>Dragging moves vertices or the whole view; nothing is added or deleted.</summary>
    Move,
}

public sealed record EdgeDrawnEventArgs(int FromId, int ToId);

public sealed record VertexMovedEventArgs(int VertexId, Point Position);

/// <summary>
/// Interactive drawing surface for a <see cref="Graph"/>: renders it with the current
/// <see cref="VisualState"/>, supports pan/zoom and reports editing gestures as events.
/// The control never modifies the graph itself — the owner applies changes and assigns a new graph.
/// </summary>
public sealed partial class GraphCanvas : FrameworkElement
{
    public const double MinScale = 0.2;
    public const double MaxScale = 4;

    private const double DragThreshold = 4;
    private const double ZoomStep = 1.15;
    private const double EdgeHitTolerance = 7;
    private const double FitMargin = 70;

    private Graph _graph = Graph.Empty;
    private VisualState _visualState = VisualState.Empty;
    private CanvasMode _mode = CanvasMode.Edit;
    private int? _hoveredVertexId;

    // View transform: screen = world * _scale + _pan.
    private double _scale = 1;
    private Vector _pan;
    private bool _fitPending = true;

    private Gesture _gesture = Gesture.None;
    private Point _pressScreen;
    private Point _lastScreen;
    private int? _pressVertexId;
    private int? _pressEdgeId;
    private Point _dragWorld;
    private Vector _dragGrabOffset;

    public GraphCanvas()
    {
        ClipToBounds = true;
        Focusable = true;
    }

    private enum Gesture
    {
        None,
        Pending,
        DrawingEdge,
        DraggingVertex,
        Panning,
    }

    public event EventHandler<Point>? EmptyAreaClicked;

    public event EventHandler<int>? VertexClicked;

    public event EventHandler<int>? EdgeClicked;

    public event EventHandler<EdgeDrawnEventArgs>? EdgeDrawn;

    public event EventHandler<VertexMovedEventArgs>? VertexMoved;

    public event EventHandler<int?>? HoveredVertexChanged;

    public Graph Graph
    {
        get => _graph;
        set
        {
            _graph = value ?? throw new ArgumentNullException(nameof(value));
            InvalidateVisual();
        }
    }

    public VisualState VisualState
    {
        get => _visualState;
        set
        {
            _visualState = value ?? throw new ArgumentNullException(nameof(value));
            InvalidateVisual();
        }
    }

    public CanvasMode Mode
    {
        get => _mode;
        set
        {
            _mode = value;
            CancelGesture();
        }
    }

    /// <summary>When set (e.g. during an animation) the canvas reports no editing gestures.</summary>
    public bool IsEditingLocked { get; set; }

    public double Scale => _scale;

    private bool CanEdit => Mode == CanvasMode.Edit && !IsEditingLocked;

    public void FitToView()
    {
        if (ActualWidth <= 0 || ActualHeight <= 0)
        {
            _fitPending = true;
            return;
        }

        _fitPending = false;
        if (_graph.IsEmpty)
        {
            _scale = 1;
            _pan = new Vector(ActualWidth / 2, ActualHeight / 2);
        }
        else
        {
            var vertices = _graph.Vertices.Values;
            var bounds = new Rect(
                new Point(vertices.Min(v => v.X), vertices.Min(v => v.Y)),
                new Point(vertices.Max(v => v.X), vertices.Max(v => v.Y)));
            bounds.Inflate(FitMargin, FitMargin);
            _scale = Math.Clamp(Math.Min(ActualWidth / bounds.Width, ActualHeight / bounds.Height), MinScale, 1.5);
            var center = new Point(bounds.X + bounds.Width / 2, bounds.Y + bounds.Height / 2);
            _pan = new Vector(ActualWidth / 2 - center.X * _scale, ActualHeight / 2 - center.Y * _scale);
        }

        InvalidateVisual();
    }

    public void ZoomBy(double factor) => ZoomAround(new Point(ActualWidth / 2, ActualHeight / 2), factor);

    /// <summary>World coordinates of the visible centre — a good spot for a vertex added from the menu.</summary>
    public Point ViewCenterInWorld() => ToWorld(new Point(ActualWidth / 2, ActualHeight / 2));

    protected override void OnRenderSizeChanged(SizeChangedInfo sizeInfo)
    {
        base.OnRenderSizeChanged(sizeInfo);
        if (_fitPending)
        {
            FitToView();
        }
    }

    protected override void OnMouseLeftButtonDown(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonDown(e);
        if (_gesture == Gesture.Panning)
        {
            return; // A middle-button pan is in progress; do not let the left button hijack it.
        }

        Focus();
        var screen = e.GetPosition(this);
        var world = ToWorld(screen);
        _pressScreen = _lastScreen = screen;
        _pressVertexId = HitVertex(world);
        _pressEdgeId = _pressVertexId is null ? HitEdge(world) : null;
        _gesture = Gesture.Pending;
        CaptureMouse();
        e.Handled = true;
    }

    protected override void OnMouseDown(MouseButtonEventArgs e)
    {
        base.OnMouseDown(e);
        if (e.ChangedButton == MouseButton.Middle)
        {
            _lastScreen = e.GetPosition(this);
            _gesture = Gesture.Panning;
            CaptureMouse();
            e.Handled = true;
        }
    }

    protected override void OnMouseMove(MouseEventArgs e)
    {
        base.OnMouseMove(e);
        var screen = e.GetPosition(this);
        if (_gesture == Gesture.Pending && (screen - _pressScreen).Length >= DragThreshold)
        {
            BeginDrag();
        }

        switch (_gesture)
        {
            case Gesture.Panning:
                _pan += screen - _lastScreen;
                InvalidateVisual();
                break;
            case Gesture.DraggingVertex:
                _dragWorld = ToWorld(screen) + _dragGrabOffset;
                InvalidateVisual();
                break;
            case Gesture.DrawingEdge:
                _dragWorld = ToWorld(screen);
                InvalidateVisual();
                break;
        }

        _lastScreen = screen;
        UpdateHover(ToWorld(screen));
    }

    protected override void OnMouseLeftButtonUp(MouseButtonEventArgs e)
    {
        base.OnMouseLeftButtonUp(e);
        var world = ToWorld(e.GetPosition(this));
        var gesture = _gesture;
        var pressVertexId = _pressVertexId;
        var pressEdgeId = _pressEdgeId;
        CancelGesture();

        switch (gesture)
        {
            case Gesture.Pending:
                RaiseClick(world, pressVertexId, pressEdgeId);
                break;
            case Gesture.DrawingEdge when HitVertex(world) is { } target && target != pressVertexId:
                EdgeDrawn?.Invoke(this, new EdgeDrawnEventArgs(pressVertexId!.Value, target));
                break;
            case Gesture.DraggingVertex:
                var position = ToWorld(e.GetPosition(this)) + _dragGrabOffset;
                VertexMoved?.Invoke(this, new VertexMovedEventArgs(pressVertexId!.Value, position));
                break;
        }

        e.Handled = true;
    }

    protected override void OnMouseUp(MouseButtonEventArgs e)
    {
        base.OnMouseUp(e);
        if (e.ChangedButton == MouseButton.Middle && _gesture == Gesture.Panning)
        {
            CancelGesture();
        }
    }

    protected override void OnMouseWheel(MouseWheelEventArgs e)
    {
        base.OnMouseWheel(e);
        ZoomAround(e.GetPosition(this), e.Delta > 0 ? ZoomStep : 1 / ZoomStep);
        e.Handled = true;
    }

    protected override void OnMouseLeave(MouseEventArgs e)
    {
        base.OnMouseLeave(e);
        if (_gesture == Gesture.None)
        {
            SetHoveredVertex(null);
        }
    }

    protected override void OnLostMouseCapture(MouseEventArgs e)
    {
        base.OnLostMouseCapture(e);
        if (_gesture != Gesture.None)
        {
            CancelGesture();
        }
    }

    private void BeginDrag()
    {
        if (_pressVertexId is not null && CanEdit)
        {
            _gesture = Gesture.DrawingEdge;
            _dragWorld = ToWorld(_pressScreen);
        }
        else if (_pressVertexId is { } movedId)
        {
            var vertex = _graph.GetVertex(movedId);
            _gesture = Gesture.DraggingVertex;
            _dragGrabOffset = new Point(vertex.X, vertex.Y) - ToWorld(_pressScreen);
            _dragWorld = new Point(vertex.X, vertex.Y);
        }
        else
        {
            _gesture = Gesture.Panning;
        }
    }

    private void RaiseClick(Point world, int? vertexId, int? edgeId)
    {
        if (!CanEdit)
        {
            return;
        }

        if (vertexId is { } v)
        {
            VertexClicked?.Invoke(this, v);
        }
        else if (edgeId is { } edge)
        {
            EdgeClicked?.Invoke(this, edge);
        }
        else
        {
            EmptyAreaClicked?.Invoke(this, world);
        }
    }

    private void CancelGesture()
    {
        var wasActive = _gesture is Gesture.DrawingEdge or Gesture.DraggingVertex;
        _gesture = Gesture.None;
        _pressVertexId = null;
        _pressEdgeId = null;
        if (IsMouseCaptured)
        {
            ReleaseMouseCapture();
        }

        if (wasActive)
        {
            InvalidateVisual();
        }
    }

    private void ZoomAround(Point screenAnchor, double factor)
    {
        var world = ToWorld(screenAnchor);
        _scale = Math.Clamp(_scale * factor, MinScale, MaxScale);
        _pan = new Vector(screenAnchor.X - world.X * _scale, screenAnchor.Y - world.Y * _scale);
        InvalidateVisual();
    }

    private void UpdateHover(Point world)
    {
        // While a vertex is dragged it stays "hovered"; otherwise (including edge drawing, where it
        // highlights the prospective target) the vertex under the cursor is hovered.
        var hovered = _gesture == Gesture.DraggingVertex ? _hoveredVertexId : HitVertex(world);
        SetHoveredVertex(hovered);
        Cursor = hovered is not null ? (CanEdit ? Cursors.Hand : Cursors.SizeAll)
            : _gesture == Gesture.Panning ? Cursors.ScrollAll
            : CanEdit ? Cursors.Cross
            : Cursors.Arrow;
    }

    private void SetHoveredVertex(int? vertexId)
    {
        if (_hoveredVertexId == vertexId)
        {
            return;
        }

        _hoveredVertexId = vertexId;
        InvalidateVisual();
        HoveredVertexChanged?.Invoke(this, vertexId);
    }

    private Point ToWorld(Point screen) => new((screen.X - _pan.X) / _scale, (screen.Y - _pan.Y) / _scale);

    /// <summary>Position of a vertex, taking an in-progress drag into account.</summary>
    private Point PositionOf(int vertexId)
    {
        if (_gesture == Gesture.DraggingVertex && _pressVertexId == vertexId)
        {
            return _dragWorld;
        }

        var vertex = _graph.GetVertex(vertexId);
        return new Point(vertex.X, vertex.Y);
    }

    private int? HitVertex(Point world)
    {
        // Last drawn is on top, so search in reverse drawing order.
        foreach (var vertex in _graph.Vertices.Values.Reverse())
        {
            if ((PositionOf(vertex.Id) - world).Length <= EdgeShape.VertexRadius)
            {
                return vertex.Id;
            }
        }

        return null;
    }

    private int? HitEdge(Point world)
    {
        var tolerance = EdgeHitTolerance / _scale;
        return _graph.Edges.Values
            .Select(edge => (edge.Id, Shape: EdgeShape.Create(_graph, edge, PositionOf)))
            .Where(x => x.Shape is not null)
            .Select(x => (x.Id, Distance: Math.Min(
                x.Shape!.Value.DistanceTo(world),
                (x.Shape.Value.LabelPosition - world).Length - LabelHitRadius)))
            .Where(x => x.Distance <= tolerance)
            .OrderBy(x => x.Distance)
            .Select(x => (int?)x.Id)
            .FirstOrDefault();
    }
}
