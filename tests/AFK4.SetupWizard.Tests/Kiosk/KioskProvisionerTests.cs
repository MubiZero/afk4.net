using AFK4.SetupWizard.Core.Kiosk;

namespace AFK4.SetupWizard.Tests.Kiosk;

/// <summary>Киоск на игровом ПК и его откат (спека оболочки, §6.1).</summary>
public sealed class KioskProvisionerTests
{
    private const string Host = @"C:\Program Files\AFK4\Player Shell\AFK4.Player.Shell.exe";

    [Fact]
    public void Provision_SignsThePlayerAccountInOnItsOwn_WithThePlayerShellAsItsDesktop()
    {
        var fixture = new Fixture();

        var sid = fixture.Provisioner.Provision(Host);

        Assert.Equal(FakeMachine.PlayerSid, sid);
        Assert.Equal(KioskSettings.UserName, fixture.Machine.Users.Single().Name);
        Assert.Equal(fixture.Machine.Users.Single().Password, fixture.Machine.AutologonPassword);
        Assert.Equal("1", fixture.Machine.Value(KioskSettings.WinlogonKey, "AutoAdminLogon").Text);
        Assert.Equal(KioskSettings.UserName, fixture.Machine.Value(KioskSettings.WinlogonKey, "DefaultUserName").Text);
        Assert.Equal($"\"{Host}\"", fixture.Machine.UserValues[(sid, KioskSettings.UserWinlogonKey, "Shell")]);
        Assert.Equal(sid, fixture.AgentConfig.Sid);
    }

    /// <summary>Пароль открытым текстом в Winlogon — ровно та дыра, от которой спасает секрет LSA.</summary>
    [Fact]
    public void Provision_RemovesAPlainTextPassword_AndTheLogonCounter()
    {
        var fixture = new Fixture();
        fixture.Machine.Set(KioskSettings.WinlogonKey, "DefaultPassword", KioskRegistryValue.Of("hunter2"));
        fixture.Machine.Set(KioskSettings.WinlogonKey, "AutoLogonCount", KioskRegistryValue.Of(3));

        fixture.Provisioner.Provision(Host);

        Assert.True(fixture.Machine.Value(KioskSettings.WinlogonKey, "DefaultPassword").IsAbsent);
        Assert.True(fixture.Machine.Value(KioskSettings.WinlogonKey, "AutoLogonCount").IsAbsent);
    }

    [Fact]
    public void Provision_TurnsOffWhatBreaksAutologon()
    {
        var fixture = new Fixture();

        fixture.Provisioner.Provision(Host);

        Assert.Equal(0, fixture.Machine.Value(@"SOFTWARE\Microsoft\Windows NT\CurrentVersion\PasswordLess\Device", "DevicePasswordLessBuildVersion").Number);
        Assert.Equal(1, fixture.Machine.Value(@"SOFTWARE\Policies\Microsoft\Windows\Personalization", "NoLockScreen").Number);
        Assert.Equal(0, fixture.Machine.Value(@"SOFTWARE\Microsoft\Windows\CurrentVersion\Policies\System", "EnableFirstLogonAnimation").Number);
    }

    [Fact]
    public void AGeneratedPassword_IsLongAndMixed_AndNeverRepeats()
    {
        var first = KioskPassword.Generate();
        var second = KioskPassword.Generate();

        Assert.Equal(32, first.Length);
        Assert.Contains(first, char.IsUpper);
        Assert.Contains(first, char.IsLower);
        Assert.Contains(first, char.IsDigit);
        Assert.Contains(first, character => !char.IsLetterOrDigit(character));
        Assert.NotEqual(first, second);
    }

    /// <summary>Откат возвращает ПК таким, каким он был до киоска, а не «по умолчанию».</summary>
    [Fact]
    public void Remove_PutsTheMachineBackAsItWas()
    {
        var fixture = new Fixture();
        fixture.Machine.Set(KioskSettings.WinlogonKey, "AutoAdminLogon", KioskRegistryValue.Of("0"));
        fixture.Machine.Set(KioskSettings.WinlogonKey, "DefaultUserName", KioskRegistryValue.Of("admin"));
        fixture.Provisioner.Provision(Host);

        fixture.Provisioner.Remove();

        Assert.Equal("0", fixture.Machine.Value(KioskSettings.WinlogonKey, "AutoAdminLogon").Text);
        Assert.Equal("admin", fixture.Machine.Value(KioskSettings.WinlogonKey, "DefaultUserName").Text);
        // Чего не было — того и нет: откат удаляет, а не пишет нули.
        Assert.True(fixture.Machine.Value(KioskSettings.WinlogonKey, "DefaultDomainName").IsAbsent);
        Assert.True(fixture.Machine.Value(@"SOFTWARE\Policies\Microsoft\Windows\Personalization", "NoLockScreen").IsAbsent);
        Assert.Null(fixture.Machine.AutologonPassword);
        Assert.Empty(fixture.Machine.Users);
        Assert.Null(fixture.AgentConfig.Sid);
        Assert.False(fixture.Provisioner.IsInstalled);
    }

    /// <summary>
    /// Повторная установка (мастер запустили ещё раз) помнит то, что было до первой: иначе
    /// «прежними» стали бы киосковые значения, и откат вернул бы киоск сам в себя.
    /// </summary>
    [Fact]
    public void ProvisioningTwice_StillRollsBackToWhatWasThereBeforeTheFirstTime()
    {
        var fixture = new Fixture();
        fixture.Machine.Set(KioskSettings.WinlogonKey, "AutoAdminLogon", KioskRegistryValue.Of("0"));
        fixture.Provisioner.Provision(Host);
        var firstPassword = fixture.Machine.AutologonPassword;

        fixture.Provisioner.Provision(Host);
        Assert.NotEqual(firstPassword, fixture.Machine.AutologonPassword);
        Assert.Single(fixture.Machine.Users);

        fixture.Provisioner.Remove();
        Assert.Equal("0", fixture.Machine.Value(KioskSettings.WinlogonKey, "AutoAdminLogon").Text);
    }

    [Fact]
    public void Remove_WithoutAKiosk_TouchesNothing()
    {
        var fixture = new Fixture();
        fixture.Machine.Set(KioskSettings.WinlogonKey, "AutoAdminLogon", KioskRegistryValue.Of("1"));

        fixture.Provisioner.Remove();

        Assert.Equal("1", fixture.Machine.Value(KioskSettings.WinlogonKey, "AutoAdminLogon").Text);
    }

    private sealed class Fixture
    {
        public Fixture()
        {
            Provisioner = new KioskProvisioner(Machine, State, AgentConfig);
        }

        public FakeMachine Machine { get; } = new();

        public MemoryState State { get; } = new();

        public RecordingAgentConfig AgentConfig { get; } = new();

        public KioskProvisioner Provisioner { get; }
    }

    internal sealed class FakeMachine : IKioskMachine
    {
        public const string PlayerSid = "S-1-5-21-1000-2000-3000-1001";

        private readonly Dictionary<(string Key, string Name), KioskRegistryValue> machine = new();

        public List<(string Name, string Password)> Users { get; } = [];

        public Dictionary<(string Sid, string Key, string Name), string> UserValues { get; } = new();

        public string? AutologonPassword { get; private set; }

        public KioskRegistryValue Value(string key, string name) => ReadMachineValue(key, name);

        public void Set(string key, string name, KioskRegistryValue value) => machine[(key, name)] = value;

        public Exception? EnsureUserFailure { get; set; }

        public string EnsureUser(string userName, string password)
        {
            if (EnsureUserFailure is not null)
            {
                throw EnsureUserFailure;
            }

            Users.RemoveAll(user => user.Name == userName);
            Users.Add((userName, password));
            return PlayerSid;
        }

        public void DeleteUser(string userName, string sid) => Users.RemoveAll(user => user.Name == userName);

        public void StoreAutologonPassword(string? password) => AutologonPassword = password;

        public KioskRegistryValue ReadMachineValue(string key, string name) =>
            machine.TryGetValue((key, name), out var value) ? value : new KioskRegistryValue(null, null);

        public void WriteMachineValue(string key, string name, KioskRegistryValue value) => machine[(key, name)] = value;

        public void DeleteMachineValue(string key, string name) => machine.Remove((key, name));

        public void WriteUserValue(string sid, string key, string name, string value) => UserValues[(sid, key, name)] = value;
    }

    internal sealed class MemoryState : IKioskStateStore
    {
        private KioskState? state;

        public KioskState? Load() => state;

        public void Save(KioskState value) => state = value;

        public void Clear() => state = null;
    }

    internal sealed class RecordingAgentConfig : IKioskAgentConfig
    {
        public string? Sid { get; private set; }

        public void Write(string playerSid) => Sid = playerSid;

        public void Clear() => Sid = null;
    }
}
