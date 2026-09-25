using AFK4.Agent.Service.Protection;
using AFK4.Shared.Contracts.Devices;

namespace AFK4.Agent.Service.Tests.Protection;

/// <summary>Профиль защиты → записи реестра (спека оболочки, §6.3 и §6.2).</summary>
public sealed class ProtectionPolicyTests
{
    private static readonly ProtectionProfileDto Everything = new(
        7, true, true, true, true, ["D", "e"], ["casino.example", "*.bet.example"], []);

    [Fact]
    public void TheKioskBaseline_IsAlwaysOn_AndCutsTheCtrlAltDelMenu()
    {
        var baseline = ProtectionPolicy.Plan(Everything with { BlockRemovableStorage = false })
            .Single(item => item.Name == ProtectionItemNames.KioskBaseline);

        Assert.True(baseline.Enabled);
        Assert.Equal(
            ["DisableLockWorkstation", "DisableChangePassword", "HideFastUserSwitching", "NoLogoff"],
            baseline.Writes.Select(write => write.Name));
    }

    [Fact]
    public void EveryClubRule_MapsToTheWindowsPolicyTheSpecNames()
    {
        var writes = ProtectionPolicy.Plan(Everything).SelectMany(item => item.Writes).ToList();

        Assert.Contains(writes, write => write.Key == ProtectionPolicy.RemovableStorage && write.Name == "Deny_All" && write.Number == 1);
        Assert.Contains(writes, write => write.Key == ProtectionPolicy.Chrome && write.Name == "DownloadRestrictions" && write.Number == 3);
        Assert.Contains(writes, write => write.Key == ProtectionPolicy.Edge && write.Name == "DownloadRestrictions" && write.Number == 3);
        Assert.Contains(writes, write => write.Key == ProtectionPolicy.Chrome && write.Name == "IncognitoModeAvailability" && write.Number == 1);
        Assert.Contains(writes, write => write.Key == ProtectionPolicy.Edge && write.Name == "InPrivateModeAvailability" && write.Number == 1);
        Assert.Contains(writes, write => write.Key == ProtectionPolicy.ExplorerPolicies && write.Name == "NoRun" && write.Number == 1);
        Assert.Contains(writes, write => write.Key == ProtectionPolicy.Chrome && write.Name == "URLBlocklist" && write.List!.SequenceEqual(Everything.UrlBlocklist));
    }

    /// <summary>§6.3: NoDrives прячет диск только в Проводнике — и отчёт должен сказать именно это.</summary>
    [Fact]
    public void HiddenDrives_AreReportedAsExplorerOnly_NotAsBlocked()
    {
        var drives = ProtectionPolicy.Plan(Everything).Single(item => item.Name == ProtectionItemNames.HiddenDrives);

        Assert.Equal(ProtectionItemStatusNames.ExplorerOnly, drives.AppliedStatus);
        Assert.Equal(0b11000, drives.Writes.Single().Number);
    }

    [Fact]
    public void TheDriveMask_HasOneBitPerLetter()
    {
        Assert.Equal(1, ProtectionPolicy.DriveMask(["A"]));
        Assert.Equal(1 << 25, ProtectionPolicy.DriveMask(["z"]));
        Assert.Equal(0b100, ProtectionPolicy.DriveMask(["C", "c", "not-a-drive"]));
    }

    [Fact]
    public void AnEmptyProfile_EnablesOnlyTheBaseline()
    {
        var enabled = ProtectionPolicy.Plan(new ProtectionProfileDto(0, false, false, false, false, [], [], []))
            .Where(item => item.Enabled)
            .Select(item => item.Name);

        Assert.Equal([ProtectionItemNames.KioskBaseline], enabled);
    }
}
