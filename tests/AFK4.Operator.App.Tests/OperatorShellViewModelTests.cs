using AFK4.Operator.App.Shell;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.Operator.App.Tests;

public sealed class OperatorShellViewModelTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");
    private static readonly Guid StaffUserId = Guid.Parse("3db1367b-88c6-4b1c-99c3-bcbb5f4d5134");

    [Fact]
    public void SignIn_WithCashierPermissions_ShowsOperationalWorkspacesOnly()
    {
        var context = CreateContext(
            StaffPermissionNames.ViewFloorMap,
            StaffPermissionNames.StartSession,
            StaffPermissionNames.CreatePosSale,
            StaffPermissionNames.PayPosSale,
            StaffPermissionNames.OpenShift,
            StaffPermissionNames.ViewShift);
        var shell = new OperatorShellViewModel();

        shell.ApplySignedInContext(context);

        Assert.True(shell.IsSignedIn);
        Assert.Contains(shell.NavigationItems, item => item.Kind == OperatorWorkspaceKind.FloorMap);
        Assert.Contains(shell.NavigationItems, item => item.Kind == OperatorWorkspaceKind.Pos);
        Assert.Contains(shell.NavigationItems, item => item.Kind == OperatorWorkspaceKind.Shifts);
        Assert.DoesNotContain(shell.NavigationItems, item => item.Kind == OperatorWorkspaceKind.Settings);
        Assert.Equal(OperatorWorkspaceKind.FloorMap, shell.SelectedWorkspace);
    }

    [Fact]
    public void ApplySignedInContext_WithDevicePermissions_ShowsSettings()
    {
        var context = CreateContext(
            StaffPermissionNames.ViewFloorMap,
            StaffPermissionNames.ViewDeviceDetail,
            StaffPermissionNames.RotateDeviceCredential);
        var shell = new OperatorShellViewModel();

        shell.ApplySignedInContext(context);

        Assert.Contains(shell.NavigationItems, item => item.Kind == OperatorWorkspaceKind.Settings);
    }

    [Fact]
    public void NavigateCommand_ChangesSelectedWorkspaceOnlyWhenItemIsAllowed()
    {
        var context = CreateContext(
            StaffPermissionNames.ViewFloorMap,
            StaffPermissionNames.CreatePosSale);
        var shell = new OperatorShellViewModel();
        shell.ApplySignedInContext(context);

        shell.NavigateCommand.Execute(OperatorWorkspaceKind.Pos);
        shell.NavigateCommand.Execute(OperatorWorkspaceKind.Settings);

        Assert.Equal(OperatorWorkspaceKind.Pos, shell.SelectedWorkspace);
    }

    [Fact]
    public void SignOutCommand_ClearsUserAndNavigation()
    {
        var shell = new OperatorShellViewModel();
        shell.ApplySignedInContext(CreateContext(StaffPermissionNames.ViewFloorMap));

        shell.SignOutCommand.Execute(null);

        Assert.False(shell.IsSignedIn);
        Assert.Null(shell.CurrentUser);
        Assert.Empty(shell.NavigationItems);
        Assert.Null(shell.SelectedWorkspace);
    }

    private static OperatorUserContext CreateContext(params string[] permissions)
    {
        return new OperatorUserContext(
            StaffUserId,
            OrganizationId,
            BranchId,
            "Cashier One",
            permissions.ToHashSet(StringComparer.OrdinalIgnoreCase));
    }
}
