using System.Collections.ObjectModel;
using System.Windows;
using System.Windows.Threading;
using Pathfinder.App.Controls;
using Pathfinder.App.Dialogs;
using Pathfinder.Core.Model;

namespace Pathfinder.App;

/// <summary>
/// Main window. This part wires the canvas gestures to graph editing; see
/// <c>MainWindow.Files.cs</c> for import/export and <c>MainWindow.Algorithms.cs</c> for the animation.
/// </summary>
public partial class MainWindow : Window
{
    private const string AppTitle = "Кратчайший путь в графе";
    private const string DefaultEdgeWeight = "1";

    private const string EditHint =
        "Клик по пустому месту — новая вершина · протяните от вершины к вершине — ребро · " +
        "клик по вершине или ребру — изменить/удалить · колесо — масштаб";

    private const string MoveHint = "Перетаскивайте вершины и поле · колесо мыши — масштаб · средняя кнопка — сдвиг";

    private const string LockedHint =
        "Идёт анимация алгоритма — редактирование недоступно. Нажмите «В конец» или «Сброс».";

    private readonly DispatcherTimer _timer = new();
    private readonly ObservableCollection<LogEntry> _log = [];

    private Graph _graph = Graph.Empty;

    public MainWindow()
    {
        InitializeComponent();

        LogList.ItemsSource = _log;
        _timer.Tick += (_, _) => AdvanceOneStep();

        GraphView.EmptyAreaClicked += (_, point) => AddVertexAt(point);
        GraphView.VertexClicked += (_, id) => EditVertex(id);
        GraphView.EdgeClicked += (_, id) => EditEdge(id);
        GraphView.EdgeDrawn += (_, args) => AddEdge(args.FromId, args.ToId);
        GraphView.VertexMoved += (_, args) => MoveVertex(args);
        GraphView.HoveredVertexChanged += (_, id) => ShowPathTo(id ?? SelectedResultVertexId);

        ApplySpeed();
        UpdateTitle();
        UpdateGraphInfo();
        UpdateControls();
        Loaded += (_, _) => OpenFileFromCommandLine();
    }

    /// <summary>Supports "Open with…" and drag-and-drop onto the executable.</summary>
    private void OpenFileFromCommandLine()
    {
        var path = Environment.GetCommandLineArgs().Skip(1).FirstOrDefault();
        if (!string.IsNullOrWhiteSpace(path))
        {
            LoadFile(path);
        }
    }

    /// <summary>Replaces the edited graph; structural edits invalidate any algorithm results.</summary>
    private void ApplyEdit(Graph graph, bool keepAlgorithmState = false)
    {
        if (!keepAlgorithmState)
        {
            ResetAlgorithm();
        }

        SetGraph(graph);
        MarkDirty();
    }

    private void SetGraph(Graph graph)
    {
        _graph = graph;
        GraphView.Graph = graph;
        UpdateGraphInfo();
    }

    private void AddVertexAt(Point position)
    {
        if (GraphRules.ValidateNewVertexCount(_graph) is { } limitError)
        {
            ShowWarning(limitError);
            return;
        }

        var input = InputDialog.Prompt(
            this,
            "Новая вершина",
            "Имя вершины (без пробелов):",
            SuggestVertexName(),
            name => GraphRules.ValidateVertexName(_graph, name));

        if (input.Outcome == InputDialogOutcome.Accepted)
        {
            ApplyEdit(_graph.AddVertex(input.Value, position.X, position.Y).Graph);
        }
    }

    private void EditVertex(int vertexId)
    {
        var vertex = _graph.GetVertex(vertexId);
        var outgoing = _graph.OutgoingEdges(vertexId).Count();
        var incoming = _graph.Edges.Values.Count(e => e.To == vertexId);

        var input = InputDialog.Prompt(
            this,
            "Вершина",
            $"Имя вершины. Исходящих рёбер: {outgoing}, входящих: {incoming}.\nПри удалении вершины удаляются и все её рёбра.",
            vertex.Name,
            name => GraphRules.ValidateVertexName(_graph, name, vertexId),
            allowDelete: true);

        switch (input.Outcome)
        {
            case InputDialogOutcome.Accepted when input.Value != vertex.Name:
                ApplyEdit(_graph.RenameVertex(vertexId, input.Value));
                break;
            case InputDialogOutcome.DeleteRequested:
                ApplyEdit(_graph.RemoveVertex(vertexId));
                break;
        }
    }

    private void AddEdge(int fromId, int toId)
    {
        if (_graph.FindEdge(fromId, toId) is { } existing)
        {
            EditEdge(existing.Id);
            return;
        }

        if (GraphRules.ValidateNewEdge(_graph, fromId, toId) is { } error)
        {
            ShowWarning(error);
            return;
        }

        var input = InputDialog.Prompt(
            this,
            "Новое ребро",
            $"Вес ребра {_graph.GetVertex(fromId).Name} → {_graph.GetVertex(toId).Name}:",
            DefaultEdgeWeight,
            ValidateWeightText);

        if (input.Outcome == InputDialogOutcome.Accepted && GraphRules.TryParseWeight(input.Value, out var weight, out _))
        {
            ApplyEdit(_graph.AddEdge(fromId, toId, weight).Graph);
        }
    }

    private void EditEdge(int edgeId)
    {
        var edge = _graph.GetEdge(edgeId);
        var input = InputDialog.Prompt(
            this,
            "Ребро",
            $"Вес ребра {_graph.GetVertex(edge.From).Name} → {_graph.GetVertex(edge.To).Name}:",
            Core.Visualization.StepDescriber.FormatNumber(edge.Weight),
            ValidateWeightText,
            allowDelete: true);

        switch (input.Outcome)
        {
            case InputDialogOutcome.Accepted when GraphRules.TryParseWeight(input.Value, out var weight, out _)
                                                  && weight != edge.Weight:
                ApplyEdit(_graph.SetEdgeWeight(edgeId, weight));
                break;
            case InputDialogOutcome.DeleteRequested:
                ApplyEdit(_graph.RemoveEdge(edgeId));
                break;
        }
    }

    private void MoveVertex(VertexMovedEventArgs args)
    {
        // Moving a vertex does not change distances, so an ongoing animation may continue.
        ApplyEdit(_graph.MoveVertex(args.VertexId, args.Position.X, args.Position.Y), keepAlgorithmState: true);
    }

    private static string? ValidateWeightText(string text) =>
        GraphRules.TryParseWeight(text, out _, out var error) ? null : error;

    private string SuggestVertexName()
    {
        var number = 1;
        while (_graph.FindVertex(number.ToString()) is not null)
        {
            number++;
        }

        return number.ToString();
    }

    private void Mode_Checked(object sender, RoutedEventArgs e)
    {
        if (GraphView is null)
        {
            return; // Raised by InitializeComponent before the canvas exists.
        }

        GraphView.Mode = MoveModeRadio.IsChecked == true ? CanvasMode.Move : CanvasMode.Edit;
        UpdateHint();
    }

    private void FitToView_Click(object sender, RoutedEventArgs e) => GraphView.FitToView();

    private void ZoomIn_Click(object sender, RoutedEventArgs e) => GraphView.ZoomBy(1.25);

    private void ZoomOut_Click(object sender, RoutedEventArgs e) => GraphView.ZoomBy(1 / 1.25);

    private void LegendMenuItem_Click(object sender, RoutedEventArgs e) =>
        Legend.Visibility = LegendMenuItem.IsChecked ? Visibility.Visible : Visibility.Collapsed;

    private void UpdateHint()
    {
        HintText.Text = GraphView.IsEditingLocked && GraphView.Mode == CanvasMode.Edit
            ? LockedHint
            : GraphView.Mode == CanvasMode.Edit ? EditHint : MoveHint;
    }

    private void UpdateGraphInfo()
    {
        GraphInfoText.Text = $"вершин: {_graph.Vertices.Count}   рёбер: {_graph.Edges.Count}";
    }

    private void ShowWarning(string message) =>
        MessageBox.Show(this, message, AppTitle, MessageBoxButton.OK, MessageBoxImage.Warning);

    private void ShowError(string message) =>
        MessageBox.Show(this, message, AppTitle, MessageBoxButton.OK, MessageBoxImage.Error);

    public sealed record LogEntry(string Text);
}
