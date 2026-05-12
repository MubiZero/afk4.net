using System.ComponentModel;
using System.Runtime.CompilerServices;

namespace AFK4.Operator.App.FloorMap;

public sealed class FloorMapSeatViewModel : INotifyPropertyChanged
{
    private string state;
    private bool isOnline = true;

    public FloorMapSeatViewModel(string name, string zone, string state)
    {
        Name = name;
        Zone = zone;
        this.state = state;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public string Name { get; }

    public string Zone { get; }

    public string State
    {
        get => state;
        private set => SetField(ref state, value);
    }

    public bool IsOnline
    {
        get => isOnline;
        private set => SetField(ref isOnline, value);
    }

    public void ApplyDeviceState(bool isOnline, bool isLocked)
    {
        IsOnline = isOnline;
        State = isOnline
            ? isLocked ? "Locked" : "Free"
            : "Offline";
    }

    private void SetField<T>(ref T field, T value, [CallerMemberName] string? propertyName = null)
    {
        if (EqualityComparer<T>.Default.Equals(field, value))
        {
            return;
        }

        field = value;
        PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(propertyName));
    }
}
