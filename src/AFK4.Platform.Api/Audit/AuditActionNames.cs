namespace AFK4.Platform.Api.Audit;

public static class AuditActionNames
{
    public const string CreateDeviceEnrollmentCode = "devices.enrollment_codes.create";

    public const string DispatchDeviceCommand = "devices.commands.dispatch";

    public const string ViewDeviceCommandStatus = "devices.commands.status.view";

    public const string RotateDeviceCredential = "devices.credentials.rotate";

    public const string RevokeDeviceCredential = "devices.credentials.revoke";

    public const string AssignDeviceSeat = "devices.seat_assignment.assign";

    public const string ApprovePendingDevice = "devices.pending.approve";

    public const string RejectPendingDevice = "devices.pending.reject";

    public const string RenameDevice = "devices.rename";

    public const string MoveDeviceSeat = "devices.seat.move";

    public const string RemoveDevice = "devices.remove";

    public const string StartSession = "sessions.start";

    public const string ExtendSession = "sessions.extend";

    public const string TransferSession = "sessions.transfer";

    public const string EndSession = "sessions.end";

    public const string CheckoutSession = "sessions.checkout";

    public const string CreatePlayerAccount = "players.create";

    public const string ViewPlayers = "players.view";

    public const string TopUpWallet = "billing.wallet.top_up";

    public const string RefundLedgerEntry = "billing.refund";

    public const string ManualLedgerCorrection = "billing.manual_correction";

    public const string PayDebt = "billing.debt.pay";

    // Anti-fraud (§5.2/§5.4/§5.7) — money-action approval lifecycle, comped sessions, shift sign-off.
    public const string MoneyActionRequested = "billing.money_action.requested";

    public const string MoneyActionApproved = "billing.money_action.approved";

    public const string MoneyActionRejected = "billing.money_action.rejected";

    public const string MoneyActionExecuted = "billing.money_action.executed";

    public const string SessionComp = "session.comp";

    public const string ShiftSignOff = "shifts.signoff";

    public const string CreateTariff = "tariffs.create";

    public const string CreateTariffVersion = "tariffs.versions.create";

    public const string UpdateTariff = "tariffs.update";

    public const string UpdateTariffVersion = "tariffs.versions.update";

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

    public const string ViewOwnerDailySummaryReport = "reports.owner_daily_summary.view";

    public const string CreateReportSchedule = "reports.schedules.create";

    public const string DeleteReportSchedule = "reports.schedules.delete";

    public const string ViewDashboardSummary = "dashboard.summary.view";

    public const string ViewReservations = "reservations.view";

    public const string ViewSessions = "sessions.view";

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

    public const string CreateStaffInvite = "identity.staff.invite.create";

    public const string ViewStaffUsers = "identity.staff.view";

    public const string UpdateStaffProfile = "identity.staff.profile.update";

    public const string UpdateStaffRoles = "identity.staff.roles.update";

    public const string UpdateStaffState = "identity.staff.state.update";

    public const string ResetStaffPassword = "identity.staff.password.reset";

    public const string CreateZone = "layout.zones.create";

    public const string UpdateZone = "layout.zones.update";

    public const string DeleteZone = "layout.zones.delete";

    public const string CreateSeat = "layout.seats.create";

    public const string UpdateSeat = "layout.seats.update";

    public const string DeleteSeat = "layout.seats.delete";

    public const string ViewLayout = "layout.view";

    public const string ViewBranchProfile = "branches.profile.view";

    public const string UpdateBranchProfile = "branches.profile.update";

    public const string PlatformAdminSignIn = "identity.platform_admin.sign_in";

    public const string PlatformAdminRefresh = "identity.platform_admin.refresh";

    public const string PlatformAdminSignOut = "identity.platform_admin.sign_out";

    public const string PlatformAdminBootstrap = "identity.platform_admin.bootstrap";

    public const string CreateTenant = "tenancy.tenant.create";

    public const string UpdateTenantStatus = "tenancy.tenant.status.update";

    public const string UpdateTenantLimits = "tenancy.tenant.limits.update";

    public const string ViewTenant = "tenancy.tenant.view";

    public const string CreateOwnerInvite = "tenancy.owner_invite.create";

    public const string ViewOwnerInvites = "tenancy.owner_invite.view";

    public const string AcceptOwnerInvite = "tenancy.owner_invite.accept";

    public const string RevokeOwnerInvite = "tenancy.owner_invite.revoke";

    public const string ResendOwnerInvite = "tenancy.owner_invite.resend";

    public const string CreateTenantSupportNote = "tenancy.support_note.create";

    public const string UpdateTenantSupportNote = "tenancy.support_note.update";

    public const string ViewTenantSupportNotes = "tenancy.support_note.view";

    public const string ViewTenantHealth = "tenancy.tenant.health.view";

    public const string ResolveOperatorConnection = "tenancy.operator_connection.resolve";

    public const string InstallDiscoverInvoked = "install.discover.invoked";

    public const string InstallEnrollSucceeded = "install.enroll.succeeded";

    public const string InstallEnrollRejected = "install.enroll.rejected";

    public const string ViewBranchSettings = "branches.settings.view";

    public const string UpdateBranchSettings = "branches.settings.update";

    public const string UpdateFloorMap = "floor_map.update";

    public const string ViewBilling = "billing.view";

    public const string CreatePlan = "billing.plan.create";

    public const string UpdatePlan = "billing.plan.update";

    public const string UpdateSubscription = "billing.subscription.update";

    public const string GenerateInvoice = "billing.invoice.generate";

    public const string MarkInvoicePaid = "billing.invoice.mark_paid";

    public const string VoidInvoice = "billing.invoice.void";

    public const string FulfilPaymentIntent = "billing.payment_intent.fulfil";

    public const string AcceptShopOrder = "shop.order.accept";

    public const string DeliverShopOrder = "shop.order.deliver";

    public const string CancelShopOrder = "shop.order.cancel";

    public const string UpdateLoyaltySettings = "loyalty.settings.update";

    public const string CreateNews = "news.create";

    public const string UpdateNews = "news.update";

    public const string DeleteNews = "news.delete";
}
