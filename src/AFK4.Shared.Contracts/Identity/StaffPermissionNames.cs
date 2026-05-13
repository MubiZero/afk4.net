namespace AFK4.Shared.Contracts.Identity;

public static class StaffPermissionNames
{
    public const string CreateDeviceEnrollmentCode = "devices.enrollment_codes.create";

    public const string DispatchDeviceCommand = "devices.commands.dispatch";

    public const string ViewDeviceCommandStatus = "devices.commands.status.view";

    public const string RotateDeviceCredential = "devices.credentials.rotate";

    public const string RevokeDeviceCredential = "devices.credentials.revoke";

    public const string ViewDeviceDetail = "devices.detail.view";

    public const string ViewFloorMap = "floor_map.view";

    public const string StartSession = "sessions.start";

    public const string ExtendSession = "sessions.extend";

    public const string TransferSession = "sessions.transfer";

    public const string EndSession = "sessions.end";

    public const string ViewSession = "sessions.view";

    public const string CreatePlayerAccount = "players.create";

    public const string ViewPlayers = "players.view";

    public const string ViewBilling = "billing.view";

    public const string TopUpWallet = "billing.wallet.top_up";

    public const string RefundLedgerEntry = "billing.refund";

    public const string ManualLedgerCorrection = "billing.manual_correction";

    public const string PayDebt = "billing.debt.pay";

    public const string ManageTariffs = "tariffs.manage";

    public const string ViewTariffs = "tariffs.view";

    public const string ManagePackages = "packages.manage";

    public const string ViewPackages = "packages.view";

    public const string PurchasePackage = "packages.purchase";

    public const string OpenShift = "shifts.open";

    public const string CloseShift = "shifts.close";

    public const string ViewShift = "shifts.view";

    public const string ManageShiftCash = "shifts.cash.manage";

    public const string ManagePosCatalog = "pos.catalog.manage";

    public const string CreatePosSale = "pos.sales.create";

    public const string PayPosSale = "pos.sales.pay";

    public const string RefundPosSale = "pos.sales.refund";

    public const string VoidPosSale = "pos.sales.void";

    public const string ManageInventoryStock = "inventory.stock.manage";

    public const string ViewInventory = "inventory.view";

    public const string ViewReceipt = "receipts.view";

    public const string ManageRoles = "identity.roles.manage";

    public const string ViewAudit = "audit.view";
}
