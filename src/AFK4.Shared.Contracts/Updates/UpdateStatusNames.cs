namespace AFK4.Shared.Contracts.Updates;

public static class UpdateStatusNames
{
    public const string NotStarted = "not-started";

    public const string Offered = "offered";

    public const string Downloading = "downloading";

    public const string Downloaded = "downloaded";

    public const string Installing = "installing";

    public const string Installed = "installed";

    public const string Superseded = "superseded";

    public const string Failed = "failed";

    public const string RollbackStarted = "rollback-started";

    public const string RolledBack = "rolled-back";

    public const string Deferred = "deferred";

    public const string ReadyToInstall = "ready-to-install";

    public const string AwaitingAppExit = "awaiting-app-exit";

    public const string HealthChecking = "health-checking";

    public const string RollbackRequired = "rollback-required";
}
