namespace AFK4.SetupWizard.Core.Kiosk;

/// <summary>
/// Системные действия киоска — тонкая прослойка над Windows (учётки, LSA, реестр, профиль).
/// Решения о том, что и в каком порядке делать, живут в <see cref="KioskProvisioner"/>.
/// </summary>
public interface IKioskMachine
{
    /// <summary>Завести обычного локального пользователя или сменить пароль существующему. Возвращает SID.</summary>
    string EnsureUser(string userName, string password);

    /// <summary>Удалить пользователя и его профиль.</summary>
    void DeleteUser(string userName, string sid);

    /// <summary>Положить пароль автовхода секретом LSA; null — удалить секрет.</summary>
    void StoreAutologonPassword(string? password);

    KioskRegistryValue ReadMachineValue(string key, string name);

    void WriteMachineValue(string key, string name, KioskRegistryValue value);

    void DeleteMachineValue(string key, string name);

    /// <summary>Записать строку в куст пользователя — с загрузкой NTUSER.DAT, если он ещё не входил.</summary>
    void WriteUserValue(string sid, string key, string name, string value);
}

/// <summary>Что было до киоска: SID учётки и прежние значения параметров ПК.</summary>
public sealed record KioskState(string Sid, IReadOnlyList<KioskMachineSetting> Previous);

public interface IKioskStateStore
{
    KioskState? Load();

    void Save(KioskState state);

    void Clear();
}

/// <summary>SID учётки игрока — агенту, для прав на канал с оболочкой (спека, §4.2).</summary>
public interface IKioskAgentConfig
{
    void Write(string playerSid);

    void Clear();
}

/// <summary>
/// Киоск на игровом ПК (спека оболочки, §6.1): учётка «AFK4 Player», автовход в неё, оболочка
/// игрока вместо проводника для неё одной. Учётка администратора ПК остаётся с проводником.
/// </summary>
public sealed class KioskProvisioner(
    IKioskMachine machine,
    IKioskStateStore stateStore,
    IKioskAgentConfig agentConfig,
    Func<string>? passwordFactory = null)
{
    private readonly Func<string> newPassword = passwordFactory ?? KioskPassword.Generate;

    public bool IsInstalled => stateStore.Load() is not null;

    /// <summary>Поставить киоск или обновить его. Возвращает SID учётки игрока.</summary>
    public string Provision(string hostExecutablePath)
    {
        // Прежние значения записываются до первой правки и только один раз: повторная установка
        // запомнила бы как «прежние» уже киосковые, и откат вернул бы киоск сам в себя.
        var previous = stateStore.Load()?.Previous
            ?? KioskSettings.Machine
                .Select(setting => setting with { Value = machine.ReadMachineValue(setting.Key, setting.Name) })
                .ToList();

        // Пароль новый при каждой установке: старый нигде не записан, и сверить его не с чем.
        var password = newPassword();
        var sid = machine.EnsureUser(KioskSettings.UserName, password);
        stateStore.Save(new KioskState(sid, previous));

        machine.StoreAutologonPassword(password);
        foreach (var setting in KioskSettings.Machine)
        {
            machine.WriteMachineValue(setting.Key, setting.Name, setting.Value);
        }

        foreach (var (key, name) in KioskSettings.MachineRemoved)
        {
            machine.DeleteMachineValue(key, name);
        }

        machine.WriteUserValue(sid, KioskSettings.UserWinlogonKey, "Shell", KioskSettings.ShellCommand(hostExecutablePath));
        agentConfig.Write(sid);
        return sid;
    }

    /// <summary>«Снять киоск»: вернуть параметры ПК, выключить автовход, удалить учётку.</summary>
    public void Remove()
    {
        var state = stateStore.Load();
        if (state is null)
        {
            return;
        }

        // Сначала агент: канал с оболочкой снова открывается любому вошедшему, а не удалённой учётке.
        agentConfig.Clear();
        foreach (var setting in state.Previous)
        {
            if (setting.Value.IsAbsent)
            {
                machine.DeleteMachineValue(setting.Key, setting.Name);
            }
            else
            {
                machine.WriteMachineValue(setting.Key, setting.Name, setting.Value);
            }
        }

        machine.StoreAutologonPassword(null);
        machine.DeleteUser(KioskSettings.UserName, state.Sid);
        stateStore.Clear();
    }
}
