using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using AFK4.Platform.Api.Data;
using AFK4.Shared.Contracts.Platform.Billing;
using AFK4.Shared.Contracts.Platform.Organizations;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace AFK4.Platform.Api.Tests.Platform;

/// <summary>
/// Отказы платформенной панели приезжают машинным именем.
///
/// Текст отказа сервер пишет по-английски и именами своих полей, а панель работает на трёх
/// языках: показать такой текст нельзя. Без кода панель показывала одно «не удалось сохранить
/// изменения» и на «счёт уже оплачен», и на «нет прав», и на обрыв сети — три разные починки под
/// одной надписью.
/// </summary>
public sealed class PlatformFailureCodeTests
{
    [Fact]
    public async Task CreateOrganization_WithATakenSlug_AnswersWithTheSlugTakenCode()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        await CreateOrganizationAsync(client, "twice");
        var duplicate = await CreateOrganizationAsync(client, "twice");

        Assert.Equal(HttpStatusCode.Conflict, duplicate.StatusCode);
        Assert.Equal(PlatformErrorCodeNames.OrganizationSlugTaken, await ReadCodeAsync(duplicate));
    }

    // Счёт за период уже выставлен — обычное дело, когда его выставили вручную и тут же нажали
    // «Создать счёт». Панель должна сказать именно это, а не «операция не удалась».
    [Fact]
    public async Task GenerateInvoice_ForAnAlreadyBilledPeriod_AnswersWithTheAlreadyBilledCode()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateOrganizationIdAsync(client, "billed");

        var first = await client.PostAsync($"/api/platform/organizations/{organizationId:D}/invoices/generate", content: null);
        Assert.Equal(HttpStatusCode.OK, first.StatusCode);

        // Выставленный счёт двигает период подписки вперёд. Возвращаем период назад — это и есть
        // «за текущий период счёт уже выставлен»: так выглядит клуб, которому счёт выписали
        // вручную, а период ещё не закончился.
        await using (var scope = factory.Services.CreateAsyncScope())
        {
            var dbContext = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
            var subscription = await dbContext.OrganizationSubscriptions
                .SingleAsync(row => row.OrganizationId == organizationId);
            var issued = await dbContext.Invoices.SingleAsync(row => row.OrganizationId == organizationId);
            subscription.CurrentPeriodStartUtc = issued.PeriodStartUtc;
            subscription.CurrentPeriodEndUtc = issued.PeriodEndUtc;
            await dbContext.SaveChangesAsync();
        }

        var second = await client.PostAsync($"/api/platform/organizations/{organizationId:D}/invoices/generate", content: null);

        Assert.Equal(HttpStatusCode.Conflict, second.StatusCode);
        Assert.Equal(PlatformErrorCodeNames.InvoicePeriodAlreadyBilled, await ReadCodeAsync(second));
    }

    [Fact]
    public async Task MarkInvoicePaid_Twice_AnswersWithTheAlreadyPaidCode()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateOrganizationIdAsync(client, "paid");

        var generated = await client.PostAsync($"/api/platform/organizations/{organizationId:D}/invoices/generate", content: null);
        var invoiceId = (await generated.Content.ReadFromJsonAsync<JsonElement>()).GetProperty("invoiceId").GetGuid();

        await MarkPaidAsync(client, invoiceId);
        var again = await MarkPaidAsync(client, invoiceId);

        Assert.Equal(HttpStatusCode.Conflict, again.StatusCode);
        Assert.Equal(PlatformErrorCodeNames.InvoiceAlreadyPaid, await ReadCodeAsync(again));
    }

    // Форма подписки — семь полей, и «не удалось сохранить» не подсказывает, какое из них не то.
    [Fact]
    public async Task UpdateSubscription_WithAnImpossiblePeriod_AnswersWithThePeriodCode()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateOrganizationIdAsync(client, "period");

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/subscription",
            new { CurrentPeriodEndUtc = DateTimeOffset.Parse("2020-01-01T00:00:00Z") });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(PlatformErrorCodeNames.SubscriptionPeriodEndNotAfterStart, await ReadCodeAsync(response));
    }

    [Fact]
    public async Task UpdateSubscription_WithAnUnknownPlan_AnswersWithThePlanNotFoundCode()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);
        var organizationId = await CreateOrganizationIdAsync(client, "plan");

        var response = await client.PatchAsJsonAsync(
            $"/api/platform/organizations/{organizationId:D}/subscription",
            new { PlanCode = "no-such-plan" });

        Assert.Equal(HttpStatusCode.BadRequest, response.StatusCode);
        Assert.Equal(PlatformErrorCodeNames.SubscriptionPlanNotFound, await ReadCodeAsync(response));
    }

    // Английская фраза остаётся рядом с кодом: её читают в журналах и в уже написанных проверках.
    [Fact]
    public async Task Failures_KeepTheHumanReadableReasonNextToTheCode()
    {
        await using var factory = new PlatformApiFactory();
        using var client = factory.CreateClient();
        await PlatformAdminTestHelper.AuthorizeAsAsync(factory, client);

        await CreateOrganizationAsync(client, "both");
        var duplicate = await CreateOrganizationAsync(client, "both");

        using var document = JsonDocument.Parse(await duplicate.Content.ReadAsStringAsync());
        Assert.Contains("already in use", document.RootElement.GetProperty("error").GetString());
        Assert.Equal(PlatformErrorCodeNames.OrganizationSlugTaken, document.RootElement.GetProperty("code").GetString());
    }

    private static Task<HttpResponseMessage> CreateOrganizationAsync(HttpClient client, string slug) =>
        client.PostAsJsonAsync(
            "/api/platform/organizations",
            new CreateOrganizationRequest(
                OrganizationSlug: slug,
                OrganizationName: $"Клуб {slug}",
                BranchSlug: "main",
                BranchName: "Главный",
                BranchCity: "Душанбе",
                PlanCode: "starter",
                SubscriptionStatus: "active",
                Limits: null,
                OwnerUserName: null,
                OwnerDisplayName: null,
                OrganizationOwnerInviteLifetime: null));

    private static async Task<Guid> CreateOrganizationIdAsync(HttpClient client, string slug)
    {
        var response = await CreateOrganizationAsync(client, slug);
        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var body = await response.Content.ReadFromJsonAsync<JsonElement>();
        return body.GetProperty("organization").GetProperty("organizationId").GetGuid();
    }

    private static Task<HttpResponseMessage> MarkPaidAsync(HttpClient client, Guid invoiceId) =>
        client.PostAsJsonAsync($"/api/platform/invoices/{invoiceId:D}/mark-paid", new { Reference = "ref" });

    private static async Task<string?> ReadCodeAsync(HttpResponseMessage response)
    {
        using var document = JsonDocument.Parse(await response.Content.ReadAsStringAsync());
        return document.RootElement.TryGetProperty("code", out var code) && code.ValueKind == JsonValueKind.String
            ? code.GetString()
            : null;
    }
}
