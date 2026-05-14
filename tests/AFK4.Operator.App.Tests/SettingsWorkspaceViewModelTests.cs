using AFK4.Operator.App.Devices;
using AFK4.Operator.App.Settings;
using AFK4.Operator.App.Audit;
using AFK4.Operator.App.Updates;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.Operator.App.Tests;

public sealed class SettingsWorkspaceViewModelTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");

    [Fact]
    public void SettingsWorkspace_ExposesOnlyPermissionAllowedPanels()
    {
        var permissions = new HashSet<string>
        {
            StaffPermissionNames.ViewDeviceDetail,
            StaffPermissionNames.ManageInventoryStock,
            StaffPermissionNames.ManagePosCatalog
        };

        var viewModel = new SettingsWorkspaceViewModel(permissions);

        Assert.Contains(viewModel.Panels, panel => panel.Key == "connection");
        Assert.Contains(viewModel.Panels, panel => panel.Key == "devices");
        Assert.Contains(viewModel.Panels, panel => panel.Key == "pos-catalog");
        Assert.DoesNotContain(viewModel.Panels, panel => panel.Key == "roles");
    }

    [Fact]
    public void SettingsWorkspace_DoesNotExposeUpdatesWithoutUpdateStatusPermission()
    {
        var permissions = new HashSet<string>
        {
            StaffPermissionNames.ViewDeviceDetail,
            StaffPermissionNames.ManageInventoryStock,
            StaffPermissionNames.ManagePosCatalog,
            StaffPermissionNames.ManageTariffs,
            StaffPermissionNames.ManagePackages,
            StaffPermissionNames.ManageRoles,
            StaffPermissionNames.ViewAudit
        };

        var viewModel = new SettingsWorkspaceViewModel(permissions);

        Assert.DoesNotContain(viewModel.Panels, panel => panel.Key == "updates");
        Assert.DoesNotContain(viewModel.Panels, panel => panel.Key == "installers");
    }

    [Fact]
    public void SettingsWorkspace_WithUpdateStatusPermission_ExposesUpdatePanel()
    {
        var updateStatus = new UpdateStatusWorkspaceViewModel(new UnconfiguredOperatorUpdateApiClient());
        var viewModel = new SettingsWorkspaceViewModel(
            new HashSet<string> { StaffPermissionNames.ViewUpdateStatus },
            technicianTools: null,
            updateStatus);

        Assert.True(viewModel.HasUpdateStatus);
        Assert.Same(updateStatus, viewModel.UpdateStatus);
        Assert.Contains(viewModel.Panels, panel => panel.Key == "updates");
    }

    [Fact]
    public void SettingsWorkspace_WithUpdateManagementPermission_ExposesUpdatePanel()
    {
        var updateStatus = new UpdateStatusWorkspaceViewModel(new UnconfiguredOperatorUpdateApiClient());
        var viewModel = new SettingsWorkspaceViewModel(
            new HashSet<string> { StaffPermissionNames.ManageUpdatePackages },
            technicianTools: null,
            updateStatus);

        Assert.True(viewModel.HasUpdateStatus);
        Assert.Same(updateStatus, viewModel.UpdateStatus);
        Assert.Contains(viewModel.Panels, panel => panel.Key == "updates");
    }

    [Fact]
    public void SettingsWorkspace_WithAuditPermission_ExposesAuditPanel()
    {
        var auditSearch = new AuditSearchWorkspaceViewModel(new UnconfiguredOperatorAuditApiClient());
        var viewModel = new SettingsWorkspaceViewModel(
            new HashSet<string> { StaffPermissionNames.ViewAudit },
            technicianTools: null,
            updateStatus: null,
            auditSearch: auditSearch);

        Assert.True(viewModel.HasAuditSearch);
        Assert.Same(auditSearch, viewModel.AuditSearch);
        Assert.Contains(viewModel.Panels, panel => panel.Key == "audit");
    }

    [Fact]
    public void SettingsWorkspace_WithDeviceCredentialPermission_ExposesTechnicianTools()
    {
        var technicianTools = new TechnicianDeviceWorkflowViewModel(new UnconfiguredOperatorDeviceApiClient());
        var viewModel = new SettingsWorkspaceViewModel(
            new HashSet<string> { StaffPermissionNames.RotateDeviceCredential },
            technicianTools);

        Assert.True(viewModel.HasTechnicianTools);
        Assert.Same(technicianTools, viewModel.TechnicianTools);
        Assert.Contains(viewModel.Panels, panel => panel.Key == "devices");
    }

    [Fact]
    public void ApplyContext_UpdatesConnectionFieldsAndTechnicianDeviceContext()
    {
        var technicianTools = new TechnicianDeviceWorkflowViewModel(new UnconfiguredOperatorDeviceApiClient());
        var updateStatus = new UpdateStatusWorkspaceViewModel(new UnconfiguredOperatorUpdateApiClient());
        var auditSearch = new AuditSearchWorkspaceViewModel(new UnconfiguredOperatorAuditApiClient());
        var viewModel = new SettingsWorkspaceViewModel(
            new HashSet<string> { StaffPermissionNames.ViewDeviceDetail },
            technicianTools,
            updateStatus,
            auditSearch);

        viewModel.ApplyContext(OrganizationId, BranchId);

        Assert.Equal(OrganizationId.ToString("D"), viewModel.OrganizationIdText);
        Assert.Equal(BranchId.ToString("D"), viewModel.BranchIdText);
        Assert.Equal(OrganizationId.ToString("D"), technicianTools.OrganizationIdText);
        Assert.Equal(BranchId.ToString("D"), technicianTools.BranchIdText);
        Assert.Equal(OrganizationId.ToString("D"), updateStatus.OrganizationIdText);
        Assert.Equal(BranchId.ToString("D"), updateStatus.BranchIdText);
        Assert.Equal(OrganizationId.ToString("D"), auditSearch.OrganizationIdText);
        Assert.Equal(BranchId.ToString("D"), auditSearch.BranchIdText);
    }
}
