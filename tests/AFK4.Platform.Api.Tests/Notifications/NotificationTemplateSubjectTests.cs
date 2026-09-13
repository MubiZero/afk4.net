using System.Text.RegularExpressions;
using AFK4.Platform.Api.Notifications;

namespace AFK4.Platform.Api.Tests.Notifications;

/// <summary>
/// Шаблоны писем как контракт с рантаймом. Рендерер бросает исключение на плейсхолдер без значения,
/// поэтому опечатка в токене — это не косметика, а письмо, которое не уйдёт вовсе; поймать это
/// должен тест, а не получатель.
/// </summary>
public sealed class NotificationTemplateSubjectTests
{
    private static readonly string[] Locales = ["ru", "en", "tg"];
    private static readonly Regex Placeholder = new(@"\{\{\s*(?<key>[\w.]+)\s*\}\}", RegexOptions.CultureInvariant);

    public static TheoryData<string, string> EveryTemplateAndLocale()
    {
        var data = new TheoryData<string, string>();
        foreach (var key in NotificationTemplateKeys.All)
        {
            foreach (var locale in Locales)
            {
                data.Add(key, locale);
            }
        }

        return data;
    }

    [Theory]
    [MemberData(nameof(EveryTemplateAndLocale))]
    public void Template_OnlyUsesTokensTheRuntimeSupplies(string templateKey, string locale)
    {
        var provider = new EmbeddedTemplateProvider("ru");
        var template = provider.Get(templateKey, locale);
        var allowed = NotificationTemplateTokens.ByTemplateKey[templateKey];

        foreach (var part in new[] { template.Subject, template.BodyText, template.BodyHtml })
        {
            foreach (Match match in Placeholder.Matches(part))
            {
                var token = match.Groups["key"].Value;
                Assert.True(
                    allowed.Contains(token),
                    $"Шаблон '{templateKey}' ({locale}) ссылается на токен '{token}', которого рантайм не передаёт.");
            }
        }
    }

    // Бренд в теме пишется строчными: SpamAssassin считает тему из латиницы в верхнем регистре
    // набранной капсом (кириллицу за буквы он не принимает) и снимает полбалла с каждого письма.
    [Theory]
    [MemberData(nameof(EveryTemplateAndLocale))]
    public void Subject_DoesNotShoutTheBrand(string templateKey, string locale)
    {
        var subject = new EmbeddedTemplateProvider("ru").Get(templateKey, locale).Subject;

        Assert.DoesNotContain("AFK4.NET", subject, StringComparison.Ordinal);
    }

    [Theory]
    [MemberData(nameof(EveryTemplateAndLocale))]
    public void Subject_KeepsSecretsOutOfTheSubjectLine(string templateKey, string locale)
    {
        var subject = new EmbeddedTemplateProvider("ru").Get(templateKey, locale).Subject;

        Assert.DoesNotContain("{{code}}", subject, StringComparison.Ordinal);
    }
}
