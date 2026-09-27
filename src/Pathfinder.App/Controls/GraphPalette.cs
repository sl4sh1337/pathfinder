using System.Windows.Media;

namespace Pathfinder.App.Controls;

/// <summary>
/// Colours of the "drafting paper" look: warm paper, dark ink, and a small set of
/// semantic accents, each reserved for one meaning during the animation.
/// </summary>
internal static class GraphPalette
{
    public static readonly Brush Paper = Frozen("#F4F1EA");
    public static readonly Brush GridDot = Frozen("#D6D0C2");
    public static readonly Brush Ink = Frozen("#1F2430");
    public static readonly Brush InkMuted = Frozen("#8A8577");
    public static readonly Brush VertexFill = Frozen("#FFFDF8");
    public static readonly Brush LabelBackground = Frozen("#F4F1EA");
    public static readonly Brush BadgeText = Frozen("#FFFFFF");

    /// <summary>Source vertex and the growing shortest-path tree.</summary>
    public static readonly Brush Tree = Frozen("#0F766E");
    public static readonly Brush TreeLight = Frozen("#CCEAE6");

    /// <summary>The vertex / edge being examined right now.</summary>
    public static readonly Brush Active = Frozen("#D97706");
    public static readonly Brush ActiveLight = Frozen("#FCE7C4");

    /// <summary>A relaxation that improved a distance.</summary>
    public static readonly Brush Improved = Frozen("#15803D");

    /// <summary>A relaxation that did not help.</summary>
    public static readonly Brush Rejected = Frozen("#B91C1C");

    /// <summary>Dijkstra: vertices whose distance is final.</summary>
    public static readonly Brush Finalized = Frozen("#DCE4EE");

    /// <summary>The shortest path under the cursor after the run.</summary>
    public static readonly Brush Path = Frozen("#E11D48");
    public static readonly Brush PathLight = Frozen("#FDE2E8");

    /// <summary>Vertices behind a negative cycle.</summary>
    public static readonly Brush NegativeCycle = Frozen("#7C3AED");
    public static readonly Brush NegativeCycleLight = Frozen("#EDE4FE");

    public static readonly Brush Selection = Frozen("#330F766E");

    private static Brush Frozen(string hex)
    {
        var brush = new SolidColorBrush((Color)ColorConverter.ConvertFromString(hex));
        brush.Freeze();
        return brush;
    }
}
