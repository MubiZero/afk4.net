namespace AFK4.Platform.Api.Install;

public interface IInstallRequestThrottle
{
    Task ApplyAsync(string sourceIp, CancellationToken cancellationToken);
}
