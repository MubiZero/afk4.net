using System.Text.Json;
using AFK4.SetupWizard.Core.Kiosk;

namespace AFK4.SetupWizard.Tests.Kiosk;

public sealed class KioskFilesTests : IDisposable
{
    private readonly string directory = Path.Combine(Path.GetTempPath(), "afk4-kiosk-" + Guid.NewGuid().ToString("N"));

    /// <summary>Агент читает SID из того же раздела «Agent», что и остальную настройку.</summary>
    [Fact]
    public void TheAgentConfig_CarriesThePlayerSidUnderTheAgentSection_AndGoesAwayOnRollback()
    {
        var path = Path.Combine(directory, "kiosk.json");
        var config = new FileKioskAgentConfig(path, restrictAccess: false);

        config.Write("S-1-5-21-1-2-3-1001");
        using (var document = JsonDocument.Parse(File.ReadAllText(path)))
        {
            Assert.Equal("S-1-5-21-1-2-3-1001", document.RootElement.GetProperty("Agent").GetProperty("ShellPipeClientSid").GetString());
        }

        config.Clear();
        Assert.False(File.Exists(path));
    }

    [Fact]
    public void TheState_SurvivesARestartOfTheWizard()
    {
        var path = Path.Combine(directory, "kiosk-state.json");
        var state = new KioskState(
            "S-1-5-21-1-2-3-1001",
            [new KioskMachineSetting(KioskSettings.WinlogonKey, "AutoAdminLogon", KioskRegistryValue.Of("0")),
             new KioskMachineSetting(@"SOFTWARE\Policies\Microsoft\Windows\Personalization", "NoLockScreen", new KioskRegistryValue(null, null))]);

        new FileKioskStateStore(path, restrictAccess: false).Save(state);
        var loaded = new FileKioskStateStore(path, restrictAccess: false).Load();

        Assert.NotNull(loaded);
        Assert.Equal(state.Sid, loaded.Sid);
        Assert.Equal(state.Previous, loaded.Previous);
    }

    public void Dispose()
    {
        if (Directory.Exists(directory))
        {
            Directory.Delete(directory, recursive: true);
        }
    }
}
