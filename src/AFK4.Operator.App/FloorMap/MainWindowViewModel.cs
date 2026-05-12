using System.Collections.ObjectModel;

namespace AFK4.Operator.App.FloorMap;

public sealed class MainWindowViewModel
{
    public MainWindowViewModel()
    {
        Seats =
        [
            new FloorMapSeatViewModel("PC-001", "Main Hall", "Free"),
            new FloorMapSeatViewModel("PC-002", "Main Hall", "Locked")
        ];
    }

    public string Title => "AFK4 Operator";

    public ObservableCollection<FloorMapSeatViewModel> Seats { get; }
}
