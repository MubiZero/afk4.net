# Eskhata Merchant — приём платежей: Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

> **Статус на 24.08.2026:** Phase 1 (бэкенд money-core) сделана и влита. Task 8 — живая сверка
> порядка хеша об тестовый контур банка — выполнен: порядок в коде верен, `posId="0"` принят.
> Phase 2 переписана под приложение на Flutter (Customer.Web перестал быть клиентом игрока), см.
> `2026-08-24-online-topup-in-app.md`. Phase 3 (QR на экране игрового ПК) не начата.

**Goal:** Построить онлайн-приём денег в кошелёк игрока через Eskhata Merchant (`orderTypeId=3`), переиспользуя money-path dcgate.

**Architecture:** Новый HTTP-клиент с SHA-256-подписью (`IEskhataMerchantClient`) создаёт заказ в ЭМ; ответ даёт `qr` (киоск) и `invoiceUrl` (→ deeplink для мобилы). Пополнение живёт в существующем `PaymentIntentEntity` (`Method="eskhata"`, `invoiceId=PaymentIntentId`). Публичный webhook перепроверяет статус через `/orders/status` и кредитит кошелёк тем же `CreditOnlineTopUpAsync` (идемпотентно по intent-id). Money-path dcgate не изменяется.

**Tech Stack:** .NET 10 minimal-API (Platform.Api), EF Core + миграции, xUnit + `PlatformApiFactory` (WebApplicationFactory), React/TS фронты на `bun test`, i18n через `@afk4/i18n` (ICU).

## Global Constraints

- Деньги внутри системы — **long minor units**; конвертация в major-string с 2 знаками только на границе клиента банка (`CultureInfo.InvariantCulture`).
- Валюта приёма — только **TJS** (`currency=972`).
- Hash-Key наружу **не отдаётся** (GET → только `hashKeySet`), пишется шифровано через `ISecretProtector`. Секреты не логировать, не хардкодить, не класть в тесты как прод-значения.
- `orderTypeId=3` (DynamicPos): `posId` **не передаётся** в create; `merchantId` обязателен; банк возвращает `posId` в ответе — сохраняем для последующих status/cancel/refund.
- Подпись — **голый `SHA256(concat(values) + "." + hashKey)`**, hex lowercase; НЕ HMAC.
- `X-CompanyId` = `Base64(companyId)`.
- Никаких AI-подписей в коммитах/коде.
- Money-path dcgate не трогаем; зачисление Eskhata — через существующий `CreditOnlineTopUpAsync`.
- Каждый .NET-таск заканчивается зелёным `dotnet test tests/AFK4.Platform.Api.Tests`; фронт-таск — зелёными `bun test` и `bun run build`.

---

## Фазировка

- **Phase 1 (этот план): бэкенд money-core.** Полностью тестируется API-тестами без UI. Даёт живой приём, проверяемый об тестовый endpoint банка.
- **Phase 2 и 3 (отдельные планы, пишутся ПОСЛЕ Phase 1):** фронты. Осознанно отложены: детальный код deeplink/QR-экранов писать против ответа банка, который мы валидируем только в Task 8 (порядок хеша типа 3 — эмпирическая неизвестность). Строить UI до этого — риск переделки. Файлы и скоуп зафиксированы в конце документа.

---

## File Structure (Phase 1)

- Create `src/AFK4.Platform.Api/Payments/Eskhata/EskhataSigner.cs` — чистая подпись + Base64 companyId.
- Create `src/AFK4.Platform.Api/Payments/Eskhata/IEskhataMerchantClient.cs` — интерфейс + result-DTO.
- Create `src/AFK4.Platform.Api/Payments/Eskhata/EskhataMerchantClient.cs` — HTTP-клиент (create/status).
- Create `src/AFK4.Platform.Api/Payments/Eskhata/IEskhataMerchantClientFactory.cs` + `EskhataMerchantClientFactory.cs` — сборка клиента из конфига org.
- Create `src/AFK4.Platform.Api/Endpoints/EskhataPaymentEndpoints.cs` — публичный webhook.
- Modify `src/AFK4.Platform.Api/Program.cs` — регистрация named HttpClient + фабрики + map webhook.
- Modify `src/AFK4.Platform.Api/Data/EskhataMerchantConfigEntity.cs` — `PosId` → `MerchantId` (+ nullable `PosId` как хранилище возврата банка не нужно на уровне конфига).
- Modify `src/AFK4.Platform.Api/Data/PaymentIntentEntity.cs` — `GatewayQrPayload`, `GatewayPosId`.
- Modify `src/AFK4.Platform.Api/Endpoints/EskhataConfigEndpoints.cs` — валидация `merchantId`.
- Modify `src/AFK4.Platform.Api/Endpoints/PlayerSelfServiceEndpoints.cs` — ветка `method=="eskhata"` + status-poll эндпоинт.
- Modify контракты `src/AFK4.Shared.Contracts/Payments/EskhataMerchantConfigDtos.cs`, `.../Players/PlayerTopUpIntentRequest.cs`, `PlayerTopUpIntentDto.cs`.
- Modify operator `src/AFK4.Operator.App.Web/src/api/clients/eskhataConfig.ts`, `management/destinations/payments/EskhataGatewayForm.tsx`, `devMockBackend.ts`, `locales/{ru,en,tg}.json`.
- Migrations: `AddEskhataMerchantId`, `AddPaymentIntentEskhataFields`.

---

### Task 1: Подпись Eskhata (чистая, unit-tested)

**Files:**
- Create: `src/AFK4.Platform.Api/Payments/Eskhata/EskhataSigner.cs`
- Test: `tests/AFK4.Platform.Api.Tests/EskhataSignerTests.cs`

**Interfaces:**
- Produces: `static class EskhataSigner { string BuildHash(IReadOnlyList<string> orderedValues, string hashKey); string CompanyIdHeader(string companyId); string FormatAmount(long minorUnits); }`

- [ ] **Step 1: Написать падающий тест по эталонному вектору из доки банка**

```csharp
using AFK4.Platform.Api.Payments.Eskhata;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class EskhataSignerTests
{
    // Канонический вектор из спеки Eskhata (раздел 2): orderTypeId=1.
    // Sha256("00116.00972description125581.56770fcaaed849dd8c80f41d5dd938e7)".
    [Fact]
    public void BuildHash_MatchesBankDocVector()
    {
        var values = new[] { "001", "16.00", "972", "description", "12558", "1" };
        var hash = EskhataSigner.BuildHash(values, "56770fcaaed849dd8c80f41d5dd938e7");
        Assert.Equal("9b9a46632e1dc5850d35bca1760479e6f0ccad290904f69cfc1723e89a1a6cc5", hash);
    }

    [Fact]
    public void CompanyIdHeader_IsBase64OfRawId()
    {
        Assert.Equal("YWJj", EskhataSigner.CompanyIdHeader("abc"));
    }

    [Theory]
    [InlineData(1600, "16.00")]
    [InlineData(99, "0.99")]
    [InlineData(100000, "1000.00")]
    public void FormatAmount_TwoDecimalsInvariant(long minor, string expected)
        => Assert.Equal(expected, EskhataSigner.FormatAmount(minor));
}
```

- [ ] **Step 2: Запустить — упадёт (класса нет)**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~EskhataSignerTests`
Expected: FAIL (does not compile / `EskhataSigner` not found).

- [ ] **Step 3: Реализовать**

```csharp
using System.Globalization;
using System.Security.Cryptography;
using System.Text;

namespace AFK4.Platform.Api.Payments.Eskhata;

// Подпись Merchant API: SHA-256 (НЕ HMAC, вопреки формулировке доки) от конкатенации
// значений скалярных параметров в порядке спецификации + "." + Hash-Key. Значения массивов
// (items) и сам hash в конкатенацию не входят.
public static class EskhataSigner
{
    public static string BuildHash(IReadOnlyList<string> orderedValues, string hashKey)
    {
        var payload = string.Concat(orderedValues) + "." + hashKey;
        return Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(payload))).ToLowerInvariant();
    }

    public static string CompanyIdHeader(string companyId) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes(companyId));

    // Minor units → major-string с ровно двумя знаками (и для тела, и для строки хеша).
    public static string FormatAmount(long minorUnits) =>
        (minorUnits / 100m).ToString("0.00", CultureInfo.InvariantCulture);
}
```

- [ ] **Step 4: Запустить — зелёно**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~EskhataSignerTests`
Expected: PASS (3 факта/теории).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Platform.Api/Payments/Eskhata/EskhataSigner.cs tests/AFK4.Platform.Api.Tests/EskhataSignerTests.cs
git commit -m "feat(payments): Eskhata SHA-256 signer + Base64 companyId (эталонный вектор доки)"
```

---

### Task 2: HTTP-клиент Eskhata Merchant + фабрика + DI

**Files:**
- Create: `src/AFK4.Platform.Api/Payments/Eskhata/IEskhataMerchantClient.cs`
- Create: `src/AFK4.Platform.Api/Payments/Eskhata/EskhataMerchantClient.cs`
- Create: `src/AFK4.Platform.Api/Payments/Eskhata/IEskhataMerchantClientFactory.cs`
- Create: `src/AFK4.Platform.Api/Payments/Eskhata/EskhataMerchantClientFactory.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (после блока dcgate, ~line 345)
- Test: `tests/AFK4.Platform.Api.Tests/EskhataMerchantClientTests.cs`

**Interfaces:**
- Consumes: `EskhataSigner` (Task 1), `ISecretProtector`, `PlatformDbContext`, `EskhataMerchantConfigEntity`.
- Produces:
  - `interface IEskhataMerchantClient { Task<EskhataCreateOrderResult> CreateOrderAsync(string invoiceId, long amountMinor, string currencyCode, string description, int merchantId, CancellationToken ct); Task<string?> GetOrderStatusAsync(string invoiceId, string orderId, long amountMinor, string currencyCode, int posId, CancellationToken ct); }`
  - `record EskhataCreateOrderResult(string OrderId, string OrderStatus, string? Qr, string? InvoiceUrl, int PosId);`
  - `interface IEskhataMerchantClientFactory { Task<IEskhataMerchantClient?> CreateForOrganizationAsync(Guid organizationId, CancellationToken ct); }`
  - `class EskhataMerchantClientFactory` с `public const string HttpClientName = "eskhata";`

- [ ] **Step 1: Написать падающий тест клиента (StubHandler)**

```csharp
using System.Net;
using System.Text.Json;
using AFK4.Platform.Api.Payments.Eskhata;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class EskhataMerchantClientTests
{
    private sealed class StubHandler : HttpMessageHandler
    {
        private readonly Func<HttpRequestMessage, HttpResponseMessage> responder;
        public string? LastBody { get; private set; }
        public HttpRequestMessage? LastRequest { get; private set; }
        public StubHandler(Func<HttpRequestMessage, HttpResponseMessage> r) => responder = r;
        protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage req, CancellationToken ct)
        {
            LastRequest = req;
            LastBody = req.Content is null ? null : await req.Content.ReadAsStringAsync(ct);
            return responder(req);
        }
    }

    private static EskhataMerchantClient Create(StubHandler h) =>
        new(new HttpClient(h) { BaseAddress = new Uri("https://em.example") },
            companyId: "918b6ea7-59fd-4e49-9481-9f2ca6a32b75",
            hashKey: "4f7fbbf60e8e4a3194042c55f474b40b");

    private static HttpResponseMessage OkCreate() => new(HttpStatusCode.OK)
    {
        Content = new StringContent("""
        {"status":true,"code":0,"message":"Успешно","data":{
          "posId":48741,"orderStatus":"NEW","invoiceId":"inv1",
          "orderId":"3818cdcccc6b4e8f8ff93bdc048a74e1",
          "qr":"0002010102...","invoiceUrl":"https://online3.eskhata.com:1444/api/v2.5/invoices/hlR2oH"}}
        """, System.Text.Encoding.UTF8, "application/json")
    };

    [Fact]
    public async Task CreateOrderAsync_SendsSignedType3Body_AndParsesQrAndUrl()
    {
        var handler = new StubHandler(_ => OkCreate());
        var client = Create(handler);

        var result = await client.CreateOrderAsync(
            invoiceId: "inv1", amountMinor: 5000, currencyCode: "972",
            description: "AFK4 wallet top-up", merchantId: 28652, CancellationToken.None);

        Assert.Equal("/merchant/api/v1/orders/create", handler.LastRequest!.RequestUri!.AbsolutePath);
        Assert.True(handler.LastRequest.Headers.Contains("X-CompanyId"));
        using var sent = JsonDocument.Parse(handler.LastBody!);
        var root = sent.RootElement;
        Assert.Equal(3, root.GetProperty("orderTypeId").GetInt32());
        Assert.Equal(28652, root.GetProperty("merchantId").GetInt32());
        Assert.False(root.TryGetProperty("posId", out _)); // тип 3: posId не шлём
        Assert.False(string.IsNullOrEmpty(root.GetProperty("hash").GetString()));

        Assert.Equal("3818cdcccc6b4e8f8ff93bdc048a74e1", result.OrderId);
        Assert.Equal("NEW", result.OrderStatus);
        Assert.False(string.IsNullOrEmpty(result.Qr));
        Assert.Contains("/invoices/hlR2oH", result.InvoiceUrl);
        Assert.Equal(48741, result.PosId);
    }

    [Fact]
    public async Task GetOrderStatusAsync_ReturnsOrderStatus()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""
            {"status":true,"code":0,"data":{"orderId":"o1","orderStatus":"COMPLETED","invoiceId":"inv1","posId":48741}}
            """, System.Text.Encoding.UTF8, "application/json")
        });
        var status = await Create(handler).GetOrderStatusAsync("inv1", "o1", 5000, "972", 48741, CancellationToken.None);
        Assert.Equal("COMPLETED", status);
    }

    [Fact]
    public async Task CreateOrderAsync_ThrowsWhenBankStatusFalse()
    {
        var handler = new StubHandler(_ => new HttpResponseMessage(HttpStatusCode.OK)
        {
            Content = new StringContent("""{"status":false,"code":-2,"message":"неверные параметры запроса","data":{}}""",
                System.Text.Encoding.UTF8, "application/json")
        });
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            Create(handler).CreateOrderAsync("inv1", 5000, "972", "d", 28652, CancellationToken.None));
    }
}
```

- [ ] **Step 2: Запустить — упадёт (нет клиента)**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~EskhataMerchantClientTests`
Expected: FAIL (does not compile).

- [ ] **Step 3: Реализовать интерфейс + result**

Файл `IEskhataMerchantClient.cs`:

```csharp
namespace AFK4.Platform.Api.Payments.Eskhata;

public interface IEskhataMerchantClient
{
    Task<EskhataCreateOrderResult> CreateOrderAsync(
        string invoiceId, long amountMinor, string currencyCode, string description,
        int merchantId, CancellationToken cancellationToken);

    // Возвращает orderStatus (NEW/IN PROCESS/COMPLETED/CANCELED/REFUNDED) или null при неуспехе банка.
    Task<string?> GetOrderStatusAsync(
        string invoiceId, string orderId, long amountMinor, string currencyCode, int posId,
        CancellationToken cancellationToken);
}

public sealed record EskhataCreateOrderResult(
    string OrderId, string OrderStatus, string? Qr, string? InvoiceUrl, int PosId);
```

- [ ] **Step 4: Реализовать клиент**

Файл `EskhataMerchantClient.cs`:

```csharp
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text.Json;

namespace AFK4.Platform.Api.Payments.Eskhata;

public sealed class EskhataMerchantClient : IEskhataMerchantClient
{
    private const int OrderTypeDynamicPos = 3;
    private readonly HttpClient httpClient;
    private readonly string companyId;
    private readonly string hashKey;

    public EskhataMerchantClient(HttpClient httpClient, string companyId, string hashKey)
    {
        this.httpClient = httpClient;
        this.companyId = companyId;
        this.hashKey = hashKey;
    }

    public async Task<EskhataCreateOrderResult> CreateOrderAsync(
        string invoiceId, long amountMinor, string currencyCode, string description,
        int merchantId, CancellationToken cancellationToken)
    {
        var amount = EskhataSigner.FormatAmount(amountMinor);
        // ⚠️ ПОРЯДОК ХЕША ДЛЯ orderTypeId=3 — ЭМПИРИЧЕСКАЯ НЕИЗВЕСТНОСТЬ (см. Task 8).
        // Базовая гипотеза: как в типе 1/2, но posId выпадает, merchantId встаёт после description.
        var hash = EskhataSigner.BuildHash(
            new[] { invoiceId, amount, currencyCode, description, merchantId.ToString(), OrderTypeDynamicPos.ToString() },
            hashKey);

        var body = new
        {
            hash,
            invoiceId,
            amount = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture),
            currency = currencyCode,
            description,
            merchantId,
            orderTypeId = OrderTypeDynamicPos
        };

        var data = await PostAsync("/merchant/api/v1/orders/create", body, cancellationToken);
        var orderId = GetString(data, "orderId") ?? throw new HttpRequestException("Eskhata: empty orderId");
        return new EskhataCreateOrderResult(
            orderId,
            GetString(data, "orderStatus") ?? "NEW",
            GetString(data, "qr"),
            GetString(data, "invoiceUrl") ?? GetString(data, "InvoiceUrl"),
            GetInt(data, "posId"));
    }

    public async Task<string?> GetOrderStatusAsync(
        string invoiceId, string orderId, long amountMinor, string currencyCode, int posId,
        CancellationToken cancellationToken)
    {
        var amount = EskhataSigner.FormatAmount(amountMinor);
        var hash = EskhataSigner.BuildHash(
            new[] { invoiceId, orderId, amount, currencyCode, posId.ToString() }, hashKey);
        var body = new
        {
            hash, invoiceId, orderId,
            amount = decimal.Parse(amount, System.Globalization.CultureInfo.InvariantCulture),
            currency = currencyCode, posId
        };
        try
        {
            var data = await PostAsync("/merchant/api/v1/orders/status", body, cancellationToken);
            return GetString(data, "orderStatus");
        }
        catch (HttpRequestException)
        {
            return null;
        }
    }

    private async Task<JsonElement> PostAsync(string path, object body, CancellationToken ct)
    {
        using var request = new HttpRequestMessage(HttpMethod.Post, path) { Content = JsonContent.Create(body) };
        request.Headers.TryAddWithoutValidation("X-CompanyId", EskhataSigner.CompanyIdHeader(companyId));
        request.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));

        using var response = await httpClient.SendAsync(request, ct);
        response.EnsureSuccessStatusCode();
        using var doc = JsonDocument.Parse(await response.Content.ReadAsStringAsync(ct));
        var root = doc.RootElement;
        if (!root.TryGetProperty("status", out var ok) || !ok.GetBoolean())
        {
            var msg = root.TryGetProperty("message", out var m) ? m.GetString() : "Eskhata request failed";
            throw new HttpRequestException($"Eskhata: {msg}");
        }
        return root.GetProperty("data").Clone();
    }

    private static string? GetString(JsonElement d, string name) =>
        d.TryGetProperty(name, out var v) && v.ValueKind == JsonValueKind.String ? v.GetString() : null;

    private static int GetInt(JsonElement d, string name) =>
        d.TryGetProperty(name, out var v) && v.TryGetInt32(out var i) ? i : 0;
}
```

- [ ] **Step 5: Реализовать фабрику + интерфейс**

Файл `IEskhataMerchantClientFactory.cs`:

```csharp
namespace AFK4.Platform.Api.Payments.Eskhata;

// Собирает клиент под org: читает EskhataMerchantConfig (BranchId==null), расшифровывает Hash-Key.
// Возвращает null, если конфиг отсутствует/неполон.
public interface IEskhataMerchantClientFactory
{
    Task<IEskhataMerchantClient?> CreateForOrganizationAsync(Guid organizationId, CancellationToken cancellationToken);
}
```

Файл `EskhataMerchantClientFactory.cs`:

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Security;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Payments.Eskhata;

public sealed class EskhataMerchantClientFactory(
    IHttpClientFactory httpClientFactory,
    PlatformDbContext db,
    ISecretProtector secretProtector) : IEskhataMerchantClientFactory
{
    public const string HttpClientName = "eskhata";

    public async Task<IEskhataMerchantClient?> CreateForOrganizationAsync(
        Guid organizationId, CancellationToken cancellationToken)
    {
        var config = await db.EskhataMerchantConfigs.AsNoTracking()
            .SingleOrDefaultAsync(c => c.OrganizationId == organizationId && c.BranchId == null, cancellationToken);
        if (config is null || string.IsNullOrEmpty(config.HashKeyEncrypted)
            || string.IsNullOrWhiteSpace(config.BaseUrl) || string.IsNullOrWhiteSpace(config.CompanyId))
        {
            return null;
        }

        string hashKey;
        try { hashKey = secretProtector.Unprotect(config.HashKeyEncrypted); }
        catch { return null; }

        var http = httpClientFactory.CreateClient(HttpClientName);
        http.BaseAddress = new Uri(config.BaseUrl);
        return new EskhataMerchantClient(http, config.CompanyId, hashKey);
    }
}
```

- [ ] **Step 6: Зарегистрировать в Program.cs (после строки 345, блок dcgate)**

```csharp
builder.Services.AddHttpClient(EskhataMerchantClientFactory.HttpClientName);
builder.Services.AddScoped<IEskhataMerchantClientFactory, EskhataMerchantClientFactory>();
```

(Фабрика — Scoped, т.к. держит `PlatformDbContext`. BaseAddress ставится на клиент из конфига per-org, поэтому named-client без базового адреса.)

- [ ] **Step 7: Запустить тест — зелёно**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~EskhataMerchantClientTests`
Expected: PASS (3 факта).

- [ ] **Step 8: Commit**

```bash
git add src/AFK4.Platform.Api/Payments/Eskhata/ src/AFK4.Platform.Api/Program.cs tests/AFK4.Platform.Api.Tests/EskhataMerchantClientTests.cs
git commit -m "feat(payments): клиент Eskhata Merchant (create/status тип 3) + фабрика per-org"
```

---

### Task 3: Конфиг `posId` → `merchantId` (бэкенд + оператор)

**Files:**
- Modify: `src/AFK4.Platform.Api/Data/EskhataMerchantConfigEntity.cs`
- Modify: `src/AFK4.Shared.Contracts/Payments/EskhataMerchantConfigDtos.cs`
- Modify: `src/AFK4.Platform.Api/Endpoints/EskhataConfigEndpoints.cs`
- Create migration: `AddEskhataMerchantId`
- Modify: `src/AFK4.Operator.App.Web/src/api/clients/eskhataConfig.ts`
- Modify: `src/AFK4.Operator.App.Web/src/management/destinations/payments/EskhataGatewayForm.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/devMockBackend.ts` (≈ lines 475, 884)
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Test: `tests/AFK4.Platform.Api.Tests/EskhataConfigEndpointsTests.cs` (обновить), `src/AFK4.Operator.App.Web/src/management/destinations/payments/EskhataGatewayForm.test.tsx` (обновить)

**Interfaces:**
- Consumes: —
- Produces: `EskhataMerchantConfigEntity.MerchantId (int)`; контракты с `MerchantId`; форма с полем «Merchant ID».

- [ ] **Step 1: Обновить упавший тест эндпоинта (валидация merchantId)**

В `EskhataConfigEndpointsTests.cs` заменить в теле запросов `PosId` → `MerchantId` и добавить факт: `merchantId <= 0` → 400. (Открыть файл, отразить новый контракт `UpdateEskhataMerchantConfigRequest(BaseUrl, CompanyId, MerchantId, HashKey)`; assert `configured` при валидном сохранении.)

- [ ] **Step 2: Запустить — упадёт (контракт ещё со старым PosId)**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~EskhataConfigEndpointsTests`
Expected: FAIL (does not compile / assert).

- [ ] **Step 3: Правки контрактов и сущности**

`EskhataMerchantConfigDtos.cs`:

```csharp
public sealed record EskhataMerchantConfigDto(
    string BaseUrl,
    string CompanyId,
    int MerchantId,
    bool HashKeySet,
    string Status);

public sealed record UpdateEskhataMerchantConfigRequest(
    string BaseUrl,
    string CompanyId,
    int MerchantId,
    string? HashKey);
```

`EskhataMerchantConfigEntity.cs`: заменить `public int PosId { get; set; }` на:

```csharp
    // Идентификатор торговой точки (merchantId) для orderTypeId=3 (DynamicPos).
    // posId при типе 3 не задаётся оператором — банк возвращает его в ответе на create.
    public int MerchantId { get; set; }
```

- [ ] **Step 4: Эндпоинт — валидация merchantId**

В `EskhataConfigEndpoints.cs`: заменить чтение/запись/валидацию `PosId` → `MerchantId` (DTO и `row.MerchantId = request.MerchantId;`, `ValidateRequest`: `request.MerchantId <= 0` → `errors["merchantId"] = ["Merchant ID must be a positive number."]`, audit-details `request.MerchantId`).

- [ ] **Step 5: Миграция**

```bash
dotnet ef migrations add AddEskhataMerchantId --project src/AFK4.Platform.Api --startup-project src/AFK4.Platform.Api
```

Проверить сгенерированную миграцию: rename/добавление колонки `MerchantId` (int, default 0), удаление `PosId`. (На Linux `dotnet ef` — см. env-quirks; при traps выполнить с `DOTNET_...` как принято в репо.)

- [ ] **Step 6: Оператор — клиент, форма, i18n, dev-mock**

`eskhataConfig.ts`: `posId` → `merchantId` в `EskhataConfigDto` и `UpdateEskhataConfigRequest`.

`EskhataGatewayForm.tsx`: state `posId`→`merchantId`; label `t('op.eskhata.posId')`→`t('op.eskhata.merchantId')`; `inputMode="numeric"`; в `update({...})` слать `merchantId: merchNumber`; валидность `Number.isInteger(merchNumber) && merchNumber > 0`.

`locales/*.json` — переименовать ключ и значения:

```json
// ru.json
"op.eskhata.merchantId": "Merchant ID",
"op.eskhata.invalid": "Заполните Base URL, Company ID и Merchant ID; при первом сохранении — Hash key.",
```
```json
// en.json
"op.eskhata.merchantId": "Merchant ID",
```
```json
// tg.json  (Merchant ID — латинский технический идентификатор, оставляем как есть; строку-инструкцию перевести на таджикский)
"op.eskhata.merchantId": "Merchant ID",
```
Удалить старый ключ `op.eskhata.posId` во всех трёх локалях. Затем регенерация:

```bash
cd packages/i18n && bun run gen
```

`devMockBackend.ts`: в `eskhataConfig()` и POST-обработчике `posId` → `merchantId`.

- [ ] **Step 7: Обновить фронт-тест формы**

`EskhataGatewayForm.test.tsx`: заменить обращения к полю «POS ID» на «Merchant ID» (по label/`op.eskhata.merchantId`).

- [ ] **Step 8: Прогнать все гейты**

Run:
```bash
dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~EskhataConfigEndpointsTests
cd src/AFK4.Operator.App.Web && bun test src/management/destinations/payments/EskhataGatewayForm.test.tsx && bun run build
```
Expected: PASS + зелёная сборка.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat(payments): конфиг Eskhata posId→merchantId (тип 3 DynamicPos) + миграция"
```

---

### Task 4: Колонки PaymentIntent под Eskhata

**Files:**
- Modify: `src/AFK4.Platform.Api/Data/PaymentIntentEntity.cs`
- Create migration: `AddPaymentIntentEskhataFields`

**Interfaces:**
- Produces: `PaymentIntentEntity.GatewayQrPayload (string?)`, `GatewayPosId (int?)`.

- [ ] **Step 1: Добавить nullable-колонки**

В `PaymentIntentEntity.cs` в блок dcgate-полей добавить:

```csharp
    // --- eskhata (orderTypeId=3) ---
    // Текст Единого QR из ответа create (для киоска/ПК).
    public string? GatewayQrPayload { get; set; }

    // posId, назначенный банком (нужен для последующих status/cancel/refund).
    public int? GatewayPosId { get; set; }
```

- [ ] **Step 2: Миграция**

```bash
dotnet ef migrations add AddPaymentIntentEskhataFields --project src/AFK4.Platform.Api --startup-project src/AFK4.Platform.Api
```

Проверить: две nullable-колонки, без изменения существующих.

- [ ] **Step 3: Сборка проходит**

Run: `dotnet build src/AFK4.Platform.Api`
Expected: PASS.

- [ ] **Step 4: Commit**

```bash
git add src/AFK4.Platform.Api/Data/PaymentIntentEntity.cs src/AFK4.Platform.Api/Data/Migrations/
git commit -m "feat(payments): PaymentIntent — GatewayQrPayload/GatewayPosId под Eskhata"
```

---

### Task 5: Ветка `method=="eskhata"` в top-up-intent + deeplink

**Files:**
- Modify: `src/AFK4.Shared.Contracts/Players/PlayerTopUpIntentRequest.cs` (комментарий методов)
- Modify: `src/AFK4.Shared.Contracts/Players/PlayerTopUpIntentDto.cs` (+ `Qr`, `DeepLink`)
- Modify: `src/AFK4.Platform.Api/Endpoints/PlayerSelfServiceEndpoints.cs` (≈ lines 220-319)
- Create: `src/AFK4.Platform.Api/Payments/Eskhata/EskhataDeepLink.cs` (чистый билдер)
- Test: `tests/AFK4.Platform.Api.Tests/EskhataTopUpIntentTests.cs`, `tests/AFK4.Platform.Api.Tests/EskhataDeepLinkTests.cs`

**Interfaces:**
- Consumes: `IEskhataMerchantClientFactory` (Task 2), `EskhataCreateOrderResult`, `PaymentIntentEntity.GatewayQrPayload/GatewayPosId` (Task 4).
- Produces: `PlayerTopUpIntentDto` с `Qr`, `DeepLink`; `static class EskhataDeepLink { string? FromInvoiceUrl(string? invoiceUrl); }`.

- [ ] **Step 1: Тест билдера deeplink**

```csharp
using AFK4.Platform.Api.Payments.Eskhata;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class EskhataDeepLinkTests
{
    [Fact]
    public void FromInvoiceUrl_BuildsSchemeFromLastSegment()
        => Assert.Equal("eskhata://pay/hlR2oH",
            EskhataDeepLink.FromInvoiceUrl("https://online3.eskhata.com:1444/api/v2.5/invoices/hlR2oH"));

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("not a url")]
    public void FromInvoiceUrl_ReturnsNullOnUnparseable(string? input)
        => Assert.Null(EskhataDeepLink.FromInvoiceUrl(input));
}
```

- [ ] **Step 2: Запустить — упадёт**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~EskhataDeepLinkTests`
Expected: FAIL.

- [ ] **Step 3: Реализовать билдер**

`EskhataDeepLink.cs`:

```csharp
namespace AFK4.Platform.Api.Payments.Eskhata;

// Ссылка на банковское приложение из hosted invoice URL: eskhata://pay/<ref>,
// где <ref> — последний сегмент пути (…/invoices/<ref>). null, если распарсить нельзя.
public static class EskhataDeepLink
{
    public static string? FromInvoiceUrl(string? invoiceUrl)
    {
        if (string.IsNullOrWhiteSpace(invoiceUrl)
            || !Uri.TryCreate(invoiceUrl, UriKind.Absolute, out var uri))
        {
            return null;
        }
        var segment = uri.Segments.LastOrDefault()?.Trim('/');
        return string.IsNullOrEmpty(segment) ? null : $"eskhata://pay/{segment}";
    }
}
```

- [ ] **Step 4: Расширить DTO**

`PlayerTopUpIntentDto.cs` — добавить два optional-поля в конец:

```csharp
    DateTimeOffset? GatewayExpiresAtUtc = null,
    string? Qr = null,
    string? DeepLink = null);
```

- [ ] **Step 5: Тест эндпоинта (eskhata-ветка) — с подменённой фабрикой**

```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Payments.Eskhata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class EskhataTopUpIntentTests
{
    private sealed class FakeEskhataClient : IEskhataMerchantClient
    {
        public Task<EskhataCreateOrderResult> CreateOrderAsync(string invoiceId, long amountMinor,
            string currencyCode, string description, int merchantId, CancellationToken ct)
            => Task.FromResult(new EskhataCreateOrderResult(
                "order-abc", "NEW", "QRTEXT",
                "https://online3.eskhata.com:1444/api/v2.5/invoices/hlR2oH", 48741));
        public Task<string?> GetOrderStatusAsync(string invoiceId, string orderId, long amountMinor,
            string currencyCode, int posId, CancellationToken ct) => Task.FromResult<string?>("COMPLETED");
    }

    private sealed class FakeFactory : IEskhataMerchantClientFactory
    {
        public Task<IEskhataMerchantClient?> CreateForOrganizationAsync(Guid orgId, CancellationToken ct)
            => Task.FromResult<IEskhataMerchantClient?>(new FakeEskhataClient());
    }

    [Fact]
    public async Task TopUpIntent_Eskhata_CreatesOrder_ReturnsQrAndDeepLink()
    {
        await using var factory = new PlatformApiFactory()
            .WithReplacedService<IEskhataMerchantClientFactory>(new FakeFactory());
        var playerToken = await PlayerAuthTestHelper.CreatePlayerAndTokenAsync(factory); // см. существующий helper в DcGateTopUpIntentTests
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", playerToken);

        var response = await client.PostAsJsonAsync("/api/me/wallet/top-up-intent",
            new { AmountMinorUnits = 5000L, CurrencyCode = "TJS", Method = "eskhata" });

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var dto = await response.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("eskhata", dto.GetProperty("method").GetString());
        Assert.Equal("QRTEXT", dto.GetProperty("qr").GetString());
        Assert.Equal("eskhata://pay/hlR2oH", dto.GetProperty("deepLink").GetString());
    }
}
```

> **Примечание для исполнителя:** свериться с `DcGateTopUpIntentTests.cs` — использовать тот же способ авторизации игрока и, если `WithReplacedService` в `PlatformApiFactory` отсутствует, добавить хелпер-обёртку `ConfigureTestServices` там же (одна строка `builder.ConfigureTestServices(s => s.AddScoped(_ => replacement))`).

- [ ] **Step 6: Запустить — упадёт**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~EskhataTopUpIntentTests`
Expected: FAIL (метод eskhata отклоняется валидацией).

- [ ] **Step 7: Реализовать ветку в эндпоинте**

В `PlayerSelfServiceEndpoints.cs`:
- В сигнатуру `MapPost("/api/me/wallet/top-up-intent", ...)` добавить параметр `IEskhataMerchantClientFactory eskhataClientFactory`.
- Разрешить метод: `if (method != "counter" && method != "dcgate" && method != "eskhata")` → 400.
- После блока `if (method == "dcgate") {...}` добавить:

```csharp
            if (method == "eskhata")
            {
                var eskhataClient = await eskhataClientFactory.CreateForOrganizationAsync(intent.OrganizationId, cancellationToken);
                if (eskhataClient is null)
                {
                    return Results.Json(new { Error = "online_payment_unavailable" },
                        statusCode: StatusCodes.Status409Conflict);
                }

                var order = await eskhataClient.CreateOrderAsync(
                    intent.PaymentIntentId.ToString("N"),
                    intent.AmountMinorUnits,
                    "972",
                    "AFK4 wallet top-up",
                    merchantId: await ResolveEskhataMerchantIdAsync(dbContext, intent.OrganizationId, cancellationToken),
                    cancellationToken);

                intent.GatewayPaymentId = order.OrderId;
                intent.GatewayPayUrl = order.InvoiceUrl;
                intent.GatewayQrPayload = order.Qr;
                intent.GatewayPosId = order.PosId;
            }
```

- Добавить локальный хелпер (в этом же классе):

```csharp
    private static async Task<int> ResolveEskhataMerchantIdAsync(
        PlatformDbContext db, Guid organizationId, CancellationToken ct)
    {
        var cfg = await db.EskhataMerchantConfigs.AsNoTracking()
            .SingleAsync(c => c.OrganizationId == organizationId && c.BranchId == null, ct);
        return cfg.MerchantId;
    }
```

- В финальный `Results.Ok(new PlayerTopUpIntentDto(...))` добавить аргументы:

```csharp
                Qr: intent.GatewayQrPayload,
                DeepLink: AFK4.Platform.Api.Payments.Eskhata.EskhataDeepLink.FromInvoiceUrl(intent.GatewayPayUrl));
```

(Также добавить `using Microsoft.EntityFrameworkCore;` если ещё нет для `SingleAsync`.)

- [ ] **Step 8: Запустить — зелёно**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~EskhataTopUpIntentTests|FullyQualifiedName~EskhataDeepLinkTests"`
Expected: PASS.

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat(payments): eskhata-ветка top-up-intent (create order → qr + deeplink)"
```

---

### Task 6: Публичный webhook Eskhata (перепроверка /status + идемпотентность)

**Files:**
- Create: `src/AFK4.Platform.Api/Endpoints/EskhataPaymentEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (рядом со строкой 443 `app.MapEskhataConfigEndpoints();`)
- Test: `tests/AFK4.Platform.Api.Tests/EskhataWebhookEndpointTests.cs`

**Interfaces:**
- Consumes: `IEskhataMerchantClientFactory`, `IBillingCommandService.CreditOnlineTopUpAsync`, `PaymentIntentEntity`.
- Produces: `POST /api/public/payments/eskhata/webhook`.

- [ ] **Step 1: Тесты webhook (по образцу DcGateWebhookEndpointTests, но с фейковой фабрикой статуса)**

```csharp
using System.Net;
using System.Net.Http.Json;
using System.Text;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Payments.Eskhata;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class EskhataWebhookEndpointTests
{
    // Фейковый клиент, чей /status возвращает заданный статус (перепроверка перед кредитом).
    private sealed class StatusStubFactory(string status) : IEskhataMerchantClientFactory
    {
        public Task<IEskhataMerchantClient?> CreateForOrganizationAsync(Guid orgId, CancellationToken ct)
            => Task.FromResult<IEskhataMerchantClient?>(new StatusStubClient(status));
    }
    private sealed class StatusStubClient(string status) : IEskhataMerchantClient
    {
        public Task<EskhataCreateOrderResult> CreateOrderAsync(string i, long a, string c, string d, int m, CancellationToken ct)
            => throw new NotSupportedException();
        public Task<string?> GetOrderStatusAsync(string i, string o, long a, string c, int p, CancellationToken ct)
            => Task.FromResult<string?>(status);
    }

    private static async Task<Guid> SeedEskhataIntentAsync(PlatformApiFactory f, string state = "pending")
    {
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var playerId = Guid.NewGuid();
        db.PlayerAccounts.Add(new PlayerAccountEntity
        {
            PlayerAccountId = playerId, OrganizationId = TestIds.OrganizationId, HomeBranchId = TestIds.BranchId,
            DisplayName = "EM Player", PhoneNumber = $"+99293111{playerId.ToString("N")[..4]}",
            IsActive = true, CreatedAtUtc = DateTimeOffset.UtcNow
        });
        var intentId = Guid.NewGuid();
        db.PaymentIntents.Add(new PaymentIntentEntity
        {
            PaymentIntentId = intentId, PlayerAccountId = playerId,
            OrganizationId = TestIds.OrganizationId, BranchId = TestIds.BranchId,
            AmountMinorUnits = 5000, CurrencyCode = "TJS", Purpose = "wallet_topup",
            State = state, Method = "eskhata", GatewayPaymentId = "order-abc", GatewayPosId = 48741,
            CreatedAtUtc = DateTimeOffset.UtcNow
        });
        await db.SaveChangesAsync();
        return intentId;
    }

    private static string CompletedBody(Guid intentId, string orderId = "order-abc") =>
        $$"""{"status":true,"code":0,"data":{"orderId":"{{orderId}}","orderStatus":"COMPLETED","invoiceId":"{{intentId:N}}","amount":50.00,"currency":"972","posId":48741}}""";

    private static async Task<int> CountTopUpsAsync(PlatformApiFactory f, Guid intentId)
    {
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var pid = (await db.PaymentIntents.SingleAsync(i => i.PaymentIntentId == intentId)).PlayerAccountId;
        return await db.LedgerEntries.CountAsync(e => e.PlayerAccountId == pid && e.EntryType == "top_up");
    }

    [Fact]
    public async Task Webhook_Completed_VerifiedByStatus_CreditsOnce()
    {
        await using var f = new PlatformApiFactory().WithReplacedService<IEskhataMerchantClientFactory>(new StatusStubFactory("COMPLETED"));
        var intentId = await SeedEskhataIntentAsync(f);
        using var client = f.CreateClient();

        var r = await client.PostAsync("/api/public/payments/eskhata/webhook",
            new StringContent(CompletedBody(intentId), Encoding.UTF8, "application/json"));

        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(1, await CountTopUpsAsync(f, intentId));
        await using var scope = f.Services.CreateAsyncScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        Assert.Equal("fulfilled", (await db.PaymentIntents.SingleAsync(i => i.PaymentIntentId == intentId)).State);
    }

    [Fact]
    public async Task Webhook_Replay_DoesNotDoubleCredit()
    {
        await using var f = new PlatformApiFactory().WithReplacedService<IEskhataMerchantClientFactory>(new StatusStubFactory("COMPLETED"));
        var intentId = await SeedEskhataIntentAsync(f);
        using var client = f.CreateClient();
        var body = CompletedBody(intentId);
        await client.PostAsync("/api/public/payments/eskhata/webhook", new StringContent(body, Encoding.UTF8, "application/json"));
        await client.PostAsync("/api/public/payments/eskhata/webhook", new StringContent(body, Encoding.UTF8, "application/json"));
        Assert.Equal(1, await CountTopUpsAsync(f, intentId));
    }

    [Fact]
    public async Task Webhook_StatusNotCompleted_DoesNotCredit()
    {
        await using var f = new PlatformApiFactory().WithReplacedService<IEskhataMerchantClientFactory>(new StatusStubFactory("NEW"));
        var intentId = await SeedEskhataIntentAsync(f);
        using var client = f.CreateClient();
        var r = await client.PostAsync("/api/public/payments/eskhata/webhook",
            new StringContent(CompletedBody(intentId), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode); // ack, но без кредита
        Assert.Equal(0, await CountTopUpsAsync(f, intentId));
    }

    [Fact]
    public async Task Webhook_OrderIdMismatch_DoesNotCredit()
    {
        await using var f = new PlatformApiFactory().WithReplacedService<IEskhataMerchantClientFactory>(new StatusStubFactory("COMPLETED"));
        var intentId = await SeedEskhataIntentAsync(f);
        using var client = f.CreateClient();
        var r = await client.PostAsync("/api/public/payments/eskhata/webhook",
            new StringContent(CompletedBody(intentId, orderId: "WRONG"), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        Assert.Equal(0, await CountTopUpsAsync(f, intentId));
    }

    [Fact]
    public async Task Webhook_UnknownInvoice_Returns200Noop()
    {
        await using var f = new PlatformApiFactory().WithReplacedService<IEskhataMerchantClientFactory>(new StatusStubFactory("COMPLETED"));
        using var client = f.CreateClient();
        var r = await client.PostAsync("/api/public/payments/eskhata/webhook",
            new StringContent(CompletedBody(Guid.NewGuid()), Encoding.UTF8, "application/json"));
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
    }
}
```

- [ ] **Step 2: Запустить — упадёт (нет эндпоинта, 404)**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~EskhataWebhookEndpointTests`
Expected: FAIL.

- [ ] **Step 3: Реализовать endpoint**

`EskhataPaymentEndpoints.cs`:

```csharp
using System.Text.Json;
using AFK4.Platform.Api.Billing;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Payments.Eskhata;
using AFK4.Shared.Contracts.Billing;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

// Публичный webhook Eskhata Merchant. Тело БЕЗ подписи — защита только IP allowlist банка,
// поэтому перед зачислением статус ПЕРЕПРОВЕРЯЕТСЯ запросом /orders/status (там наша подпись).
// Идемпотентность — по intent.State + ключ идемпотентности биллинга (intentId).
internal static class EskhataPaymentEndpoints
{
    private const string CreditReason = "eskhata_online_topup";

    public static void MapEskhataPaymentEndpoints(this WebApplication app)
    {
        app.MapPost("/api/public/payments/eskhata/webhook", async (
            HttpRequest httpRequest,
            IEskhataMerchantClientFactory clientFactory,
            IBillingCommandService billingCommandService,
            PlatformDbContext db,
            TimeProvider timeProvider,
            CancellationToken ct) =>
        {
            string raw;
            using (var reader = new StreamReader(httpRequest.Body))
            {
                raw = await reader.ReadToEndAsync(ct);
            }

            JsonElement data;
            try
            {
                using var doc = JsonDocument.Parse(raw);
                if (!doc.RootElement.TryGetProperty("data", out data)) return Results.Ok();
                data = data.Clone();
            }
            catch (JsonException) { return Results.BadRequest(); }

            var orderStatus = data.TryGetProperty("orderStatus", out var s) ? s.GetString() : null;
            var invoiceId = data.TryGetProperty("invoiceId", out var i) ? i.GetString() : null;
            var orderId = data.TryGetProperty("orderId", out var o) ? o.GetString() : null;
            if (orderStatus != "COMPLETED" || string.IsNullOrEmpty(invoiceId) || string.IsNullOrEmpty(orderId))
            {
                return Results.Ok(); // ack, не наш случай
            }

            if (!Guid.TryParseExact(invoiceId, "N", out var intentId)) return Results.Ok();

            var intent = await db.PaymentIntents.SingleOrDefaultAsync(x => x.PaymentIntentId == intentId, ct);
            if (intent is null || intent.Method != "eskhata") return Results.Ok();
            if (intent.GatewayPaymentId != orderId) return Results.Ok(); // не совпал заказ → игнор
            if (intent.State == "fulfilled") return Results.Ok(); // идемпотентность

            // Перепроверка статуса об API банка (подписанный запрос) перед кредитом.
            var client = await clientFactory.CreateForOrganizationAsync(intent.OrganizationId, ct);
            if (client is null) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);
            var verified = await client.GetOrderStatusAsync(
                invoiceId, orderId, intent.AmountMinorUnits, intent.CurrencyCode == "TJS" ? "972" : intent.CurrencyCode,
                intent.GatewayPosId ?? 0, ct);
            if (verified != "COMPLETED") return Results.Ok(); // не подтверждено API → без кредита

            var topUp = new TopUpWalletRequest(
                intent.OrganizationId,
                new MoneyDto(intent.CurrencyCode, intent.AmountMinorUnits),
                CreditReason,
                intent.PaymentIntentId.ToString("N"));

            var result = await billingCommandService.CreditOnlineTopUpAsync(
                intent.PlayerAccountId, intent.BranchId, topUp, ct);
            if (!result.Succeeded) return Results.StatusCode(StatusCodes.Status503ServiceUnavailable);

            intent.State = "fulfilled";
            intent.FulfilledAtUtc = timeProvider.GetUtcNow();
            await db.SaveChangesAsync(ct);
            return Results.Ok();
        }).RequireRateLimiting("player-public");
    }
}
```

> Свериться с dcgate-webhook: точные имена `TopUpWalletRequest`/`MoneyDto`/namespace `AFK4.Shared.Contracts.Billing` и константа reason — взять как там (`PaymentGatewayEndpoints.cs`, строки 161-173). При расхождении привести к фактическим.

- [ ] **Step 4: Замапить в Program.cs**

Рядом со строкой 443:

```csharp
app.MapEskhataConfigEndpoints();
app.MapEskhataPaymentEndpoints();
```

- [ ] **Step 5: Запустить — зелёно**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~EskhataWebhookEndpointTests`
Expected: PASS (5 фактов).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(payments): webhook Eskhata — credit с перепроверкой /status, идемпотентно"
```

---

### Task 7: Статус-поллинг для игрока

**Files:**
- Modify: `src/AFK4.Platform.Api/Endpoints/PlayerSelfServiceEndpoints.cs`
- Test: `tests/AFK4.Platform.Api.Tests/EskhataTopUpIntentTests.cs` (доп. факт)

**Interfaces:**
- Consumes: `IEskhataMerchantClientFactory`, `IBillingCommandService`, `PaymentIntentEntity`.
- Produces: `POST /api/me/wallet/top-up-intents/{intentId:guid}/eskhata-status` → `{ payment: "pending"|"paid"|"failed" }`.

- [ ] **Step 1: Доп-тест — pending intent, API=COMPLETED → paid + кредит**

```csharp
    [Fact]
    public async Task EskhataStatusPoll_WhenCompleted_CreditsAndReturnsPaid()
    {
        await using var factory = new PlatformApiFactory()
            .WithReplacedService<IEskhataMerchantClientFactory>(new FakeFactory()); // GetOrderStatus → COMPLETED
        var (playerToken, intentId) = await SeedEskhataPendingIntentForPlayerAsync(factory); // хелпер по образцу
        using var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new("Bearer", playerToken);

        var r = await client.PostAsync($"/api/me/wallet/top-up-intents/{intentId}/eskhata-status", null);
        Assert.Equal(HttpStatusCode.OK, r.StatusCode);
        var dto = await r.Content.ReadFromJsonAsync<System.Text.Json.JsonElement>();
        Assert.Equal("paid", dto.GetProperty("payment").GetString());
    }
```

> Хелпер `SeedEskhataPendingIntentForPlayerAsync` создаёт игрока+токен и его pending eskhata-intent (`GatewayPaymentId`, `GatewayPosId` заполнены) — по образцу существующих player-хелперов в `DcGateTopUpIntentTests.cs`.

- [ ] **Step 2: Запустить — упадёт (404)**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~EskhataStatusPoll`
Expected: FAIL.

- [ ] **Step 3: Реализовать эндпоинт**

В `PlayerSelfServiceEndpoints.cs` добавить:

```csharp
        app.MapPost("/api/me/wallet/top-up-intents/{intentId:guid}/eskhata-status", async (
            Guid intentId,
            IPlayerContextAccessor playerContextAccessor,
            IEskhataMerchantClientFactory eskhataClientFactory,
            IBillingCommandService billingCommandService,
            PlatformDbContext dbContext,
            TimeProvider timeProvider,
            CancellationToken cancellationToken) =>
        {
            var player = playerContextAccessor.Current;
            if (player is null) return Results.Unauthorized();

            var intent = await dbContext.PaymentIntents.SingleOrDefaultAsync(
                x => x.PaymentIntentId == intentId && x.PlayerAccountId == player.PlayerAccountId, cancellationToken);
            if (intent is null || intent.Method != "eskhata") return Results.NotFound();

            if (intent.State == "fulfilled") return Results.Ok(new { payment = "paid" });
            if (intent.State is "cancelled" or "expired") return Results.Ok(new { payment = "failed" });

            var client = await eskhataClientFactory.CreateForOrganizationAsync(intent.OrganizationId, cancellationToken);
            if (client is null) return Results.Ok(new { payment = "pending" });

            var status = await client.GetOrderStatusAsync(
                intent.PaymentIntentId.ToString("N"), intent.GatewayPaymentId ?? "",
                intent.AmountMinorUnits, "972", intent.GatewayPosId ?? 0, cancellationToken);

            if (status == "COMPLETED")
            {
                var topUp = new TopUpWalletRequest(
                    intent.OrganizationId, new MoneyDto(intent.CurrencyCode, intent.AmountMinorUnits),
                    "eskhata_online_topup", intent.PaymentIntentId.ToString("N"));
                var result = await billingCommandService.CreditOnlineTopUpAsync(
                    intent.PlayerAccountId, intent.BranchId, topUp, cancellationToken);
                if (result.Succeeded)
                {
                    intent.State = "fulfilled";
                    intent.FulfilledAtUtc = timeProvider.GetUtcNow();
                    await dbContext.SaveChangesAsync(cancellationToken);
                    return Results.Ok(new { payment = "paid" });
                }
            }
            if (status is "CANCELED" or "REFUNDED") return Results.Ok(new { payment = "failed" });
            return Results.Ok(new { payment = "pending" });
        }).RequireRateLimiting("player-me");
```

(Идемпотентность двойного кредита — общий ключ `intentId` в биллинге, как в webhook.)

- [ ] **Step 4: Запустить — зелёно**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~EskhataStatusPoll`
Expected: PASS.

- [ ] **Step 5: Прогнать весь Eskhata-набор + сборку**

Run:
```bash
dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~Eskhata
dotnet build src/AFK4.Platform.Api
```
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "feat(payments): eskhata-status поллинг игрока (кредит по COMPLETED, идемпотентно)"
```

---

### Task 8: Интеграционная сверка об тестовый API банка (ручная, снимает неизвестность хеша типа 3)

**Files:**
- Create: `scripts/eskhata-probe.http` (или временный xUnit `[Fact(Skip=...)]`, включаемый вручную)

**Цель:** определить точный порядок склейки хеша для `orderTypeId=3` и подтвердить create/status об реальный тестовый endpoint (доступы: companyId `5107ba47-fc70-4180-ae2d-18a8eee913bb`, merchantId `28652`, hashKey — из зашифрованного хранилища/переданный банком; base_url — тестовый от банка).

- [ ] **Step 1: Подготовить пробу**

Скрипт, который шлёт `POST /merchant/api/v1/orders/create` с телом типа 3 и печатает `code`/`message`. Ключ и base_url брать из окружения/argsсекрета, **не хардкодить**.

- [ ] **Step 2: Перебрать кандидатные порядки хеша**

Кандидаты для `BuildHash`:
1. `invoiceId, amount, currency, description, merchantId, orderTypeId` (текущая гипотеза в коде)
2. `invoiceId, amount, currency, description, orderTypeId, merchantId`
3. `invoiceId, amount, currency, description, merchantId` (без orderTypeId)

Критерий: `code=0` (успех) vs `code=-2` (ошибка контроля суммы) / `code=-12` (неправильный тип заказа).

- [ ] **Step 3: Зафиксировать правильный порядок в `EskhataMerchantClient.CreateOrderAsync`**

Обновить массив `BuildHash(...)` на подтверждённый порядок; убрать предупреждающий комментарий-гипотезу; при необходимости поправить unit-тест клиента (значение хеша не проверяем, проверяем структуру — тест остаётся валиден).

- [ ] **Step 4: Прогон оплаты тестовым кошельком → проверить webhook/поллинг**

Оплатить тестовый заказ, убедиться, что webhook/`eskhata-status` доводят intent до `fulfilled` и кошелёк кредитуется один раз.

- [ ] **Step 5: Активация**

После успешной сверки — расширить статусы конфига (`active`) и перевести тестовую org в `active` (отдельный маленький PR/шаг, если решим включать статус в UI).

- [ ] **Step 6: Commit**

```bash
git add -A
git commit -m "fix(payments): подтверждён порядок хеша Eskhata orderTypeId=3 (интеграционная сверка)"
```

---

## Phase 1 — финальная приёмка

- [ ] `dotnet test tests/AFK4.Platform.Api.Tests` — весь набор зелёный.
- [ ] `dotnet build src/AFK4.Platform.Api` — зелёно.
- [ ] `cd src/AFK4.Operator.App.Web && bun test && bun run build` — зелёно (конфиг-форма).
- [ ] Миграции применяются на staging (`AddEskhataMerchantId`, `AddPaymentIntentEskhataFields`).
- [ ] PR → зелёный CI → merge (auto-merge авторизован).

---

## Subsequent phases (отдельные планы — ПОСЛЕ Phase 1)

Пишутся после того, как Task 8 подтвердит контракт банка (иначе UI строится против неизвестного ответа = риск переделки).

### Phase 2 — Customer.Web (PWA, телефон игрока): deeplink-кнопка
- Файлы: `src/AFK4.Customer.Web/src/api/playerApi.ts` (метод top-up-intent + eskhata-status), топап-экран Customer.Web.
- Механика (эталон `nj-cosmetics.com` OrderDetailsComponent.vue, проверен): `method:"eskhata"` → из ответа взять `deepLink` → на мобиле `window.location.href = deepLink` (кастомная схема не выгружает вкладку) → поллинг `eskhata-status` раз в 3с + на `visibilitychange` → дедлайн 5 мин; десктоп → открыть `payUrl` в новой вкладке по клику пользователя.
- Тесты: `bun test` на компонент топапа (мок ответа с deepLink → рендер кнопки, клик → навигация).

### Phase 3 — Player.Shell.Web (игровой ПК / киоск): QR на экране
- Файлы: `src/AFK4.Player.Shell.Web/src/screens/TopUpScreen.tsx`, `shellApi.ts`, `apiTypes.ts`.
- Механика: `method:"eskhata"` → из ответа взять `qr` → отрендерить QR-код на экране → игрок сканит своим телефоном → тот же `eskhata-status` поллинг до `paid`.
- Открытый пункт: подтвердить QR-библиотеку рендера в shell-стеке (уточнить в плане Phase 3).
