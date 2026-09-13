using AFK4.Platform.Api.Media;

namespace AFK4.Platform.Api.Tests;

/// <summary>
/// Ненастроенное хранилище — самый вероятный отказ загрузки на новой среде. Он должен называть
/// себя: «не удалось загрузить файл» отправляет искать проблему в файле, которой в нём нет.
/// </summary>
public sealed class MediaStorageConfigurationTests
{
    private static MediaOptions.S3Options Configured() => new()
    {
        Endpoint = "https://updates.afk4.net",
        Bucket = "afk4-media-staging",
        AccessKey = "key",
        SecretKey = "secret",
        PublicBaseUri = "https://updates.afk4.net/afk4-media-staging",
    };

    [Fact]
    public void FullySpecifiedStorage_IsConfigured()
    {
        Assert.True(Configured().IsConfigured);
    }

    [Fact]
    public void EmptyStorage_IsNotConfigured()
    {
        Assert.False(new MediaOptions.S3Options().IsConfigured);
    }

    // Каждое из полей обязательно: без публичного адреса, например, файл сохранится, а ссылка на
    // него будет битой — и это хуже честного отказа.
    [Theory]
    [InlineData("Endpoint")]
    [InlineData("Bucket")]
    [InlineData("AccessKey")]
    [InlineData("SecretKey")]
    [InlineData("PublicBaseUri")]
    public void MissingAnySingleField_IsNotConfigured(string missingField)
    {
        var options = Configured();
        typeof(MediaOptions.S3Options).GetProperty(missingField)!.SetValue(options, "   ");

        Assert.False(options.IsConfigured);
    }
}
