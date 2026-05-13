namespace AFK4.Operator.App.Shell;

public sealed class OperatorNavigationItemViewModel
{
    public OperatorNavigationItemViewModel(OperatorWorkspaceKind kind, string label, string requiredPermission)
    {
        Kind = kind;
        Label = label;
        RequiredPermission = requiredPermission;
    }

    public OperatorWorkspaceKind Kind { get; }

    public string Label { get; }

    public string RequiredPermission { get; }
}
