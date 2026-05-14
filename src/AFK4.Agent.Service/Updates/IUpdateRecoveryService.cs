namespace AFK4.Agent.Service.Updates;

public interface IUpdateRecoveryService
{
    Task RecoverAsync(CancellationToken cancellationToken);
}
