namespace AFK4.Agent.Service;

public sealed class StaticInstalledAppInventoryCollector(
    IReadOnlyCollection<InstalledAppSnapshot> apps) : IInstalledAppInventoryCollector
{
    public Task<IReadOnlyCollection<InstalledAppSnapshot>> CollectAsync(CancellationToken cancellationToken)
    {
        return Task.FromResult(apps);
    }
}
