using AFK4.Operator.App.FloorMap;

namespace AFK4.Operator.App.Tests;

public sealed class MainWindowViewModelTests
{
    [Fact]
    public void Constructor_CreatesInitialSeatCards()
    {
        var viewModel = new MainWindowViewModel();

        Assert.Equal("AFK4.NET Operator", viewModel.Title);
        Assert.Contains(viewModel.Seats, seat => seat.Name == "PC-001" && seat.State == "Free");
        Assert.Contains(viewModel.Seats, seat => seat.Name == "PC-002" && seat.State == "Locked");
    }
}
