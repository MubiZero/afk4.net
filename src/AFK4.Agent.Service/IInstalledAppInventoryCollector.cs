namespace AFK4.Agent.Service;

public interface IInstalledAppInventoryCollector
{
    Task<IReadOnlyCollection<InstalledAppSnapshot>> CollectAsync(CancellationToken cancellationToken);
}
