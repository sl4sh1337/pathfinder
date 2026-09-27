using System.Collections.Immutable;
using System.Globalization;
using System.Text;
using Pathfinder.Core.Layout;
using Pathfinder.Core.Model;

namespace Pathfinder.Core.IO;

public sealed record ParseError(int Line, string Message)
{
    public override string ToString() => Line > 0 ? $"Строка {Line}: {Message}" : Message;
}

public sealed record GraphParseResult(Graph? Graph, ImmutableArray<ParseError> Errors)
{
    public bool IsSuccess => Graph is not null;
}

/// <summary>
/// Plain-text graph format: a list of edges, one per line.
/// <code>
/// # comment
/// A B 4.5      edge A → B with weight 4.5
/// C            vertex without edges
/// @ A 10 20    optional position of vertex A (also declares it)
/// </code>
/// Tokens are separated by whitespace, numbers use '.' as the decimal separator.
/// Vertices without an explicit position are laid out on a circle.
/// </summary>
public static class GraphFileFormat
{
    public const string FileFilter = "Файлы графов (*.graph;*.txt)|*.graph;*.txt|Все файлы (*.*)|*.*";
    public const string DefaultExtension = ".graph";
    public const long MaxFileSizeBytes = 5 * 1024 * 1024;
    public const int MaxReportedErrors = 20;

    private const char CommentChar = '#';
    private const string PositionMarker = "@";

    public static GraphParseResult Parse(string text)
    {
        ArgumentNullException.ThrowIfNull(text);

        var document = new ParsedDocument();
        var lines = text.Split('\n');
        for (var i = 0; i < lines.Length && document.Errors.Count < MaxReportedErrors; i++)
        {
            ParseLine(lines[i], i + 1, document);
        }

        document.ValidateLimits();
        return document.Errors.Count > 0
            ? new GraphParseResult(null, document.Errors.ToImmutableArray())
            : new GraphParseResult(document.BuildGraph(), []);
    }

    public static string Serialize(Graph graph)
    {
        ArgumentNullException.ThrowIfNull(graph);

        var text = new StringBuilder()
            .AppendLine("# Граф для программы нахождения кратчайшего пути")
            .AppendLine("# @ <вершина> <x> <y>        — вершина и её положение")
            .AppendLine("# <откуда> <куда> <вес>     — ориентированное ребро");

        foreach (var vertex in graph.Vertices.Values)
        {
            text.AppendLine($"{PositionMarker} {vertex.Name} {Format(Math.Round(vertex.X, 2))} {Format(Math.Round(vertex.Y, 2))}");
        }

        foreach (var edge in graph.Edges.Values)
        {
            var from = graph.GetVertex(edge.From).Name;
            var to = graph.GetVertex(edge.To).Name;
            text.AppendLine($"{from} {to} {Format(edge.Weight)}");
        }

        return text.ToString();
    }

    private static string Format(double value) => value.ToString(CultureInfo.InvariantCulture);

    private static void ParseLine(string rawLine, int lineNumber, ParsedDocument document)
    {
        var commentStart = rawLine.IndexOf(CommentChar);
        var content = commentStart >= 0 ? rawLine[..commentStart] : rawLine;
        var tokens = content.Split((char[]?)null, StringSplitOptions.RemoveEmptyEntries);

        switch (tokens)
        {
            case []:
                return;
            case [PositionMarker, var name, var x, var y]:
                document.AddPosition(lineNumber, name, x, y);
                return;
            case [PositionMarker, ..]:
                document.AddError(lineNumber, "строка положения должна иметь вид «@ <вершина> <x> <y>».");
                return;
            case [var name]:
                document.AddVertex(lineNumber, name);
                return;
            case [var from, var to, var weight]:
                document.AddEdge(lineNumber, from, to, weight);
                return;
            default:
                document.AddError(
                    lineNumber,
                    $"ожидалось «<откуда> <куда> <вес>» или имя вершины, найдено элементов: {tokens.Length}.");
                return;
        }
    }

    /// <summary>Mutable accumulator used only while a single file is being parsed.</summary>
    private sealed class ParsedDocument
    {
        private readonly List<string> _vertexOrder = [];
        private readonly HashSet<string> _vertexNames = new(StringComparer.Ordinal);
        private readonly Dictionary<string, (double X, double Y)> _positions = new(StringComparer.Ordinal);
        private readonly List<(string From, string To, double Weight)> _edges = [];
        private readonly HashSet<(string, string)> _edgeKeys = [];

        public List<ParseError> Errors { get; } = [];

        public void AddError(int line, string message) => Errors.Add(new ParseError(line, message));

        public void AddVertex(int line, string name)
        {
            var error = GraphRules.ValidateVertexNameFormat(name);
            if (error is not null)
            {
                AddError(line, $"«{name}»: {error}");
                return;
            }

            if (_vertexNames.Add(name))
            {
                _vertexOrder.Add(name);
            }
        }

        public void AddPosition(int line, string name, string xText, string yText)
        {
            if (!TryParseNumber(xText, out var x) || !TryParseNumber(yText, out var y))
            {
                AddError(line, $"координаты «{xText} {yText}» должны быть конечными числами.");
                return;
            }

            if (!_positions.TryAdd(name, (x, y)))
            {
                AddError(line, $"положение вершины «{name}» уже задано.");
                return;
            }

            AddVertex(line, name);
        }

        public void AddEdge(int line, string from, string to, string weightText)
        {
            if (!TryParseNumber(weightText, out var weight) || GraphRules.ValidateWeight(weight) is not null)
            {
                AddError(line, $"вес «{weightText}» должен быть числом не больше {GraphRules.MaxAbsWeight:G} по модулю.");
                return;
            }

            var errorsBefore = Errors.Count;
            AddVertex(line, from);
            AddVertex(line, to);
            if (Errors.Count != errorsBefore)
            {
                return;
            }

            if (string.Equals(from, to, StringComparison.Ordinal))
            {
                AddError(line, $"петля «{from} → {to}» не поддерживается.");
                return;
            }

            if (!_edgeKeys.Add((from, to)))
            {
                AddError(line, $"ребро «{from} → {to}» указано повторно.");
                return;
            }

            _edges.Add((from, to, weight));
        }

        public void ValidateLimits()
        {
            if (_vertexOrder.Count > GraphRules.MaxVertexCount)
            {
                AddError(0, $"Граф содержит {_vertexOrder.Count} вершин, допустимо не более {GraphRules.MaxVertexCount}.");
            }

            if (_edges.Count > GraphRules.MaxEdgeCount)
            {
                AddError(0, $"Граф содержит {_edges.Count} рёбер, допустимо не более {GraphRules.MaxEdgeCount}.");
            }
        }

        public Graph BuildGraph()
        {
            var unplaced = _vertexOrder.Where(name => !_positions.ContainsKey(name)).ToList();
            var circle = CircleLayout.Positions(unplaced.Count);
            var layout = unplaced.Zip(circle).ToDictionary(p => p.First, p => p.Second, StringComparer.Ordinal);

            var graph = Graph.Empty;
            foreach (var name in _vertexOrder)
            {
                var (x, y) = _positions.TryGetValue(name, out var position) ? position : layout[name];
                graph = graph.AddVertex(name, x, y).Graph;
            }

            foreach (var (from, to, weight) in _edges)
            {
                graph = graph.AddEdge(graph.FindVertex(from)!.Id, graph.FindVertex(to)!.Id, weight).Graph;
            }

            return graph;
        }

        private static bool TryParseNumber(string text, out double value) =>
            double.TryParse(text, NumberStyles.Float, CultureInfo.InvariantCulture, out value)
            && double.IsFinite(value);
    }
}
