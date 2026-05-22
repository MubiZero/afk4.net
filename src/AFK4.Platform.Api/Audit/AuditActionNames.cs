namespace AFK4.Platform.Api.Audit;

public static class AuditActionNames
{
    public const string CreateDeviceEnrollmentCode = "devices.enrollment_codes.create";

    public const string DispatchDeviceCommand = "devices.commands.dispatch";

    public const string ViewDeviceCommandStatus = "devices.commands.status.view";

    public const string RotateDeviceCredential = "devices.credentials.rotate";

    public const string RevokeDeviceCredential = "devices.credentials.revoke";

    public const string AssignDeviceSeat = "devices.seat_assignment.assign";

    public const string StartSession = "sessions.start";

    public const string ExtendSession = "sessions.extend";

    public const string TransferSession = "sessions.transfer";

    public const string EndSession = "sessions.end";

    public const string CreatePlayerAccount = "players.create";

    public const string ViewPlayers = "players.view";

    public const string TopUpWallet = "billing.wallet.top_up";

    public const string RefundLedgerEntry = "billing.refund";

    public const string ManualLedgerCorrection = "billing.manual_correction";

    public const string PayDebt = "billing.debt.pay";

    public const string CreateTariff = "tariffs.create";

    public const string CreateTariffVersion = "tariffs.versions.create";

    public const string ViewTariffs = "tariffs.view";

    public const string CreatePackageDefinition = "packages.create";

    public const string UpdatePackageDefinition = "packages.update";

    public const string ViewPackages = "packages.view";

    public const string PurchasePackage = "packages.purchase";

    public const string OpenShift = "shifts.open";

    public const string CloseShift = "shifts.close";

    public const string RecordCashMovement = "shifts.cash_movement";

    public const string ViewShiftReport = "reports.shifts.view";

    public const string ViewSalesReport = "reports.sales.view";

    public const string ViewGameplayTimeReport = "reports.gameplay_time.view";

    public const string ViewCashOperationReport = "reports.cash_operations.view";

    public const string ViewOperatorActionReport = "reports.operator_actions.view";

    public const string ViewDashboardSummary = "dashboard.summary.view";

    public const string ViewReservations = "reservations.view";

    public const string CreateReservation = "reservations.create";

    public const string UpdateReservation = "reservations.update";

    public const string ConfirmReservation = "reservations.confirm";

    public const string SeatReservation = "reservations.seat";

    public const string CancelReservation = "reservations.cancel";

    public const string CreateProductCategory = "pos.categories.create";

    public const string CreateProduct = "pos.products.create";

    public const string UpdateProduct = "pos.products.update";

    public const string CreateStockMovement = "inventory.stock.create";

    public const string CreatePosSale = "pos.sales.create";

    public const string PayPosSale = "pos.sales.pay";

    public const string RefundPosSale = "pos.sales.refund";

    public const string VoidPosSale = "pos.sales.void";

    public const string RegisterUpdatePackage = "updates.packages.register";

    public const string ChangeUpdatePackageState = "updates.packages.state.change";

    public const string CreateUpdateRollout = "updates.rollouts.create";

    public const string ChangeUpdateRolloutState = "updates.rollouts.state.change";

    public const string ViewUpdateRollout = "updates.rollouts.view";

    public const string ViewDiagnostics = "diagnostics.view";

    public const string ViewAudit = "audit.view";

    public const string CreateStaffUser = "identity.staff.create";

    public const string ViewStaffUsers = "identity.staff.view";

    public const string UpdateStaffRoles = "identity.staff.roles.update";

    public const string CreateZone = "layout.zones.create";

    public const string CreateSeat = "layout.seats.create";

    public const string ViewLayout = "layout.view";

    public const string ViewBranchProfile = "branches.profile.view";

    public const string UpdateBranchProfile = "branches.profile.update";
}
