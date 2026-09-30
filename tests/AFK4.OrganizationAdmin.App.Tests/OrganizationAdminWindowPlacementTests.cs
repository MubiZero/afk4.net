using AFK4.OrganizationAdmin.Web;

namespace AFK4.OrganizationAdmin.App.Tests;

public sealed class OrganizationAdminWindowPlacementTests
{
    private const double ScreenLeft = 0;
    private const double ScreenTop = 0;
    private const double ScreenWidth = 1920;
    private const double ScreenHeight = 1080;
    private const double MinWidth = 960;
    private const double MinHeight = 600;

    private static OrganizationAdminWindowPlacement? Sanitize(OrganizationAdminWindowPlacement placement) =>
        placement.Sanitize(ScreenLeft, ScreenTop, ScreenWidth, ScreenHeight, MinWidth, MinHeight);

    /// <summary>
    /// Приёмка 30.09.2026: экран 1280×800, рабочая область 752 по высоте. Окно 1152×840 по центру
    /// уходило шапкой за верхний край, а рамки Windows у него нет — ни перетащить, ни закрыть.
    /// </summary>
    [Theory]
    [InlineData(1280, 752)]
    [InlineData(1366, 720)]
    public void FirstRun_OnASmallScreen_KeepsTheWholeWindowInsideTheWorkArea(double workWidth, double workHeight)
    {
        var placement = OrganizationAdminWindowPlacement.FirstRun(0, 0, workWidth, workHeight, 1152, 840);

        Assert.True(placement.Top >= 0);
        Assert.True(placement.Left >= 0);
        Assert.True(placement.Top + placement.Height <= workHeight);
        Assert.True(placement.Left + placement.Width <= workWidth);
    }

    [Fact]
    public void FirstRun_OnALargeScreen_KeepsTheDefaultSizeCentred()
    {
        var placement = OrganizationAdminWindowPlacement.FirstRun(0, 0, 1920, 1032, 1152, 840);

        Assert.Equal(1152, placement.Width);
        Assert.Equal(840, placement.Height);
        Assert.Equal((1920 - 1152) / 2.0, placement.Left);
        Assert.Equal((1032 - 840) / 2.0, placement.Top);
    }

    [Fact]
    public void Sanitize_KeepsAFullyVisibleWindowUnchanged()
    {
        var placement = new OrganizationAdminWindowPlacement { Left = 100, Top = 80, Width = 1200, Height = 800 };

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
        var placement = new OrganizationAdminWindowPlacement { Left = 50, Top = 50, Width = 400, Height = 300 };

        var result = Sanitize(placement);

        Assert.NotNull(result);
        Assert.Equal(MinWidth, result!.Width);
        Assert.Equal(MinHeight, result.Height);
    }

    [Fact]
    public void Sanitize_PullsAnOffScreenWindowBackIntoView()
    {
        var placement = new OrganizationAdminWindowPlacement { Left = 5000, Top = 4000, Width = 1200, Height = 800 };

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
        var placement = new OrganizationAdminWindowPlacement { Left = 100, Top = 80, Width = 1200, Height = 800, Maximized = true };

        var result = Sanitize(placement);

        Assert.NotNull(result);
        Assert.True(result!.Maximized);
    }

    [Fact]
    public void Sanitize_RejectsCorruptGeometry()
    {
        var placement = new OrganizationAdminWindowPlacement { Left = double.NaN, Top = 0, Width = 1200, Height = 800 };

        Assert.Null(Sanitize(placement));
    }

    [Fact]
    public void Sanitize_RejectsAnEmptyVirtualScreen()
    {
        var placement = new OrganizationAdminWindowPlacement { Left = 0, Top = 0, Width = 1200, Height = 800 };

        Assert.Null(placement.Sanitize(0, 0, 0, 0, MinWidth, MinHeight));
    }
}
