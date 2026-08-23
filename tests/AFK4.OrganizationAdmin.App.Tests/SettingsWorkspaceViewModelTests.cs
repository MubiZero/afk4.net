using AFK4.OrganizationAdmin.App.Devices;
using AFK4.OrganizationAdmin.App.Settings;
using AFK4.OrganizationAdmin.App.Audit;
using AFK4.OrganizationAdmin.App.Diagnostics;
using AFK4.OrganizationAdmin.App.Updates;
using AFK4.Shared.Contracts.Identity;

namespace AFK4.OrganizationAdmin.App.Tests;

public sealed class SettingsWorkspaceViewModelTests
{
    private static readonly Guid OrganizationId = Guid.Parse("0c04d6c0-bfa8-4e26-9263-fc0d307d0f08");
    private static readonly Guid BranchId = Guid.Parse("acfc0212-967f-4d84-94be-9003387b09c2");

    [Fact]
    public void SettingsWorkspace_ExposesOnlyPermissionAllowedPanels()
    {
        var permissions = new HashSet<string>
        {
            OrganizationPermissionNames.ViewDeviceDetail,
            OrganizationPermissionNames.ManageInventoryStock,
            OrganizationPermissionNames.ManagePosCatalog
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
            OrganizationPermissionNames.ViewDeviceDetail,
            OrganizationPermissionNames.ManageInventoryStock,
            OrganizationPermissionNames.ManagePosCatalog,
            OrganizationPermissionNames.ManageTariffs,
            OrganizationPermissionNames.ManagePackages,
            OrganizationPermissionNames.ManageRoles,
            OrganizationPermissionNames.ViewAudit
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
            new HashSet<string> { OrganizationPermissionNames.ViewUpdateStatus },
            technicianTools: null,
            updateStatus);

        Assert.True(viewModel.HasUpdateStatus);
        Assert.Same(updateStatus, viewModel.UpdateStatus);
        Assert.Contains(viewModel.Panels, panel => panel.Key == "updates");
    }

    [Fact]
    public void SettingsWorkspace_WithRemovedUpdateManagementPermission_DoesNotExposeUpdatePanel()
    {
        var updateStatus = new UpdateStatusWorkspaceViewModel(new UnconfiguredOperatorUpdateApiClient());
        var viewModel = new SettingsWorkspaceViewModel(
            new HashSet<string> { "organization.updates.packages.manage" },
            technicianTools: null,
            updateStatus);

        Assert.False(viewModel.HasUpdateStatus);
        Assert.DoesNotContain(viewModel.Panels, panel => panel.Key == "updates");
    }

    [Fact]
    public void SettingsWorkspace_WithAuditPermission_ExposesAuditPanel()
    {
        var auditSearch = new AuditSearchWorkspaceViewModel(new UnconfiguredOperatorAuditApiClient());
        var viewModel = new SettingsWorkspaceViewModel(
            new HashSet<string> { OrganizationPermissionNames.ViewAudit },
            technicianTools: null,
            updateStatus: null,
            auditSearch: auditSearch);

        Assert.True(viewModel.HasAuditSearch);
        Assert.Same(auditSearch, viewModel.AuditSearch);
        Assert.Contains(viewModel.Panels, panel => panel.Key == "audit");
    }

    [Fact]
    public void SettingsWorkspace_WithDiagnosticsPermission_ExposesDiagnosticsPanel()
    {
        var diagnostics = new DiagnosticsWorkspaceViewModel(new UnconfiguredOperatorDiagnosticsApiClient());
        var viewModel = new SettingsWorkspaceViewModel(
            new HashSet<string> { OrganizationPermissionNames.ViewDiagnostics },
            technicianTools: null,
            updateStatus: null,
            auditSearch: null,
            diagnostics: diagnostics);

        Assert.True(viewModel.HasDiagnostics);
        Assert.Same(diagnostics, viewModel.Diagnostics);
        Assert.Contains(viewModel.Panels, panel => panel.Key == "diagnostics");
    }

    [Fact]
    public void SettingsWorkspace_WithDeviceCredentialPermission_ExposesTechnicianTools()
    {
        var technicianTools = new TechnicianDeviceWorkflowViewModel(new UnconfiguredOperatorDeviceApiClient());
        var viewModel = new SettingsWorkspaceViewModel(
            new HashSet<string> { OrganizationPermissionNames.RotateDeviceCredential },
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
        var diagnostics = new DiagnosticsWorkspaceViewModel(new UnconfiguredOperatorDiagnosticsApiClient());
        var viewModel = new SettingsWorkspaceViewModel(
            new HashSet<string> { OrganizationPermissionNames.ViewDeviceDetail },
            technicianTools,
            updateStatus,
            auditSearch,
            diagnostics);

        viewModel.ApplyContext(OrganizationId, BranchId);

        Assert.Equal(OrganizationId.ToString("D"), viewModel.OrganizationIdText);
        Assert.Equal(BranchId.ToString("D"), viewModel.BranchIdText);
        Assert.Equal(OrganizationId.ToString("D"), technicianTools.OrganizationIdText);
        Assert.Equal(BranchId.ToString("D"), technicianTools.BranchIdText);
        Assert.Equal(OrganizationId.ToString("D"), updateStatus.OrganizationIdText);
        Assert.Equal(BranchId.ToString("D"), updateStatus.BranchIdText);
        Assert.Equal(OrganizationId.ToString("D"), auditSearch.OrganizationIdText);
        Assert.Equal(BranchId.ToString("D"), auditSearch.BranchIdText);
        Assert.Equal(OrganizationId.ToString("D"), diagnostics.OrganizationIdText);
        Assert.Equal(BranchId.ToString("D"), diagnostics.BranchIdText);
    }
}
