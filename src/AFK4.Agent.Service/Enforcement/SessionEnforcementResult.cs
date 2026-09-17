using AFK4.Shared.Contracts.Devices;

namespace AFK4.Agent.Service.Enforcement;

public sealed record SessionEnforcementResult(string Status, string Message, string Outcome)
{
    public static SessionEnforcementResult Accepted(string message, string outcome)
    {
        return new SessionEnforcementResult("Accepted", message, outcome);
    }

    public static SessionEnforcementResult Rejected(string message, string outcome)
    {
        return new SessionEnforcementResult("Rejected", message, outcome);
    }
}
