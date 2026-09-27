using System.Globalization;

namespace Pathfinder.Core.Model;

/// <summary>
/// Invariants shared by the editor, the file format and the graph model itself.
/// Validators return a user-facing error message, or <c>null</c> when the value is valid.
/// </summary>
public static class GraphRules
{
    public const int MaxNameLength = 32;
    public const int MaxVertexCount = 1_000;
    public const int MaxEdgeCount = 20_000;
    public const double MaxAbsWeight = 1e9;

    /// <summary>Characters reserved by the file format (comments and position lines).</summary>
    public const string ReservedNameChars = "#@";

    public static string? ValidateVertexName(Graph graph, string? name, int? ignoreVertexId = null)
    {
        var formatError = ValidateVertexNameFormat(name);
        if (formatError is not null)
        {
            return formatError;
        }

        var existing = graph.FindVertex(name!);
        return existing is not null && existing.Id != ignoreVertexId
            ? $"Вершина с именем «{name}» уже существует."
            : null;
    }

    public static string? ValidateVertexNameFormat(string? name)
    {
        if (string.IsNullOrEmpty(name))
        {
            return "Имя вершины не может быть пустым.";
        }

        if (name.Length > MaxNameLength)
        {
            return $"Имя вершины не может быть длиннее {MaxNameLength} символов.";
        }

        if (name.Any(char.IsWhiteSpace))
        {
            return "Имя вершины не может содержать пробелы.";
        }

        if (name.Any(c => char.IsControl(c) || ReservedNameChars.Contains(c)))
        {
            return $"Имя вершины не может содержать символы «{ReservedNameChars}» и управляющие символы.";
        }

        return null;
    }

    public static string? ValidateWeight(double weight)
    {
        if (!double.IsFinite(weight))
        {
            return "Вес ребра должен быть конечным числом.";
        }

        return Math.Abs(weight) > MaxAbsWeight
            ? $"Вес ребра по модулю не может превышать {MaxAbsWeight:G}."
            : null;
    }

    /// <summary>Parses a weight typed by a user; both '.' and ',' are accepted as the decimal separator.</summary>
    public static bool TryParseWeight(string? text, out double weight, out string? error)
    {
        weight = 0;
        var normalized = text?.Trim().Replace(',', '.');
        if (string.IsNullOrEmpty(normalized)
            || !double.TryParse(normalized, NumberStyles.Float, CultureInfo.InvariantCulture, out weight))
        {
            error = "Вес ребра должен быть числом, например 5 или -2,5.";
            return false;
        }

        error = ValidateWeight(weight);
        return error is null;
    }

    public static string? ValidateNewEdge(Graph graph, int from, int to)
    {
        if (!graph.Vertices.ContainsKey(from) || !graph.Vertices.ContainsKey(to))
        {
            return "Ребро должно соединять существующие вершины.";
        }

        if (from == to)
        {
            return "Петли (рёбра из вершины в саму себя) не поддерживаются.";
        }

        if (graph.FindEdge(from, to) is not null)
        {
            return "Такое ребро уже существует.";
        }

        return graph.Edges.Count >= MaxEdgeCount
            ? $"Граф не может содержать больше {MaxEdgeCount} рёбер."
            : null;
    }

    public static string? ValidateNewVertexCount(Graph graph) =>
        graph.Vertices.Count >= MaxVertexCount
            ? $"Граф не может содержать больше {MaxVertexCount} вершин."
            : null;
}
