namespace AFK4.Shared.Contracts.Identity;

public static class OrganizationPermissionNames
{
    public const string CreateDeviceEnrollmentCode = "organization.devices.enrollment_codes.create";

    public const string DispatchDeviceCommand = "organization.devices.commands.dispatch";

    public const string ViewDeviceCommandStatus = "organization.devices.commands.status.view";

    public const string RotateDeviceCredential = "organization.devices.credentials.rotate";

    public const string RevokeDeviceCredential = "organization.devices.credentials.revoke";

    public const string AssignDeviceSeat = "organization.devices.seat_assignment.assign";

    public const string ViewDeviceDetail = "organization.devices.detail.view";

    public const string InstallDevice = "organization.devices.install";

    public const string ViewFloorMap = "organization.floor_map.view";

    public const string ManageLayout = "organization.layout.manage";

    public const string StartSession = "organization.sessions.start";

    public const string ExtendSession = "organization.sessions.extend";

    public const string TransferSession = "organization.sessions.transfer";

    public const string EndSession = "organization.sessions.end";

    public const string ViewSession = "organization.sessions.view";

    public const string CreatePlayerAccount = "organization.players.create";

    public const string ViewPlayers = "organization.players.view";

    public const string ViewBilling = "organization.billing.view";

    public const string TopUpWallet = "organization.billing.wallet.top_up";

    public const string RefundLedgerEntry = "organization.billing.refund";

    public const string ManualLedgerCorrection = "organization.billing.manual_correction";

    public const string PayDebt = "organization.billing.debt.pay";

    // Anti-fraud (§5.2/D2): approve an over-threshold high-risk money action raised by another actor.
    public const string ApproveMoneyAction = "organization.billing.money_action.approve";

    public const string ViewSubscription = "organization.billing.subscription.view";

    public const string ManageTariffs = "organization.tariffs.manage";

    public const string ViewTariffs = "organization.tariffs.view";

    public const string ManagePackages = "organization.packages.manage";

    public const string ViewPackages = "organization.packages.view";

    public const string PurchasePackage = "organization.packages.purchase";

    public const string OpenShift = "organization.shifts.open";

    public const string CloseShift = "organization.shifts.close";

    public const string ViewShift = "organization.shifts.view";

    public const string ManageShiftCash = "organization.shifts.cash.manage";

    public const string ViewReports = "organization.reports.view";

    public const string ViewReservations = "organization.reservations.view";

    public const string ManageReservations = "organization.reservations.manage";

    public const string ManagePosCatalog = "organization.pos.catalog.manage";

    public const string CreatePosSale = "organization.pos.sales.create";

    public const string PayPosSale = "organization.pos.sales.pay";

    public const string RefundPosSale = "organization.pos.sales.refund";

    public const string VoidPosSale = "organization.pos.sales.void";

    public const string ManageInventoryStock = "organization.inventory.stock.manage";

    public const string ViewInventory = "organization.inventory.view";

    public const string ViewReceipt = "organization.receipts.view";

    public const string ViewUpdateStatus = "organization.updates.status.view";

    public const string ViewDiagnostics = "organization.diagnostics.view";

    public const string ManageBranchStaff = "organization.identity.branch_staff.manage";

    public const string ManageRoles = "organization.identity.roles.manage";

    public const string ViewAudit = "organization.audit.view";

    // Owner-only: read org-wide audit across all branches + org-level records.
    public const string ViewOrganizationAudit = "organization.audit.organization.view";

    public const string ManageBranchSettings = "organization.branches.settings.manage";

    // Owner-only: view the org-wide branch roster (network overview).
    public const string ViewBranches = "organization.branches.view";

    // Owner-only: connect/manage the club's DC-Bank payment cards (dcgate gateways).
    public const string ManagePaymentGateways = "organization.payments.gateways.manage";

    public const string ManageShopOrders = "organization.shop.orders.manage";

    // Owner-only: configure org-wide loyalty/cashback rates.
    public const string ManageLoyaltySettings = "organization.loyalty.settings.manage";

    public const string ManageNews = "organization.news.manage";
}
