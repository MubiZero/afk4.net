namespace AFK4.GamingPc.Setup.Core;

public interface IWindowsServiceController
{
    void StopIfExists(string serviceName);

    void Start(string serviceName);

    string QueryStatus(string serviceName);
}
