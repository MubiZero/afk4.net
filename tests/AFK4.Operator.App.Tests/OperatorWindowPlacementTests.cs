using AFK4.Operator.App.Web;

namespace AFK4.Operator.App.Tests;

public sealed class OperatorWindowPlacementTests
{
    private const double ScreenLeft = 0;
    private const double ScreenTop = 0;
    private const double ScreenWidth = 1920;
    private const double ScreenHeight = 1080;
    private const double MinWidth = 960;
    private const double MinHeight = 600;

    private static OperatorWindowPlacement? Sanitize(OperatorWindowPlacement placement) =>
        placement.Sanitize(ScreenLeft, ScreenTop, ScreenWidth, ScreenHeight, MinWidth, MinHeight);

    [Fact]
    public void Sanitize_KeepsAFullyVisibleWindowUnchanged()
    {
        var placement = new OperatorWindowPlacement { Left = 100, Top = 80, Width = 1200, Height = 800 };

        var result = Sanitize(placement);

        Assert.NotNull(result);
        Assert.Equal(100, result!.Left);
        Assert.Equal(80, result.Top);
        Assert.Equal(1200, result.Width);
        Assert.Equal(800, result.Height);
    }

    [Fact]
    public void Sanitize_GrowsSizeBelowMinimumUpToTheMinimum()
    {
        var placement = new OperatorWindowPlacement { Left = 50, Top = 50, Width = 400, Height = 300 };

        var result = Sanitize(placement);

        Assert.NotNull(result);
        Assert.Equal(MinWidth, result!.Width);
        Assert.Equal(MinHeight, result.Height);
    }

    [Fact]
    public void Sanitize_PullsAnOffScreenWindowBackIntoView()
    {
        var placement = new OperatorWindowPlacement { Left = 5000, Top = 4000, Width = 1200, Height = 800 };

        var result = Sanitize(placement);

        Assert.NotNull(result);
        // A graspable strip must remain on the virtual screen (96px sentinel).
        Assert.True(result!.Left <= ScreenWidth - 96);
        Assert.True(result.Top <= ScreenHeight - 96);
        Assert.True(result.Left + result.Width > ScreenLeft);
    }

    [Fact]
    public void Sanitize_PreservesMaximizedFlag()
    {
        var placement = new OperatorWindowPlacement { Left = 100, Top = 80, Width = 1200, Height = 800, Maximized = true };

        var result = Sanitize(placement);

        Assert.NotNull(result);
        Assert.True(result!.Maximized);
    }

    [Fact]
    public void Sanitize_RejectsCorruptGeometry()
    {
        var placement = new OperatorWindowPlacement { Left = double.NaN, Top = 0, Width = 1200, Height = 800 };

        Assert.Null(Sanitize(placement));
    }

    [Fact]
    public void Sanitize_RejectsAnEmptyVirtualScreen()
    {
        var placement = new OperatorWindowPlacement { Left = 0, Top = 0, Width = 1200, Height = 800 };

        Assert.Null(placement.Sanitize(0, 0, 0, 0, MinWidth, MinHeight));
    }
}
