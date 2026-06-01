using AFK4.Platform.Api.Notifications;

namespace AFK4.Platform.Api.Tests.Notifications;

public sealed class NotificationRendererTests
{
    private static readonly INotificationRenderer Renderer = new NotificationRenderer();

    [Fact]
    public void Render_SubstitutesTokensInSubjectTextAndHtml()
    {
        var template = new NotificationTemplate(
            Subject: "Hello {{name}}",
            BodyText: "Your code is {{code}}.",
            BodyHtml: "<p>Your code is {{code}}.</p>");
        var tokens = new Dictionary<string, string> { ["name"] = "Sam", ["code"] = "1234" };

        var rendered = Renderer.Render(template, tokens);

        Assert.Equal("Hello Sam", rendered.Subject);
        Assert.Equal("Your code is 1234.", rendered.BodyText);
        Assert.Equal("<p>Your code is 1234.</p>", rendered.BodyHtml);
    }

    [Fact]
    public void Render_HtmlEscapesInterpolatedValuesInHtmlBodyOnly()
    {
        var template = new NotificationTemplate(
            Subject: "Re: {{title}}",
            BodyText: "Hi {{title}}",
            BodyHtml: "<p>Hi {{title}}</p>");
        var tokens = new Dictionary<string, string> { ["title"] = "<b>&you</b>" };

        var rendered = Renderer.Render(template, tokens);

        Assert.Equal("Re: <b>&you</b>", rendered.Subject);
        Assert.Equal("Hi <b>&you</b>", rendered.BodyText);
        Assert.Equal("<p>Hi &lt;b&gt;&amp;you&lt;/b&gt;</p>", rendered.BodyHtml);
    }

    [Fact]
    public void Render_ReplacesAllOccurrencesOfAToken()
    {
        var template = new NotificationTemplate("{{x}}-{{x}}", "{{x}}{{x}}", "{{x}}");

        var rendered = Renderer.Render(template, new Dictionary<string, string> { ["x"] = "9" });

        Assert.Equal("9-9", rendered.Subject);
        Assert.Equal("99", rendered.BodyText);
        Assert.Equal("9", rendered.BodyHtml);
    }

    [Fact]
    public void Render_IgnoresExtraTokensNotReferencedByTemplate()
    {
        var template = new NotificationTemplate("Hi {{name}}", "Hi {{name}}", "<p>Hi {{name}}</p>");
        var tokens = new Dictionary<string, string> { ["name"] = "Sam", ["unused"] = "x" };

        var rendered = Renderer.Render(template, tokens);

        Assert.Equal("Hi Sam", rendered.Subject);
    }

    [Fact]
    public void Render_ThrowsWhenTemplatePlaceholderHasNoToken()
    {
        var template = new NotificationTemplate("Hi {{missing}}", string.Empty, string.Empty);

        var exception = Assert.Throws<InvalidOperationException>(() =>
            Renderer.Render(template, new Dictionary<string, string>()));

        Assert.Contains("missing", exception.Message, StringComparison.Ordinal);
    }
}
