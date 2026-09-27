using System.Collections.Immutable;

namespace Pathfinder.Core.Model;

/// <summary>
/// Immutable directed weighted graph. Every modification returns a new instance,
/// so snapshots can be handed to algorithms and the UI without defensive copies.
/// </summary>
public sealed class Graph
{
    public static Graph Empty { get; } = new(
        ImmutableSortedDictionary<int, Vertex>.Empty,
        ImmutableSortedDictionary<int, Edge>.Empty,
        nextVertexId: 1,
        nextEdgeId: 1);

    private readonly int _nextVertexId;
    private readonly int _nextEdgeId;
    private readonly Lazy<ILookup<int, Edge>> _outgoing;
    private readonly Lazy<ImmutableDictionary<string, Vertex>> _byName;
    private readonly Lazy<ImmutableHashSet<(int From, int To)>> _edgeKeys;

    private Graph(
        ImmutableSortedDictionary<int, Vertex> vertices,
        ImmutableSortedDictionary<int, Edge> edges,
        int nextVertexId,
        int nextEdgeId)
    {
        Vertices = vertices;
        Edges = edges;
        _nextVertexId = nextVertexId;
        _nextEdgeId = nextEdgeId;
        _outgoing = new Lazy<ILookup<int, Edge>>(() => Edges.Values.ToLookup(e => e.From));
        _byName = new Lazy<ImmutableDictionary<string, Vertex>>(
            () => Vertices.Values.ToImmutableDictionary(v => v.Name, StringComparer.Ordinal));
        _edgeKeys = new Lazy<ImmutableHashSet<(int, int)>>(
            () => Edges.Values.Select(e => (e.From, e.To)).ToImmutableHashSet());
    }

    public ImmutableSortedDictionary<int, Vertex> Vertices { get; }

    public ImmutableSortedDictionary<int, Edge> Edges { get; }

    public bool IsEmpty => Vertices.IsEmpty;

    public bool HasNegativeWeights => Edges.Values.Any(e => e.Weight < 0);

    public IEnumerable<Edge> OutgoingEdges(int vertexId) => _outgoing.Value[vertexId];

    public Vertex? FindVertex(string name) => _byName.Value.GetValueOrDefault(name);

    public Edge? FindEdge(int from, int to) =>
        _edgeKeys.Value.Contains((from, to))
            ? OutgoingEdges(from).First(e => e.To == to)
            : null;

    public bool HasReverseEdge(Edge edge) => _edgeKeys.Value.Contains((edge.To, edge.From));

    public Vertex GetVertex(int id) =>
        Vertices.TryGetValue(id, out var vertex)
            ? vertex
            : throw new GraphRuleException($"Вершина с идентификатором {id} не найдена.");

    public Edge GetEdge(int id) =>
        Edges.TryGetValue(id, out var edge)
            ? edge
            : throw new GraphRuleException($"Ребро с идентификатором {id} не найдено.");

    public (Graph Graph, Vertex Vertex) AddVertex(string name, double x, double y)
    {
        ThrowIfInvalid(GraphRules.ValidateNewVertexCount(this) ?? GraphRules.ValidateVertexName(this, name));
        ThrowIfInvalid(ValidatePosition(x, y));

        var vertex = new Vertex(_nextVertexId, name, x, y);
        var graph = new Graph(Vertices.Add(vertex.Id, vertex), Edges, _nextVertexId + 1, _nextEdgeId);
        return (graph, vertex);
    }

    public Graph RenameVertex(int id, string name)
    {
        var vertex = GetVertex(id);
        ThrowIfInvalid(GraphRules.ValidateVertexName(this, name, id));
        return WithVertex(vertex with { Name = name });
    }

    public Graph MoveVertex(int id, double x, double y)
    {
        var vertex = GetVertex(id);
        ThrowIfInvalid(ValidatePosition(x, y));
        return WithVertex(vertex with { X = x, Y = y });
    }

    /// <summary>Removes the vertex together with every edge incident to it.</summary>
    public Graph RemoveVertex(int id)
    {
        GetVertex(id);
        var incident = Edges.Values.Where(e => e.From == id || e.To == id).Select(e => e.Id);
        return new Graph(Vertices.Remove(id), Edges.RemoveRange(incident), _nextVertexId, _nextEdgeId);
    }

    public (Graph Graph, Edge Edge) AddEdge(int from, int to, double weight)
    {
        ThrowIfInvalid(GraphRules.ValidateNewEdge(this, from, to) ?? GraphRules.ValidateWeight(weight));

        var edge = new Edge(_nextEdgeId, from, to, weight);
        var graph = new Graph(Vertices, Edges.Add(edge.Id, edge), _nextVertexId, _nextEdgeId + 1);
        return (graph, edge);
    }

    public Graph SetEdgeWeight(int id, double weight)
    {
        var edge = GetEdge(id);
        ThrowIfInvalid(GraphRules.ValidateWeight(weight));
        return new Graph(Vertices, Edges.SetItem(id, edge with { Weight = weight }), _nextVertexId, _nextEdgeId);
    }

    public Graph RemoveEdge(int id)
    {
        GetEdge(id);
        return new Graph(Vertices, Edges.Remove(id), _nextVertexId, _nextEdgeId);
    }

    private Graph WithVertex(Vertex vertex) =>
        new(Vertices.SetItem(vertex.Id, vertex), Edges, _nextVertexId, _nextEdgeId);

    private static string? ValidatePosition(double x, double y) =>
        double.IsFinite(x) && double.IsFinite(y) ? null : "Координаты вершины должны быть конечными числами.";

    private static void ThrowIfInvalid(string? error)
    {
        if (error is not null)
        {
            throw new GraphRuleException(error);
        }
    }
}
