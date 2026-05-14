namespace AFK4.Agent.Service.Enforcement;

public sealed record SessionEnforcementResult(string Status, string Message)
{
    public static SessionEnforcementResult Accepted(string message)
    {
        return new SessionEnforcementResult("Accepted", message);
    }

    public static SessionEnforcementResult Rejected(string message)
    {
        return new SessionEnforcementResult("Rejected", message);
    }
}
