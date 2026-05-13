namespace AFK4.Platform.Api.Audit;

public static class AuditActionNames
{
    public const string CreateDeviceEnrollmentCode = "devices.enrollment_codes.create";

    public const string DispatchDeviceCommand = "devices.commands.dispatch";

    public const string ViewDeviceCommandStatus = "devices.commands.status.view";

    public const string RotateDeviceCredential = "devices.credentials.rotate";

    public const string RevokeDeviceCredential = "devices.credentials.revoke";

    public const string StartSession = "sessions.start";

    public const string ExtendSession = "sessions.extend";

    public const string TransferSession = "sessions.transfer";

    public const string EndSession = "sessions.end";

    public const string CreatePlayerAccount = "players.create";

    public const string TopUpWallet = "billing.wallet.top_up";

    public const string RefundLedgerEntry = "billing.refund";

    public const string ManualLedgerCorrection = "billing.manual_correction";

    public const string PayDebt = "billing.debt.pay";

    public const string CreateTariff = "tariffs.create";

    public const string CreateTariffVersion = "tariffs.versions.create";

    public const string CreatePackageDefinition = "packages.create";

    public const string PurchasePackage = "packages.purchase";
}
