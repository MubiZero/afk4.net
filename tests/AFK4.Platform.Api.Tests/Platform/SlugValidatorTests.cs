using AFK4.Platform.Api.Platform.Tenancy;

namespace AFK4.Platform.Api.Tests.Platform;

public sealed class SlugValidatorTests
{
    [Theory]
    [InlineData("  Demo-Org  ", "demo-org")]
    [InlineData("DEMO", "demo")]
    [InlineData("demo-org-123", "demo-org-123")]
    [InlineData(null, "")]
    [InlineData("", "")]
    public void Normalize_TrimsAndLowercases(string? input, string expected)
    {
        Assert.Equal(expected, SlugValidator.Normalize(input));
    }

    [Theory]
    [InlineData("demo-org")]
    [InlineData("dem")]
    [InlineData("a-b-c-d")]
    [InlineData("abc123def")]
    [InlineData("ru1-club")]
    [InlineData("123")]
    public void Validate_AcceptsConformingSlugs(string normalized)
    {
        Assert.Null(SlugValidator.Validate(normalized, "Slug"));
    }

    [Theory]
    [InlineData("", "required")]
    [InlineData("ab", "at least")]
    [InlineData("-leading", "lowercase")]
    [InlineData("trailing-", "lowercase")]
    [InlineData("UPPER", "lowercase")]
    [InlineData("under_score", "lowercase")]
    [InlineData("double--hyphen", "lowercase")]
    [InlineData("white space", "lowercase")]
    public void Validate_RejectsNonConformingSlugs(string normalized, string expectedErrorFragment)
    {
        var error = SlugValidator.Validate(normalized, "Slug");
        Assert.NotNull(error);
        Assert.Contains(expectedErrorFragment, error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_RejectsTooLongSlugs()
    {
        var tooLong = new string('a', SlugValidator.MaxLength + 1);
        var error = SlugValidator.Validate(tooLong, "Slug");
        Assert.NotNull(error);
        Assert.Contains("at most", error, StringComparison.OrdinalIgnoreCase);
    }

    [Fact]
    public void Validate_AcceptsMaxLengthSlug()
    {
        var slug = new string('a', SlugValidator.MaxLength);
        Assert.Null(SlugValidator.Validate(slug, "Slug"));
    }
}
