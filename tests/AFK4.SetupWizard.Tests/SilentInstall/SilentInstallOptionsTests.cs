using AFK4.SetupWizard.Core.SilentInstall;

namespace AFK4.SetupWizard.Tests.SilentInstall;

public sealed class SilentInstallOptionsTests
{
    [Fact]
    public void WithoutACode_ItIsTheOrdinaryWizard()
    {
        var parse = SilentInstallOptions.Parse([]);

        Assert.False(parse.Requested);
        Assert.Null(parse.Options);
    }

    [Theory]
    [InlineData("--install-code", "7KQ2-M9XD-4TPV-HB3R", "--seat", "PC-07")]
    [InlineData("--install-code=7KQ2-M9XD-4TPV-HB3R", "--seat=PC-07", null, null)]
    public void ReadsTheCodeAndTheSeat(string first, string second, string? third, string? fourth)
    {
        string[] arguments = third is null ? [first, second] : [first, second, third, fourth!];

        var parse = SilentInstallOptions.Parse(arguments);

        Assert.True(parse.Requested);
        Assert.Equal(new SilentInstallOptions("7KQ2-M9XD-4TPV-HB3R", "PC-07"), parse.Options);
    }

    // Установщик передаёт место всегда, пустое — когда техник его не назвал.
    [Fact]
    public void AnEmptySeat_MeansTheSeatIsFoundByThePcName()
    {
        var parse = SilentInstallOptions.Parse(["--install-code", "7KQ2-M9XD-4TPV-HB3R", "--seat", ""]);

        Assert.Null(parse.Options!.SeatName);
    }

    // Код был, но пустой: окно открывать некому — это тоже тихая установка, только неудачная.
    [Theory]
    [InlineData("--install-code")]
    [InlineData("--install-code=")]
    public void AMissingCode_IsStillSilent_ButRefused(string argument)
    {
        var parse = SilentInstallOptions.Parse([argument, "--seat", "PC-07"]);

        Assert.True(parse.Requested);
        Assert.Null(parse.Options);
        Assert.NotNull(parse.Error);
    }
}
