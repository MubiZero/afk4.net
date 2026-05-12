namespace AFK4.Operator.App.FloorMap;

public sealed class FloorMapSeatViewModel
{
    public FloorMapSeatViewModel(string name, string zone, string state)
    {
        Name = name;
        Zone = zone;
        State = state;
    }

    public string Name { get; }

    public string Zone { get; }

    public string State { get; }
}
