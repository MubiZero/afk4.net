namespace AFK4.Agent.Service;

public interface IInstalledAppReporter
{
    Task ReportAsync(
        IReadOnlyCollection<InstalledAppSnapshot> apps,
        DateTimeOffset reportedAtUtc,
        CancellationToken cancellationToken);
}
