using System.ComponentModel;

namespace AFK4.Operator.App.Shell;

public sealed class OperatorNavigationItemViewModel : INotifyPropertyChanged
{
    private bool isSelected;

    public OperatorNavigationItemViewModel(OperatorWorkspaceKind kind, string label, string requiredPermission)
    {
        Kind = kind;
        Label = label;
        RequiredPermission = requiredPermission;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public OperatorWorkspaceKind Kind { get; }

    public string Label { get; }

    public string RequiredPermission { get; }

    public bool IsSelected
    {
        get => isSelected;
        set
        {
            if (isSelected == value)
            {
                return;
            }

            isSelected = value;
            PropertyChanged?.Invoke(this, new PropertyChangedEventArgs(nameof(IsSelected)));
        }
    }

    public string Icon => Kind switch
    {
        OperatorWorkspaceKind.FloorMap => "⌂",
        OperatorWorkspaceKind.Pos => "T",
        OperatorWorkspaceKind.Players => "ID",
        OperatorWorkspaceKind.Shifts => "₸",
        OperatorWorkspaceKind.Settings => "⚙",
        _ => ""
    };

    public string ShortcutHint => Kind switch
    {
        OperatorWorkspaceKind.FloorMap => "F1",
        OperatorWorkspaceKind.Pos => "F3",
        OperatorWorkspaceKind.Players => "F4",
        OperatorWorkspaceKind.Shifts => "F8",
        OperatorWorkspaceKind.Settings => "F10",
        _ => ""
    };

    public string Description => Kind switch
    {
        OperatorWorkspaceKind.FloorMap => "Места и сессии",
        OperatorWorkspaceKind.Pos => "Продажи и оплаты",
        OperatorWorkspaceKind.Players => "Аккаунты и кошелек",
        OperatorWorkspaceKind.Shifts => "Кассовая смена",
        OperatorWorkspaceKind.Settings => "Операции филиала",
        _ => ""
    };
}
