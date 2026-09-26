using AFK4.Player.Shell.Workstation;

namespace AFK4.Player.Shell.Tests.Workstation;

/// <summary>Раскладка называется по языку: британская английская — тоже «EN».</summary>
public sealed class KeyboardLayoutsTests
{
    [Theory]
    [InlineData(0x0419, "RU")]
    [InlineData(0x0409, "EN")]
    [InlineData(0x0809, "EN")]
    [InlineData(0x0428, "TG")]
    [InlineData(0x0407, null)]
    public void TheLabel_ComesFromTheLanguage(int languageId, string? label)
    {
        Assert.Equal(label, KeyboardLayouts.LabelFor((ushort)languageId));
    }

    [Theory]
    [InlineData("RU", "00000419")]
    [InlineData("EN", "00000409")]
    [InlineData("TG", "00000428")]
    [InlineData("DE", null)]
    [InlineData(null, null)]
    public void OnlyTheClubLanguages_AreOffered(string? label, string? klid)
    {
        Assert.Equal(klid, KeyboardLayouts.KlidFor(label));
    }
}
