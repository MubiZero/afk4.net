namespace AFK4.Agent.Service.Updates;

public sealed record UpdateRestartResult(bool Succeeded, bool IsRequired, string Message)
{
    public static UpdateRestartResult NotRequired(string message)
    {
        return new UpdateRestartResult(true, false, message);
    }

    public static UpdateRestartResult Scheduled(string message)
    {
        return new UpdateRestartResult(true, true, message);
    }

    public static UpdateRestartResult Failed(string message)
    {
        return new UpdateRestartResult(false, true, message);
    }
}
