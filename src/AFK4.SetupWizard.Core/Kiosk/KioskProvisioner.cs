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

    /// <summary>Прочитать секрет LSA; null — такого секрета нет.</summary>
    string? ReadSecret(string name);

    /// <summary>Положить секрет LSA; null — удалить (секрета может и не быть — это тоже «удалён»).</summary>
    void StoreSecret(string name, string? value);

    KioskRegistryValue ReadMachineValue(string key, string name);

    void WriteMachineValue(string key, string name, KioskRegistryValue value);

    void DeleteMachineValue(string key, string name);

    /// <summary>Записать строку в куст пользователя — с загрузкой NTUSER.DAT, если он ещё не входил.</summary>
    void WriteUserValue(string sid, string key, string name, string value);
}

/// <summary>
/// Что было до киоска: SID учётки и прежние значения параметров ПК. <paramref name="AutologonPasswordSaved"/>
/// — пароль прежнего автовхода (или его отсутствие) удалось запомнить; у состояния, записанного
/// прежней версией мастера, его нет, и откат честно выключает автовход вместо входа с пустым паролем.
/// </summary>
public sealed record KioskState(string Sid, IReadOnlyList<KioskMachineSetting> Previous, bool AutologonPasswordSaved = false);

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
        var existing = stateStore.Load();
        var previous = existing?.Previous
            ?? KioskSettings.Tracked
                .Select(entry => new KioskMachineSetting(entry.Key, entry.Name, machine.ReadMachineValue(entry.Key, entry.Name)))
                .ToList();
        var passwordSaved = existing?.AutologonPasswordSaved ?? SavePreviousAutologonPassword();

        // Пароль новый при каждой установке: старый нигде не записан, и сверить его не с чем.
        var password = newPassword();
        var sid = machine.EnsureUser(KioskSettings.UserName, password);
        stateStore.Save(new KioskState(sid, previous, passwordSaved));

        machine.StoreSecret(KioskSettings.AutologonSecret, password);
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

        RestoreAutologonPassword(state);
        machine.DeleteUser(KioskSettings.UserName, state.Sid);
        stateStore.Clear();
    }

    /// <summary>
    /// Пароль прежнего автовхода — до того, как киоск перепишет секрет LSA и сотрёт пароль из
    /// реестра. Не вышло запомнить — не беда для установки: откат тогда выключит автовход.
    /// </summary>
    private bool SavePreviousAutologonPassword()
    {
        try
        {
            var previous = machine.ReadSecret(KioskSettings.AutologonSecret)
                ?? machine.ReadMachineValue(KioskSettings.WinlogonKey, KioskSettings.PlainAutologonPassword).Text;
            machine.StoreSecret(KioskSettings.SavedAutologonSecret, previous);
            return true;
        }
        catch (Exception exception) when (exception is not OutOfMemoryException)
        {
            SetupWizardStartupLog.Write("The previous autologon password could not be saved; removing the kiosk will turn autologon off.", exception);
            return false;
        }
    }

    /// <summary>
    /// Автовход возвращается целиком: без пароля он не работает, а Windows после первой неудачи сама
    /// сбрасывает AutoAdminLogon в 0 — ПК вставал на экране входа с обещанием «настройки вернутся».
    /// Пароль прежнего автовхода неизвестен (киоск ставила старая версия мастера или чтение не
    /// удалось) — автовход выключается, а не оставляется сломанным.
    /// </summary>
    private void RestoreAutologonPassword(KioskState state)
    {
        var saved = state.AutologonPasswordSaved ? machine.ReadSecret(KioskSettings.SavedAutologonSecret) : null;
        machine.StoreSecret(KioskSettings.AutologonSecret, saved);
        machine.StoreSecret(KioskSettings.SavedAutologonSecret, null);

        var wasEnabled = state.Previous.Any(setting =>
            setting.Key == KioskSettings.WinlogonKey && setting.Name == KioskSettings.AutologonEnabled && setting.Value.Text == "1");
        if (wasEnabled && !state.AutologonPasswordSaved)
        {
            machine.WriteMachineValue(KioskSettings.WinlogonKey, KioskSettings.AutologonEnabled, KioskRegistryValue.Of("0"));
            SetupWizardStartupLog.Write("Autologon was turned off: the previous autologon password is unknown.");
        }
    }
}
