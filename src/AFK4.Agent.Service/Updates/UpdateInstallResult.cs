namespace AFK4.Agent.Service.Updates;

public sealed record UpdateInstallResult(bool Succeeded, string Message)
{
    public static UpdateInstallResult Success(string message)
    {
        return new UpdateInstallResult(true, message);
    }

    public static UpdateInstallResult Failed(string message)
    {
        return new UpdateInstallResult(false, message);
    }
}
