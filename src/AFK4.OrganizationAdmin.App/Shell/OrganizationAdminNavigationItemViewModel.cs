using System.ComponentModel;

namespace AFK4.OrganizationAdmin.App.Shell;

public sealed class OrganizationAdminNavigationItemViewModel : INotifyPropertyChanged
{
    private bool isSelected;

    public OrganizationAdminNavigationItemViewModel(OrganizationAdminWorkspaceKind kind, string label, string requiredPermission)
    {
        Kind = kind;
        Label = label;
        RequiredPermission = requiredPermission;
    }

    public event PropertyChangedEventHandler? PropertyChanged;

    public OrganizationAdminWorkspaceKind Kind { get; }

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
        OrganizationAdminWorkspaceKind.FloorMap => "⌂",
        OrganizationAdminWorkspaceKind.Pos => "T",
        OrganizationAdminWorkspaceKind.Players => "ID",
        OrganizationAdminWorkspaceKind.Shifts => "₸",
        OrganizationAdminWorkspaceKind.Settings => "⚙",
        _ => ""
    };

    public string ShortcutHint => Kind switch
    {
        OrganizationAdminWorkspaceKind.FloorMap => "F1",
        OrganizationAdminWorkspaceKind.Pos => "F3",
        OrganizationAdminWorkspaceKind.Players => "F4",
        OrganizationAdminWorkspaceKind.Shifts => "F8",
        OrganizationAdminWorkspaceKind.Settings => "F10",
        _ => ""
    };

    public string Description => Kind switch
    {
        OrganizationAdminWorkspaceKind.FloorMap => "Места и сессии",
        OrganizationAdminWorkspaceKind.Pos => "Продажи и оплаты",
        OrganizationAdminWorkspaceKind.Players => "Аккаунты и кошелек",
        OrganizationAdminWorkspaceKind.Shifts => "Кассовая смена",
        OrganizationAdminWorkspaceKind.Settings => "Операции филиала",
        _ => ""
    };
}
