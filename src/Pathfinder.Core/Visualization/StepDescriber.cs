using System.Globalization;
using Pathfinder.Core.Algorithms;
using Pathfinder.Core.Model;

namespace Pathfinder.Core.Visualization;

/// <summary>Human-readable (Russian) narration of algorithm steps for the execution log.</summary>
public static class StepDescriber
{
    public const string Infinity = "∞";

    private static readonly CultureInfo Culture = CultureInfo.GetCultureInfo("ru-RU");

    public static string FormatDistance(double distance) => distance switch
    {
        double.PositiveInfinity => Infinity,
        double.NegativeInfinity => "−" + Infinity,
        _ => FormatNumber(distance),
    };

    public static string FormatNumber(double value) => value.ToString("0.###", Culture);

    public static string Describe(AlgorithmStep step, Graph graph)
    {
        string Name(int id) => graph.Vertices.TryGetValue(id, out var v) ? v.Name : $"#{id}";

        return step switch
        {
            InitializationStep s =>
                $"Инициализация: d({Name(s.SourceId)}) = 0, остальные вершины = {Infinity}.",
            PassStartedStep s =>
                $"Проход {s.Pass} из {s.MaxPasses} по всем рёбрам.",
            VertexSelectedStep s =>
                $"Выбрана вершина {Name(s.VertexId)} с наименьшей меткой {FormatDistance(s.Distance)}.",
            EdgeCheckedStep s => DescribeEdgeCheck(s, Name(s.FromId), Name(s.ToId)),
            VertexFinalizedStep s =>
                $"Вершина {Name(s.VertexId)} обработана, её расстояние окончательно.",
            EarlyTerminationStep s =>
                $"За проход {s.Pass} ни одна метка не изменилась — алгоритм завершается досрочно.",
            NegativeCycleStep s =>
                $"Обнаружен цикл отрицательного веса! Для вершин {string.Join(", ", s.AffectedVertexIds.Select(Name))} кратчайшего пути не существует (−{Infinity}).",
            TraceTruncatedStep s =>
                $"Трасса превысила {s.Limit:N0} шагов: дальнейшие шаги не показываются, итоговый результат будет отображён по завершении.",
            CompletedStep s => DescribeCompletion(s.Result),
            _ => step.ToString(),
        };
    }

    private static string DescribeEdgeCheck(EdgeCheckedStep s, string from, string to)
    {
        var comparison =
            $"Ребро {from} → {to}: {FormatDistance(s.CandidateDistance)} {(s.Improved ? "<" : "≥")} {FormatDistance(s.OldDistance)}";
        return s.Improved
            ? $"{comparison}, метка {to} уменьшена до {FormatDistance(s.CandidateDistance)}."
            : $"{comparison}, метка не меняется.";
    }

    private static string DescribeCompletion(ShortestPathResult result)
    {
        var reached = result.Distances.Values.Count(double.IsFinite);
        return result.HasNegativeCycle
            ? $"Готово. Достижимо вершин с конечным расстоянием: {reached}; найден цикл отрицательного веса."
            : $"Готово. Достижимо вершин: {reached} из {result.Distances.Count}. Наведите курсор на вершину, чтобы увидеть путь.";
    }
}
