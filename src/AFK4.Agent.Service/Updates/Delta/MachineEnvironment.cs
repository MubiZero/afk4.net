using AFK4.Shared.Contracts.Updates;

namespace AFK4.Agent.Service.Updates.Delta;

/// <summary>Машинные переменные окружения: их пишут MSI, и из них агент узнаёт версии установленных программ.</summary>
public interface IMachineEnvironment
{
    void Set(string name, string value);
}

public sealed class WindowsMachineEnvironment : IMachineEnvironment
{
    public void Set(string name, string value) =>
        Environment.SetEnvironmentVariable(name, value, EnvironmentVariableTarget.Machine);
}

public static class ComponentVersionVariables
{
    /// <summary>Та же переменная, что пишет MSI компонента (installers/*/Package.wxs): после дельты версия должна сдвинуться так же.</summary>
    public static string? For(string component) => component switch
    {
        UpdateComponentNames.AgentService => "Agent__AgentVersion",
        UpdateComponentNames.PlayerShell => "Agent__ShellVersion",
        UpdateComponentNames.OrganizationAdmin => "Agent__OrganizationAdminVersion",
        _ => null
    };
}
