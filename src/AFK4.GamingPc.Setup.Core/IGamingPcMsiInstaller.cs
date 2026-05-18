namespace AFK4.GamingPc.Setup.Core;

public interface IGamingPcMsiInstaller
{
    Task InstallAsync(CancellationToken cancellationToken);
}
