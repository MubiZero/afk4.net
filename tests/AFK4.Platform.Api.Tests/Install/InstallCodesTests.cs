using AFK4.Platform.Api.Install;

namespace AFK4.Platform.Api.Tests.Install;

public sealed class InstallCodesTests
{
    [Fact]
    public void Create_GivesFourGroupsOfFourReadableSymbols()
    {
        var code = InstallCodes.Create();

        Assert.Matches("^[0-9A-HJKMNP-TV-Z]{4}(-[0-9A-HJKMNP-TV-Z]{4}){3}$", code);
        Assert.NotEqual(code, InstallCodes.Create());
    }

    // Код переписывают с экрана и диктуют по телефону: регистр, дефисы и пробелы не важны, а O, I
    // и L — это 0 и 1, которых в алфавите нет.
    [Theory]
    [InlineData("7kq2-m9xd-4tpv-hb3r", "7KQ2M9XD4TPVHB3R")]
    [InlineData("7KQ2 M9XD 4TPV HB3R", "7KQ2M9XD4TPVHB3R")]
    [InlineData("OOOO-IIII-LLLL-0000", "0000111111110000")]
    public void Normalize_ForgivesHowPeopleTypeIt(string typed, string expected)
    {
        Assert.Equal(expected, InstallCodes.Normalize(typed));
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("7KQ2-M9XD-4TPV")]
    [InlineData("7KQ2-M9XD-4TPV-HB3R-0")]
    [InlineData("7KQ2-M9XD-4TPV-HB3U")]
    [InlineData("7KQ2-M9XD-4TPV-HB3Ж")]
    public void Normalize_RefusesWhatIsNotACode(string? typed)
    {
        Assert.Null(InstallCodes.Normalize(typed));
    }

    [Fact]
    public void Hash_IsTheSameForEveryWayOfTypingTheCode()
    {
        var code = InstallCodes.Create();

        Assert.Equal(
            InstallCodes.Hash(InstallCodes.Normalize(code)!),
            InstallCodes.Hash(InstallCodes.Normalize(code.ToLowerInvariant().Replace('-', ' '))!));
    }
}
