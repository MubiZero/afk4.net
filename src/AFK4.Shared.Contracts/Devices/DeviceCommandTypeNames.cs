namespace AFK4.Shared.Contracts.Devices;

public static class DeviceCommandTypeNames
{
    public const string Lock = "lock";

    public const string Unlock = "unlock";

    public const string RefreshSessionLease = "refresh-session-lease";

    // A non-blocking warning overlay pushed to the shell (e.g. fixed time almost
    // up, or an open tab approaching its credit limit).
    public const string Warn = "warn";
}
