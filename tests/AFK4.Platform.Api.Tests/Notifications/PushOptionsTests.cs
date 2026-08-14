using AFK4.Platform.Api.Notifications;
using Xunit;

namespace AFK4.Platform.Api.Tests.Notifications;

/// Ключ переносят из служебного файла Firebase в переменную окружения руками, и переносы строк
/// в нём выживают по-разному. Разбираться в base64 глазами человек не должен.
public sealed class PushOptionsTests
{
    private const string Body = "MIIEvQIBADANBgkqhkiG9w0BAQEFAASCBKcwggSjAgEAAoIBAQC2AW3eOJ0099Cl";

    [Fact]
    public void NormalizedPrivateKey_EscapedNewlines_BecomeRealOnes()
    {
        var options = new PushOptions
        {
            PrivateKey = $"-----BEGIN PRIVATE KEY-----\\n{Body}\\n-----END PRIVATE KEY-----\\n",
        };

        Assert.Equal(
            $"-----BEGIN PRIVATE KEY-----\n{Body}\n-----END PRIVATE KEY-----",
            options.NormalizedPrivateKey);
    }

    /// Ровно тот случай, который приходит из формы: края поправлены, середина осталась как была.
    [Fact]
    public void NormalizedPrivateKey_MixedNewlines_AreBroughtToOneKind()
    {
        var options = new PushOptions
        {
            PrivateKey = $"-----BEGIN PRIVATE KEY-----\n{Body}\\n{Body}\n-----END PRIVATE KEY-----",
        };

        Assert.Equal(
            $"-----BEGIN PRIVATE KEY-----\n{Body}\n{Body}\n-----END PRIVATE KEY-----",
            options.NormalizedPrivateKey);
    }

    [Fact]
    public void NormalizedPrivateKey_WindowsNewlines_BecomeUnixOnes()
    {
        var options = new PushOptions
        {
            PrivateKey = $"-----BEGIN PRIVATE KEY-----\r\n{Body}\r\n-----END PRIVATE KEY-----",
        };

        Assert.DoesNotContain('\r', options.NormalizedPrivateKey);
    }

    [Fact]
    public void IsConfigured_WithoutKeys_IsFalse()
    {
        Assert.False(new PushOptions().IsConfigured);
        Assert.False(new PushOptions { ProjectId = "afk4-net" }.IsConfigured);
    }

    [Fact]
    public void IsConfigured_WithEveryField_IsTrue()
    {
        var options = new PushOptions
        {
            ProjectId = "afk4-net",
            ClientEmail = "firebase-adminsdk@afk4-net.iam.gserviceaccount.com",
            PrivateKey = "-----BEGIN PRIVATE KEY-----\nkey\n-----END PRIVATE KEY-----",
        };

        Assert.True(options.IsConfigured);
    }
}
