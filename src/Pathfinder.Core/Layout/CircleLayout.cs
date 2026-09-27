namespace Pathfinder.Core.Layout;

/// <summary>Places vertices evenly on a circle whose size grows with the vertex count.</summary>
public static class CircleLayout
{
    public const double MinRadius = 160;
    public const double SpacingAlongCircle = 90;

    public static IReadOnlyList<(double X, double Y)> Positions(int count, double centerX = 0, double centerY = 0)
    {
        ArgumentOutOfRangeException.ThrowIfNegative(count);
        if (count == 1)
        {
            return [(centerX, centerY)];
        }

        var radius = Math.Max(MinRadius, count * SpacingAlongCircle / (2 * Math.PI));
        return Enumerable.Range(0, count)
            .Select(i =>
            {
                // Start at the top and go clockwise, as people usually read a drawn cycle.
                var angle = -Math.PI / 2 + 2 * Math.PI * i / count;
                return (centerX + radius * Math.Cos(angle), centerY + radius * Math.Sin(angle));
            })
            .ToList();
    }
}
