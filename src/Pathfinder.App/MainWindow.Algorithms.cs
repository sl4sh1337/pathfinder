using System.Windows;
using System.Windows.Controls;
using System.Windows.Input;
using Pathfinder.App.Dialogs;
using Pathfinder.Core.Algorithms;
using Pathfinder.Core.Model;
using Pathfinder.Core.Visualization;
using VisualState = Pathfinder.Core.Visualization.VisualState;

namespace Pathfinder.App;

/// <summary>Running the algorithms and animating their execution step by step.</summary>
public partial class MainWindow
{
    private const double SlowestStepMs = 1500;
    private const double FastestStepMs = 25;
    private const string IdleTitle = "Алгоритм не запущен";
    private const string IdleProgress = "Постройте граф и выберите алгоритм на панели сверху.";

    private AlgorithmPlayback? _playback;
    private int? _lastSourceId;
    private bool _isPaused;

    private bool IsAnimating => _playback is { IsFinished: false };

    private int? SelectedResultVertexId => (ResultGrid.SelectedItem as ResultRow)?.VertexId;

    private async void Dijkstra_Click(object sender, RoutedEventArgs e) => await StartAlgorithmAsync(new DijkstraAlgorithm());

    private async void BellmanFord_Click(object sender, RoutedEventArgs e) => await StartAlgorithmAsync(new BellmanFordAlgorithm());

    private async Task StartAlgorithmAsync(IShortestPathAlgorithm algorithm)
    {
        // The modal dialog pumps messages, so an ongoing animation must not tick underneath it.
        var wasTicking = _timer.IsEnabled;
        _timer.Stop();

        var sourceId = AskForSource(algorithm);
        if (sourceId is null)
        {
            if (wasTicking)
            {
                _timer.Start();
            }

            return;
        }

        ResetAlgorithm();
        _lastSourceId = sourceId;
        var graph = _graph;
        AlgorithmRun run;
        IsEnabled = false;
        Mouse.OverrideCursor = Cursors.Wait;
        try
        {
            // Bellman–Ford on the largest allowed graph takes noticeable time: keep the UI responsive.
            // The graph is immutable, so handing it to another thread is safe.
            run = await Task.Run(() => algorithm.Run(graph, sourceId.Value));
        }
        finally
        {
            Mouse.OverrideCursor = null;
            IsEnabled = true;
        }

        if (!IsLoaded)
        {
            return; // The window was closed while the algorithm was running.
        }

        _playback = new AlgorithmPlayback(graph, run);
        AlgorithmTitle.Text = algorithm.Name;
        GraphView.IsEditingLocked = true;

        AdvanceOneStep();
        if (IsAnimating)
        {
            _timer.Start();
        }

        UpdateControls();
    }

    private int? AskForSource(IShortestPathAlgorithm algorithm)
    {
        if (_graph.IsEmpty)
        {
            ShowWarning("Граф пуст. Добавьте вершины щелчком по полю или откройте граф из файла.");
            return null;
        }

        if (algorithm.GetInapplicabilityReason(_graph) is { } reason)
        {
            ShowWarning(reason);
            return null;
        }

        var preselected = _lastSourceId is { } last && _graph.Vertices.ContainsKey(last) ? last : (int?)null;
        return SelectVertexDialog.Prompt(this, algorithm.Name, _graph, preselected);
    }

    private void AdvanceOneStep()
    {
        if (_playback?.Advance() is not { } step)
        {
            return;
        }

        AppendLog(StepDescriber.Describe(step, _playback.Graph));
        ShowPlaybackState(step);
        if (_playback.IsFinished)
        {
            FinishPlayback();
        }
    }

    private void ShowPlaybackState(AlgorithmStep step)
    {
        var playback = _playback!;
        GraphView.VisualState = playback.State;
        UpdateProgressText();

        // The table only changes when some distance does.
        if (step is InitializationStep or EdgeCheckedStep { Improved: true } or NegativeCycleStep or CompletedStep)
        {
            UpdateResultGrid();
        }
    }

    private void FinishPlayback()
    {
        _timer.Stop();
        _isPaused = false;
        GraphView.IsEditingLocked = false;
        UpdateProgressText();
        UpdateControls();
    }

    private void Pause_Click(object sender, RoutedEventArgs e)
    {
        if (!IsAnimating)
        {
            return;
        }

        _isPaused = !_isPaused;
        if (_isPaused)
        {
            _timer.Stop();
        }
        else
        {
            _timer.Start();
        }

        UpdateProgressText();
        UpdateControls();
    }

    private void Step_Click(object sender, RoutedEventArgs e)
    {
        if (!IsAnimating)
        {
            return;
        }

        if (!_isPaused)
        {
            _isPaused = true;
            _timer.Stop();
            UpdateControls();
        }

        AdvanceOneStep();
    }

    private void Skip_Click(object sender, RoutedEventArgs e)
    {
        if (_playback is null || _playback.IsFinished)
        {
            return;
        }

        var remaining = _playback.AdvanceToEnd();

        // Narrating thousands of skipped steps would only flood the log; keep the milestones.
        var milestones = remaining.Where(s => s is NegativeCycleStep or TraceTruncatedStep or CompletedStep).ToList();
        AppendLog($"… пропущено шагов: {remaining.Count - milestones.Count}.");
        foreach (var step in milestones)
        {
            AppendLog(StepDescriber.Describe(step, _playback.Graph));
        }

        ShowPlaybackState(remaining[^1]);
        FinishPlayback();
    }

    private void Reset_Click(object sender, RoutedEventArgs e) => ResetAlgorithm();

    private void ResetAlgorithm()
    {
        _timer.Stop();
        _playback = null;
        _isPaused = false;
        _log.Clear();
        GraphView.VisualState = VisualState.Empty;
        GraphView.IsEditingLocked = false;
        AlgorithmTitle.Text = IdleTitle;
        ProgressText.Text = IdleProgress;
        ResultGrid.ItemsSource = null;
        UpdateControls();
    }

    private void ShowPathTo(int? vertexId)
    {
        if (_playback is { IsFinished: true } playback)
        {
            GraphView.VisualState = playback.State.WithPathTo(vertexId);
        }
    }

    private void ResultGrid_SelectionChanged(object sender, SelectionChangedEventArgs e) =>
        ShowPathTo(SelectedResultVertexId);

    private void SpeedSlider_ValueChanged(object sender, RoutedPropertyChangedEventArgs<double> e)
    {
        if (SpeedText is not null)
        {
            ApplySpeed(); // Also raised by InitializeComponent before SpeedText exists.
        }
    }

    private void ApplySpeed()
    {
        // Exponential mapping: the slider feels uniform from "slow lecture" to "just show me".
        var delay = SlowestStepMs * Math.Pow(FastestStepMs / SlowestStepMs, SpeedSlider.Value / SpeedSlider.Maximum);
        _timer.Interval = TimeSpan.FromMilliseconds(delay);
        SpeedText.Text = $"{delay:0} мс/шаг";
    }

    private void AppendLog(string text)
    {
        var entry = new LogEntry(text);
        _log.Add(entry);
        LogList.ScrollIntoView(entry);
    }

    private void UpdateProgressText()
    {
        if (_playback is not { } playback)
        {
            return;
        }

        ProgressText.Text = playback.IsFinished
            ? "Выполнение завершено. Наведите курсор на вершину или выберите строку таблицы, чтобы увидеть путь."
            : $"Шаг {playback.Position} из {playback.Run.Steps.Length}{(_isPaused ? " · пауза" : string.Empty)}";
    }

    private void UpdateResultGrid()
    {
        if (_playback is not { } playback)
        {
            return;
        }

        var state = playback.State;
        ResultGrid.ItemsSource = playback.Graph.Vertices.Values
            .OrderBy(v => v.Name, StringComparer.CurrentCulture)
            .Select(v => new ResultRow(
                v.Id,
                v.Name,
                StepDescriber.FormatDistance(state.Distances.GetValueOrDefault(v.Id, double.PositiveInfinity)),
                state.Result is null ? string.Empty : DescribePath(state.Result, playback.Graph, v.Id)))
            .ToList();
    }

    private static string DescribePath(ShortestPathResult result, Graph graph, int vertexId)
    {
        if (result.GetPathVertices(vertexId) is { } path)
        {
            return string.Join(" → ", path.Select(id => graph.GetVertex(id).Name));
        }

        return double.IsNegativeInfinity(result.DistanceTo(vertexId))
            ? "нет: отрицательный цикл"
            : "недостижима";
    }

    private void UpdateControls()
    {
        var animating = IsAnimating;
        PauseButton.IsEnabled = animating;
        StepButton.IsEnabled = animating;
        SkipButton.IsEnabled = animating;
        ResetButton.IsEnabled = _playback is not null;
        PauseButton.Content = _isPaused ? "▶  Продолжить" : "❚❚  Пауза";
        UpdateHint();
    }

    public sealed record ResultRow(int VertexId, string Vertex, string Distance, string Path);
}
