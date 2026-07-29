using AFK4.Shared.Contracts.Identity;

namespace AFK4.Platform.Api.Identity;

public static class OrganizationPermissionCatalog
{
    private static readonly IReadOnlyDictionary<string, IReadOnlySet<string>> RolePermissions =
        new Dictionary<string, IReadOnlySet<string>>(StringComparer.OrdinalIgnoreCase)
        {
            [OrganizationRoleNames.OrganizationOwner] = new HashSet<string>
            {
                OrganizationPermissionNames.CreateDeviceEnrollmentCode,
                OrganizationPermissionNames.DispatchDeviceCommand,
                OrganizationPermissionNames.ViewDeviceCommandStatus,
                OrganizationPermissionNames.RotateDeviceCredential,
                OrganizationPermissionNames.RevokeDeviceCredential,
                OrganizationPermissionNames.AssignDeviceSeat,
                OrganizationPermissionNames.ViewDeviceDetail,
                OrganizationPermissionNames.InstallDevice,
                OrganizationPermissionNames.ViewFloorMap,
                OrganizationPermissionNames.ManageLayout,
                OrganizationPermissionNames.StartSession,
                OrganizationPermissionNames.ExtendSession,
                OrganizationPermissionNames.TransferSession,
                OrganizationPermissionNames.EndSession,
                OrganizationPermissionNames.ViewSession,
                OrganizationPermissionNames.CreatePlayerAccount,
                OrganizationPermissionNames.ViewPlayers,
                OrganizationPermissionNames.ViewBilling,
                OrganizationPermissionNames.TopUpWallet,
                OrganizationPermissionNames.RefundLedgerEntry,
                OrganizationPermissionNames.ManualLedgerCorrection,
                OrganizationPermissionNames.PayDebt,
                OrganizationPermissionNames.ApproveMoneyAction,
                OrganizationPermissionNames.ViewSubscription,
                OrganizationPermissionNames.ManageTariffs,
                OrganizationPermissionNames.ViewTariffs,
                OrganizationPermissionNames.ManagePackages,
                OrganizationPermissionNames.ViewPackages,
                OrganizationPermissionNames.PurchasePackage,
                OrganizationPermissionNames.OpenShift,
                OrganizationPermissionNames.CloseShift,
                OrganizationPermissionNames.ViewShift,
                OrganizationPermissionNames.ManageShiftCash,
                OrganizationPermissionNames.ViewReports,
                OrganizationPermissionNames.ViewReservations,
                OrganizationPermissionNames.ManageReservations,
                OrganizationPermissionNames.ManagePosCatalog,
                OrganizationPermissionNames.ManageShopOrders,
                OrganizationPermissionNames.CreatePosSale,
                OrganizationPermissionNames.PayPosSale,
                OrganizationPermissionNames.RefundPosSale,
                OrganizationPermissionNames.VoidPosSale,
                OrganizationPermissionNames.ManageInventoryStock,
                OrganizationPermissionNames.ViewInventory,
                OrganizationPermissionNames.ViewReceipt,
                OrganizationPermissionNames.ViewUpdateStatus,
                OrganizationPermissionNames.ViewDiagnostics,
                OrganizationPermissionNames.ManageBranchStaff,
                OrganizationPermissionNames.ManageRoles,
                OrganizationPermissionNames.ViewAudit,
                OrganizationPermissionNames.ViewOrganizationAudit,
                OrganizationPermissionNames.ManageBranchSettings,
                OrganizationPermissionNames.ViewBranches,
                OrganizationPermissionNames.ManagePaymentGateways,
                OrganizationPermissionNames.ManageLoyaltySettings,
                OrganizationPermissionNames.ManageNews
            },
            [OrganizationRoleNames.BranchManager] = new HashSet<string>
            {
                OrganizationPermissionNames.CreateDeviceEnrollmentCode,
                OrganizationPermissionNames.DispatchDeviceCommand,
                OrganizationPermissionNames.ViewDeviceCommandStatus,
                OrganizationPermissionNames.RotateDeviceCredential,
                OrganizationPermissionNames.RevokeDeviceCredential,
                OrganizationPermissionNames.AssignDeviceSeat,
                OrganizationPermissionNames.ViewDeviceDetail,
                OrganizationPermissionNames.InstallDevice,
                OrganizationPermissionNames.ViewFloorMap,
                OrganizationPermissionNames.ManageLayout,
                OrganizationPermissionNames.StartSession,
                OrganizationPermissionNames.ExtendSession,
                OrganizationPermissionNames.TransferSession,
                OrganizationPermissionNames.EndSession,
                OrganizationPermissionNames.ViewSession,
                OrganizationPermissionNames.CreatePlayerAccount,
                OrganizationPermissionNames.ViewPlayers,
                OrganizationPermissionNames.ViewBilling,
                OrganizationPermissionNames.TopUpWallet,
                OrganizationPermissionNames.RefundLedgerEntry,
                OrganizationPermissionNames.ManualLedgerCorrection,
                OrganizationPermissionNames.PayDebt,
                OrganizationPermissionNames.ApproveMoneyAction,
                OrganizationPermissionNames.ManageTariffs,
                OrganizationPermissionNames.ViewTariffs,
                OrganizationPermissionNames.ManagePackages,
                OrganizationPermissionNames.ViewPackages,
                OrganizationPermissionNames.PurchasePackage,
                OrganizationPermissionNames.OpenShift,
                OrganizationPermissionNames.CloseShift,
                OrganizationPermissionNames.ViewShift,
                OrganizationPermissionNames.ManageShiftCash,
                OrganizationPermissionNames.ViewReports,
                OrganizationPermissionNames.ViewReservations,
                OrganizationPermissionNames.ManageReservations,
                OrganizationPermissionNames.ManagePosCatalog,
                OrganizationPermissionNames.ManageShopOrders,
                OrganizationPermissionNames.CreatePosSale,
                OrganizationPermissionNames.PayPosSale,
                OrganizationPermissionNames.RefundPosSale,
                OrganizationPermissionNames.VoidPosSale,
                OrganizationPermissionNames.ManageInventoryStock,
                OrganizationPermissionNames.ViewInventory,
                OrganizationPermissionNames.ViewReceipt,
                OrganizationPermissionNames.ViewUpdateStatus,
                OrganizationPermissionNames.ViewDiagnostics,
                OrganizationPermissionNames.ManageBranchStaff,
                OrganizationPermissionNames.ViewAudit,
                OrganizationPermissionNames.ManageBranchSettings
            },
            [OrganizationRoleNames.ShiftSupervisor] = new HashSet<string>
            {
                OrganizationPermissionNames.ViewDeviceCommandStatus,
                OrganizationPermissionNames.ViewDeviceDetail,
                OrganizationPermissionNames.ViewFloorMap,
                OrganizationPermissionNames.StartSession,
                OrganizationPermissionNames.ExtendSession,
                OrganizationPermissionNames.TransferSession,
                OrganizationPermissionNames.EndSession,
                OrganizationPermissionNames.ViewSession,
                OrganizationPermissionNames.CreatePlayerAccount,
                OrganizationPermissionNames.ViewPlayers,
                OrganizationPermissionNames.ViewBilling,
                OrganizationPermissionNames.TopUpWallet,
                OrganizationPermissionNames.RefundLedgerEntry,
                OrganizationPermissionNames.ManualLedgerCorrection,
                OrganizationPermissionNames.PayDebt,
                OrganizationPermissionNames.ApproveMoneyAction,
                OrganizationPermissionNames.ViewTariffs,
                OrganizationPermissionNames.ViewPackages,
                OrganizationPermissionNames.PurchasePackage,
                OrganizationPermissionNames.OpenShift,
                OrganizationPermissionNames.CloseShift,
                OrganizationPermissionNames.ViewShift,
                OrganizationPermissionNames.ManageShiftCash,
                OrganizationPermissionNames.ViewReports,
                OrganizationPermissionNames.ViewReservations,
                OrganizationPermissionNames.ManageReservations,
                OrganizationPermissionNames.CreatePosSale,
                OrganizationPermissionNames.PayPosSale,
                OrganizationPermissionNames.RefundPosSale,
                OrganizationPermissionNames.VoidPosSale,
                OrganizationPermissionNames.ViewInventory,
                OrganizationPermissionNames.ViewReceipt,
                OrganizationPermissionNames.ViewUpdateStatus
            },
            [OrganizationRoleNames.Operator] = new HashSet<string>
            {
                OrganizationPermissionNames.ViewFloorMap,
                OrganizationPermissionNames.StartSession,
                OrganizationPermissionNames.ExtendSession,
                OrganizationPermissionNames.TransferSession,
                OrganizationPermissionNames.EndSession,
                OrganizationPermissionNames.ViewSession,
                OrganizationPermissionNames.CreatePlayerAccount,
                OrganizationPermissionNames.ViewPlayers,
                OrganizationPermissionNames.ViewBilling,
                OrganizationPermissionNames.TopUpWallet,
                OrganizationPermissionNames.PayDebt,
                OrganizationPermissionNames.ViewTariffs,
                OrganizationPermissionNames.ViewPackages,
                OrganizationPermissionNames.PurchasePackage,
                OrganizationPermissionNames.OpenShift,
                OrganizationPermissionNames.ViewShift,
                OrganizationPermissionNames.ViewReservations,
                OrganizationPermissionNames.ManageReservations,
                OrganizationPermissionNames.CreatePosSale,
                OrganizationPermissionNames.PayPosSale,
                OrganizationPermissionNames.ViewReceipt
            },
            [OrganizationRoleNames.Technician] = new HashSet<string>
            {
                OrganizationPermissionNames.CreateDeviceEnrollmentCode,
                OrganizationPermissionNames.DispatchDeviceCommand,
                OrganizationPermissionNames.ViewDeviceCommandStatus,
                OrganizationPermissionNames.RotateDeviceCredential,
                OrganizationPermissionNames.RevokeDeviceCredential,
                OrganizationPermissionNames.AssignDeviceSeat,
                OrganizationPermissionNames.ViewDeviceDetail,
                OrganizationPermissionNames.InstallDevice,
                OrganizationPermissionNames.ViewFloorMap,
                OrganizationPermissionNames.ViewInventory,
                OrganizationPermissionNames.ViewUpdateStatus,
                OrganizationPermissionNames.ViewDiagnostics
            },
            [OrganizationRoleNames.Accountant] = new HashSet<string>
            {
                OrganizationPermissionNames.ViewSession,
                OrganizationPermissionNames.ViewPlayers,
                OrganizationPermissionNames.ViewBilling,
                OrganizationPermissionNames.ViewTariffs,
                OrganizationPermissionNames.ViewPackages,
                OrganizationPermissionNames.ViewShift,
                OrganizationPermissionNames.ViewReports,
                OrganizationPermissionNames.ViewReservations,
                OrganizationPermissionNames.ViewInventory,
                OrganizationPermissionNames.ViewReceipt,
                OrganizationPermissionNames.ViewUpdateStatus,
                OrganizationPermissionNames.ViewDiagnostics,
                OrganizationPermissionNames.ViewAudit
            }
        };

    public static IReadOnlySet<string> GetPermissions(IEnumerable<string> roleNames)
    {
        var permissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        foreach (var roleName in roleNames)
        {
            if (!RolePermissions.TryGetValue(roleName, out var rolePermissions))
            {
                continue;
            }

            permissions.UnionWith(rolePermissions);
        }

        return permissions;
    }

    public static bool IsKnownRole(string roleName)
    {
        return RolePermissions.ContainsKey(roleName);
    }
}
