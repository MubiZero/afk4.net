namespace AFK4.Agent.Service.Updates;

public sealed record UpdateRollbackResult(bool Succeeded, string Message)
{
    public static UpdateRollbackResult Success(string message)
    {
        return new UpdateRollbackResult(true, message);
    }

    public static UpdateRollbackResult Failed(string message)
    {
        return new UpdateRollbackResult(false, message);
    }
}
