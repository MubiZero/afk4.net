using System.Runtime.InteropServices;

namespace AFK4.SetupWizard.Core;

public sealed class EnvironmentBootstrapWriter(
    string machineName,
    EnvironmentVariableTarget target = EnvironmentVariableTarget.Machine) : ISetupWizardBootstrapWriter
{
    private const string OperatorPlatformBaseUrlEnvironmentVariable = "AFK4_ORGANIZATION_ADMIN_PLATFORM_BASE_URL";
    private const string OperatorOrganizationIdEnvironmentVariable = "AFK4_ORGANIZATION_ADMIN_ORGANIZATION_ID";
    private const string OperatorBranchIdEnvironmentVariable = "AFK4_ORGANIZATION_ADMIN_BRANCH_ID";

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

        // An Explorer session that predates the wizard still carries a stale environment block, so
        // a freshly-machine-written value isn't visible to processes it launches (e.g. the operator)
        // until the next sign-in. Broadcasting WM_SETTINGCHANGE("Environment") tells running shells
        // to re-read the machine env. Only matters for machine-scope writes.
        if (target == EnvironmentVariableTarget.Machine)
        {
            BroadcastEnvironmentChange();
        }
    }

    private void Write(string name, string value)
    {
        Environment.SetEnvironmentVariable(name, value, target);
    }

    private static void BroadcastEnvironmentChange()
    {
        if (!OperatingSystem.IsWindows())
        {
            return;
        }

        SendMessageTimeout(
            HwndBroadcast,
            WmSettingChange,
            UIntPtr.Zero,
            "Environment",
            SmtoAbortIfHung,
            millisecondsTimeout: 5000,
            out _);
    }

    private static readonly IntPtr HwndBroadcast = new(0xffff);
    private const uint WmSettingChange = 0x001A;
    private const uint SmtoAbortIfHung = 0x0002;

    [DllImport("user32.dll", CharSet = CharSet.Unicode, SetLastError = true)]
    private static extern IntPtr SendMessageTimeout(
        IntPtr hWnd,
        uint msg,
        UIntPtr wParam,
        string lParam,
        uint flags,
        uint millisecondsTimeout,
        out UIntPtr result);
}
