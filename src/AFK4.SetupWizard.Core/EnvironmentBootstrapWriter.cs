using System.Runtime.InteropServices;
using AFK4.Shared.Contracts.Install;

namespace AFK4.SetupWizard.Core;

/// <summary>
/// Запасной канал настройки: машинные переменные среды.
///
/// Секретов здесь нет намеренно. Машинные переменные лежат в реестре и читаются любой учётной
/// записью на этой машине — гость за игровым ПК набирает <c>echo %Agent__DeviceCredentialSecret%</c>
/// и получает ключ, которым этот ПК представляется платформе. Тот же ключ в bootstrap.json
/// заперт правами на систему и администраторов; публиковать его рядом открытым текстом значило
/// бы обнулить этот замок.
///
/// Настройки приложения клуба — исключение и только на рабочем месте управляющего: приложение
/// работает от имени кассира и запертый файл прочитать не может, а гостей за этой машиной нет.
/// На игровом ПК приложения клуба нет вовсе, поэтому там их не пишем.
/// </summary>
public sealed class EnvironmentBootstrapWriter(
    string machineName,
    EnvironmentVariableTarget target = EnvironmentVariableTarget.Machine) : ISetupWizardBootstrapWriter
{
    private const string OperatorPlatformBaseUrlEnvironmentVariable = "AFK4_ORGANIZATION_ADMIN_PLATFORM_BASE_URL";
    private const string OperatorOrganizationIdEnvironmentVariable = "AFK4_ORGANIZATION_ADMIN_ORGANIZATION_ID";
    private const string OperatorBranchIdEnvironmentVariable = "AFK4_ORGANIZATION_ADMIN_BRANCH_ID";
    private const string OperatorUpdatePipeNameEnvironmentVariable = "AFK4_ORGANIZATION_ADMIN_UPDATE_COORDINATION_PIPE_NAME";
    private const string OperatorUpdateSecretEnvironmentVariable = "AFK4_ORGANIZATION_ADMIN_UPDATE_COORDINATION_SECRET";

    /// <summary>Значения, которые машинной переменной не становятся никогда.</summary>
    private static readonly HashSet<string> SecretAgentKeys = new(StringComparer.Ordinal)
    {
        "DeviceCredentialSecret",
        "OrganizationAdminUpdateCoordinationSecret"
    };

    public void Write(SetupWizardBootstrapConfig config)
    {
        var platformBaseUrl = config.ApiBaseUrl.TrimEnd('/');

        var agentValues = AgentBootstrapValues.Build(config, machineName);

        if (string.Equals(config.Role, DeviceRoleNames.ManagerWorkstation, StringComparison.Ordinal))
        {
            // Organization Admin reads these (it runs interactively, so it sees fresh machine env).
            Write(OperatorPlatformBaseUrlEnvironmentVariable, platformBaseUrl);
            Write(OperatorOrganizationIdEnvironmentVariable, config.OrganizationId.ToString("D"));
            Write(OperatorBranchIdEnvironmentVariable, config.BranchId.ToString("D"));
            Write(OperatorUpdatePipeNameEnvironmentVariable, agentValues["OrganizationAdminUpdateCoordinationPipeName"]);
            Write(OperatorUpdateSecretEnvironmentVariable, agentValues["OrganizationAdminUpdateCoordinationSecret"]);
        }
        else
        {
            // Машина могла быть рабочим местом управляющего раньше. Не написать секрет мало —
            // оставленный от прошлой роли, он лежит ровно там же и читается так же.
            Remove(OperatorPlatformBaseUrlEnvironmentVariable);
            Remove(OperatorOrganizationIdEnvironmentVariable);
            Remove(OperatorBranchIdEnvironmentVariable);
            Remove(OperatorUpdatePipeNameEnvironmentVariable);
            Remove(OperatorUpdateSecretEnvironmentVariable);
        }

        // Секреты, написанные прошлыми версиями мастера, тоже убираем: машина, настроенная
        // раньше, иначе донесёт открытый ключ устройства до первого переустановления Windows.
        foreach (var secretKey in SecretAgentKeys)
        {
            Remove("Agent__" + secretKey);
        }

        // Agent config is ALSO emitted as machine env as a fallback. The Agent reads the bootstrap
        // FILE first (see FileBootstrapWriter): a service launched by the SCM inherits a stale
        // environment block and would not see these freshly-written values until the next reboot.
        // Секреты в этот запасной канал не попадают: за ними агент идёт только в запертый файл.
        foreach (var (key, value) in agentValues)
        {
            if (SecretAgentKeys.Contains(key))
            {
                continue;
            }

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

    private void Remove(string name)
    {
        Environment.SetEnvironmentVariable(name, null, target);
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
