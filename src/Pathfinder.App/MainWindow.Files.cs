using System.ComponentModel;
using System.IO;
using System.Security;
using System.Windows;
using System.Windows.Input;
using Microsoft.Win32;
using Pathfinder.Core.IO;
using Pathfinder.Core.Model;

namespace Pathfinder.App;

/// <summary>Creating, importing and exporting graphs; tracking unsaved changes.</summary>
public partial class MainWindow
{
    private const int MaxErrorsInMessage = 10;

    private string? _filePath;
    private bool _isDirty;

    private static string SamplesDirectory => Path.Combine(AppContext.BaseDirectory, "Samples");

    private void New_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        ResetAlgorithm();
        SetGraph(Graph.Empty);
        SetDocument(path: null);
        GraphView.FitToView();
    }

    private void Open_Executed(object sender, ExecutedRoutedEventArgs e)
    {
        if (!ConfirmDiscardChanges())
        {
            return;
        }

        var dialog = new OpenFileDialog
        {
            Title = "Открыть граф",
            Filter = GraphFileFormat.FileFilter,
            DefaultExt = GraphFileFormat.DefaultExtension,
            InitialDirectory = _filePath is not null ? Path.GetDirectoryName(_filePath) : ExistingSamplesDirectory(),
        };

        if (dialog.ShowDialog(this) == true)
        {
            LoadFile(dialog.FileName);
        }
    }

    private void Save_Executed(object sender, ExecutedRoutedEventArgs e) => Save();

    private void SaveAs_Executed(object sender, ExecutedRoutedEventArgs e) => SaveAs();

    private void Exit_Click(object sender, RoutedEventArgs e) => Close();

    private void Window_Closing(object? sender, CancelEventArgs e)
    {
        if (!ConfirmDiscardChanges())
        {
            e.Cancel = true;
            return;
        }

        _timer.Stop();
    }

    private void LoadFile(string path)
    {
        string text;
        try
        {
            if (new FileInfo(path).Length > GraphFileFormat.MaxFileSizeBytes)
            {
                ShowError($"Файл слишком большой (допустимо не более {GraphFileFormat.MaxFileSizeBytes / 1024 / 1024} МБ).");
                return;
            }

            text = File.ReadAllText(path);
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            ShowError($"Не удалось прочитать файл «{Path.GetFileName(path)}»:\n{ex.Message}");
            return;
        }

        var result = GraphFileFormat.Parse(text);
        if (!result.IsSuccess)
        {
            ShowError(DescribeParseErrors(path, result));
            return;
        }

        ResetAlgorithm();
        SetGraph(result.Graph!);
        SetDocument(path);
        GraphView.FitToView();
    }

    private static string DescribeParseErrors(string path, GraphParseResult result)
    {
        var shown = result.Errors.Take(MaxErrorsInMessage).Select(error => "• " + error);
        var more = result.Errors.Length > MaxErrorsInMessage
            ? $"\n… и другие ошибки ({result.Errors.Length - MaxErrorsInMessage})."
            : string.Empty;
        return $"Файл «{Path.GetFileName(path)}» содержит ошибки и не был загружен:\n\n{string.Join("\n", shown)}{more}";
    }

    private bool Save() => _filePath is null ? SaveAs() : SaveTo(_filePath);

    private bool SaveAs()
    {
        var dialog = new SaveFileDialog
        {
            Title = "Сохранить граф",
            Filter = GraphFileFormat.FileFilter,
            DefaultExt = GraphFileFormat.DefaultExtension,
            FileName = _filePath is not null ? Path.GetFileName(_filePath) : "граф" + GraphFileFormat.DefaultExtension,
        };

        return dialog.ShowDialog(this) == true && SaveTo(dialog.FileName);
    }

    private bool SaveTo(string path)
    {
        try
        {
            File.WriteAllText(path, GraphFileFormat.Serialize(_graph));
        }
        catch (Exception ex) when (ex is IOException or UnauthorizedAccessException or SecurityException)
        {
            ShowError($"Не удалось сохранить файл «{Path.GetFileName(path)}»:\n{ex.Message}");
            return false;
        }

        SetDocument(path);
        return true;
    }

    /// <summary>Asks whether to save unsaved changes. Returns <c>false</c> if the user cancelled.</summary>
    private bool ConfirmDiscardChanges()
    {
        if (!_isDirty)
        {
            return true;
        }

        var answer = MessageBox.Show(
            this,
            "Граф был изменён. Сохранить изменения?",
            AppTitle,
            MessageBoxButton.YesNoCancel,
            MessageBoxImage.Question);

        return answer switch
        {
            MessageBoxResult.Yes => Save(),
            MessageBoxResult.No => true,
            _ => false,
        };
    }

    private void SetDocument(string? path)
    {
        _filePath = path;
        _isDirty = false;
        UpdateTitle();
    }

    private void MarkDirty()
    {
        _isDirty = true;
        UpdateTitle();
    }

    private void UpdateTitle()
    {
        var name = _filePath is null ? "Новый граф" : Path.GetFileName(_filePath);
        Title = $"{name}{(_isDirty ? " •" : string.Empty)} — {AppTitle}";
    }

    private static string? ExistingSamplesDirectory() => Directory.Exists(SamplesDirectory) ? SamplesDirectory : null;

    private void FileFormatHelp_Click(object sender, RoutedEventArgs e)
    {
        const string help =
            "Граф хранится в текстовом файле — списке рёбер, по одному на строку:\n\n" +
            "    A B 4.5        ребро A → B с весом 4.5\n" +
            "    C              вершина без рёбер\n" +
            "    @ A 10 20      (необязательно) положение вершины A\n" +
            "    # текст        комментарий\n\n" +
            "Элементы разделяются пробелами, дробная часть отделяется точкой.\n" +
            "Имена вершин не должны содержать пробелов и символов # и @.\n" +
            "Рёбра ориентированные; петли и повторные рёбра не допускаются.\n" +
            "Вершины без указанного положения располагаются по окружности.";
        MessageBox.Show(this, help, "Формат файла", MessageBoxButton.OK, MessageBoxImage.Information);
    }

    private void About_Click(object sender, RoutedEventArgs e)
    {
        const string about =
            "Программа нахождения кратчайшего пути в графе\n" +
            "A Program for Finding Shortest Path in a Graph\n\n" +
            "Позволяет строить ориентированные взвешенные графы, импортировать и экспортировать их, " +
            "а также наглядно показывает работу алгоритмов Дейкстры и Беллмана–Форда.";
        MessageBox.Show(this, about, "О программе", MessageBoxButton.OK, MessageBoxImage.Information);
    }
}
