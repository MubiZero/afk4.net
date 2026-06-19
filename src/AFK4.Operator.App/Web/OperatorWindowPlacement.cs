namespace AFK4.Operator.App.Web;

// Persisted window geometry: the normal-state bounds plus whether the window was maximized.
// Maximized is stored alongside the restore bounds so re-opening a maximized window restores both
// the full-screen state AND the size to fall back to when the user un-maximizes.
public sealed record OperatorWindowPlacement
{
    public double Left { get; init; }

    public double Top { get; init; }

    public double Width { get; init; }

    public double Height { get; init; }

    public bool Maximized { get; init; }

    // Returns a placement safe to apply, or null to fall back to the default centered window.
    // Guards the two ways a saved geometry goes stale: (1) below the window minimum, (2) off-screen
    // after a monitor was unplugged or resolution changed. Width/height are bumped to the minimum;
    // the position is clamped so a graspable strip of the window (incl. the drag area) stays on a
    // monitor. Non-finite values (corrupt file) yield null.
    public OperatorWindowPlacement? Sanitize(
        double virtualScreenLeft,
        double virtualScreenTop,
        double virtualScreenWidth,
        double virtualScreenHeight,
        double minWidth,
        double minHeight)
    {
        if (!IsFinite(Left) || !IsFinite(Top) || !IsFinite(Width) || !IsFinite(Height))
        {
            return null;
        }

        if (virtualScreenWidth <= 0 || virtualScreenHeight <= 0)
        {
            return null;
        }

        var width = Math.Max(Width, minWidth);
        var height = Math.Max(Height, minHeight);
        if (width <= 0 || height <= 0)
        {
            return null;
        }

        // Keep at least this much of the window on a monitor so the user can always grab and move it.
        const double MinVisible = 96;
        var screenRight = virtualScreenLeft + virtualScreenWidth;
        var screenBottom = virtualScreenTop + virtualScreenHeight;
        var left = Clamp(Left, virtualScreenLeft - width + MinVisible, screenRight - MinVisible);
        var top = Clamp(Top, virtualScreenTop, screenBottom - MinVisible);

        return this with { Left = left, Top = top, Width = width, Height = height };
    }

    private static double Clamp(double value, double min, double max)
    {
        if (max < min)
        {
            return min;
        }

        return Math.Min(Math.Max(value, min), max);
    }

    private static bool IsFinite(double value) => !double.IsNaN(value) && !double.IsInfinity(value);
}
