using Pathfinder.Core.Model;

namespace Pathfinder.Core.Tests;

internal static class TestGraphs
{
    /// <summary>Builds a graph from "A B 3" style triples; vertices are created on first mention.</summary>
    public static Graph Build(params (string From, string To, double Weight)[] edges)
    {
        var graph = Graph.Empty;
        foreach (var (from, to, weight) in edges)
        {
            graph = EnsureVertex(graph, from);
            graph = EnsureVertex(graph, to);
            graph = graph.AddEdge(Id(graph, from), Id(graph, to), weight).Graph;
        }

        return graph;
    }

    public static Graph EnsureVertex(Graph graph, string name) =>
        graph.FindVertex(name) is null ? graph.AddVertex(name, 0, 0).Graph : graph;

    public static int Id(Graph graph, string name) => graph.FindVertex(name)!.Id;

    /// <summary>Random directed graph without self-loops or parallel edges.</summary>
    public static Graph RandomGraph(Random random, int vertexCount, double edgeProbability, int minWeight, int maxWeight)
    {
        var graph = Graph.Empty;
        for (var i = 0; i < vertexCount; i++)
        {
            graph = graph.AddVertex($"v{i}", i, i).Graph;
        }

        var ids = graph.Vertices.Keys.ToArray();
        foreach (var from in ids)
        {
            foreach (var to in ids.Where(to => to != from && random.NextDouble() < edgeProbability))
            {
                graph = graph.AddEdge(from, to, random.Next(minWeight, maxWeight + 1)).Graph;
            }
        }

        return graph;
    }

    /// <summary>
    /// Floyd–Warshall reference: exact distances from <paramref name="sourceId"/>,
    /// −∞ for vertices reachable through a negative cycle.
    /// </summary>
    public static Dictionary<int, double> ReferenceDistances(Graph graph, int sourceId)
    {
        var ids = graph.Vertices.Keys.ToArray();
        var index = ids.Select((id, i) => (id, i)).ToDictionary(p => p.id, p => p.i);
        var n = ids.Length;
        var d = new double[n, n];
        for (var i = 0; i < n; i++)
        {
            for (var j = 0; j < n; j++)
            {
                d[i, j] = i == j ? 0 : double.PositiveInfinity;
            }
        }

        foreach (var e in graph.Edges.Values)
        {
            d[index[e.From], index[e.To]] = Math.Min(d[index[e.From], index[e.To]], e.Weight);
        }

        for (var k = 0; k < n; k++)
        {
            for (var i = 0; i < n; i++)
            {
                for (var j = 0; j < n; j++)
                {
                    if (d[i, k] + d[k, j] < d[i, j])
                    {
                        d[i, j] = d[i, k] + d[k, j];
                    }
                }
            }
        }

        var s = index[sourceId];
        var result = new Dictionary<int, double>();
        for (var j = 0; j < n; j++)
        {
            // j is −∞ if some vertex k on a negative cycle is reachable from s and reaches j.
            var hitsNegativeCycle = Enumerable.Range(0, n)
                .Any(k => d[k, k] < 0 && double.IsFinite(d[s, k]) && double.IsFinite(d[k, j]));
            result[ids[j]] = hitsNegativeCycle ? double.NegativeInfinity : d[s, j];
        }

        return result;
    }
}
