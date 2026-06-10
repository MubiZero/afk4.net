namespace AFK4.SetupWizard.Core;

public sealed class EnvironmentBootstrapWriter(
    string machineName,
    EnvironmentVariableTarget target = EnvironmentVariableTarget.Machine) : ISetupWizardBootstrapWriter
{
    private const string OperatorPlatformBaseUrlEnvironmentVariable = "AFK4_OPERATOR_PLATFORM_BASE_URL";
    private const string OperatorOrganizationIdEnvironmentVariable = "AFK4_OPERATOR_ORGANIZATION_ID";
    private const string OperatorBranchIdEnvironmentVariable = "AFK4_OPERATOR_BRANCH_ID";

    public void Write(SetupWizardBootstrapConfig config)
    {
        var platformBaseUrl = config.ApiBaseUrl.TrimEnd('/');

        // Operator App reads these (it runs interactively, so it sees fresh machine env).
        Write(OperatorPlatformBaseUrlEnvironmentVariable, platformBaseUrl);
        Write(OperatorOrganizationIdEnvironmentVariable, config.OrganizationId.ToString("D"));
        Write(OperatorBranchIdEnvironmentVariable, config.BranchId.ToString("D"));

        // Agent config is ALSO emitted as machine env as a fallback. The Agent reads the bootstrap
        // FILE first (see FileBootstrapWriter): a service launched by the SCM inherits a stale
        // environment block and would not see these freshly-written values until the next reboot.
        foreach (var (key, value) in AgentBootstrapValues.Build(config, machineName))
        {
            Write("Agent__" + key, value);
        }
    }

    private void Write(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value, target);
    }
}
