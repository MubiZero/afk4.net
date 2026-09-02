# DC (DushanbeCity) pay-link/QR + ручное подтверждение — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Приём пополнений кошелька переводом на карту DushanbeCity через pay-link/QR в Кассе с ручным подтверждением кассиром; полный снос старого dcgate money-path.

**Architecture:** Кассир заводит DC-пополнение → бэкенд собирает ссылку `pay.dc.tj/?A=&s=&c=&f1=133`, создаёт `PaymentIntent(Method="dc", pending)` → Касса рендерит QR → игрок платит → кассир жмёт «Оплата получена», которая **переиспользует существующий** `POST /api/wallet/top-up-intents/{id}/fulfil` (кредит через `TopUpWalletAsync`, идемпотентно). Старый dcgate (клиент/резолвер/webhook/entity/таблицы/онлайн-метод/Telegram-UI) удаляется целиком.

**Tech Stack:** .NET 10 minimal-API, EF Core (PostgreSQL) + миграции, xUnit (`PlatformApiFactory`). React/TS фронт на `bun test` (happy-dom + jest-dom) + `bun run build` (`tsc -b && vite`). i18n через `locales/{ru,en,tg}.json` → `packages/i18n` gen. QR — либа `qrcode`.

## Global Constraints

- **Только** `AFK4.Platform.Api`, `AFK4.Shared.Contracts`, `AFK4.Operator.App.Web`. `Player.Shell.Web` НЕ трогаем (бэклог); его runtime-поломка онлайн-пополнения после сноса dcgate — **принята**.
- **Money-path:** зачисление только через существующий `TopUpWalletAsync`, идемпотентность по `PaymentIntentId.ToString("N")`. Не писать свой кредит. IsActive-guard игрока обеспечивает `TopUpWalletAsync` (не дублировать, но и не обходить).
- **Секреты:** номер карты приёма шифруется `ISecretProtector.Protect`, наружу (GET/DTO) отдаём только `CardLast4` + `CardSet`. Полный PAN не логировать, не возвращать.
- **Формат суммы `s`** в ссылке: мажорные сомони, ровно 2 знака, `InvariantCulture` (`(minor/100m).ToString("0.00", ...)`).
- **Константа ссылки:** `http://pay.dc.tj/?A={card}&s={amount}&c={urlencoded-comment}&f1=133`. `f1=133` — фикс, не параметр.
- **`PaymentIntentEntity` колонки НЕ трогаем** (`GatewayPaymentId`/`GatewayPayUrl` общие с Eskhata; `GatewayComment` переиспользуем под DC-референс; `Disputed`/`GatewayExpiresAtUtc` оставляем как безвредные nullable). Миграция сноса дропает только **две таблицы**.
- **Права:** конфиг DC → `StaffPermissionNames.ManagePaymentGateways` (`payments.gateways.manage`, переиспользуем — константу НЕ удалять). POS DC-действия → `StaffPermissionNames.TopUpWallet` (`billing.wallet.top_up`).
- **Миграции (WSL-грабли):** ВСЕГДА `dotnet build src/AFK4.Platform.Api` перед `dotnet ef migrations add <Name> --project src/AFK4.Platform.Api --no-build` (иначе пустая миграция). Откат неприменённой миграции = `rm` `.cs`+`.Designer.cs`, НЕ `dotnet ef migrations remove` (коннектится к БД, падает без Postgres).
- **Гейты слайса:** `dotnet build` + полный `dotnet test tests/AFK4.Platform.Api.Tests` зелёные; для фронта `bun test` + `bun run build` зелёные.
- **i18n:** править `locales/{ru,en,tg}.json`, затем `cd packages/i18n && bun run gen` (НЕ редактировать `messages.ts` руками). tg — настоящий таджикский (guard `tg !== ru`, тех-идентификаторы в whitelist).
- Никаких AI-подписей в коммитах.

---

## File Structure

**Удаляем (снос dcgate):**
- Backend: вся папка `src/AFK4.Platform.Api/Payments/DcGate/` (7 файлов), `Payments/IBranchPaymentGatewayResolver.cs`, `Payments/EfBranchPaymentGatewayResolver.cs`, `Payments/BranchPaymentGatewayStatus.cs`, `Data/BranchPaymentGatewayEntity.cs`, `Data/DcGateWebhookEventEntity.cs`, `Endpoints/PaymentGatewayEndpoints.cs`, `Shared.Contracts/Payments/DcGateWebhookPayload.cs`, `Shared.Contracts/Payments/OwnerPaymentGatewayDtos.cs`.
- Backend tests: `DcGateTopUpIntentTests.cs`, `DcGateAdminClientTests.cs`, `DcGateClientTests.cs`, `DcGateWebhookEndpointTests.cs`, `BranchPaymentGatewayEntityTests.cs`, `BranchPaymentGatewayResolverTests.cs`, `OwnerPaymentGatewayEndpointTests.cs`.
- Frontend: `src/AFK4.Operator.App.Web/src/PaymentGatewaysWorkspace.tsx`, `src/api/clients/paymentGateways.ts`, `src/PaymentGatewaysWorkspace.test.tsx`.

**Создаём (новый DC):**
- `src/AFK4.Platform.Api/Payments/Dc/DcPayLink.cs` — сборщик ссылки/комментария (чистые функции).
- `src/AFK4.Platform.Api/Data/DcPayLinkConfigEntity.cs` — конфиг карты приёма.
- `src/AFK4.Shared.Contracts/Payments/DcPayLinkConfigDtos.cs` — DTO конфига.
- `src/AFK4.Shared.Contracts/Payments/DcTopUpDtos.cs` — DTO POS-запроса/ответа.
- `src/AFK4.Platform.Api/Endpoints/DcConfigEndpoints.cs` — `GET/POST /api/owner/dc-config`.
- `src/AFK4.Platform.Api/Endpoints/DcTopUpEndpoints.cs` — `POST /api/branches/{branchId}/pos/dc-topups` + `.../{id}/cancel`.
- `src/AFK4.Operator.App.Web/src/management/destinations/payments/DcTransferForm.tsx` — форма конфига (в слот PaymentMethodsSection).
- `src/AFK4.Operator.App.Web/src/api/clients/dcConfig.ts` — клиент конфига.
- `src/AFK4.Operator.App.Web/src/players/DcTopUpDialog.tsx` — диалог пополнения в Кассе (QR + confirm/cancel).
- `src/AFK4.Operator.App.Web/src/api/clients/dcTopUps.ts` — клиент POS DC.
- Тесты: `DcPayLinkTests.cs`, `DcConfigEndpointsTests.cs`, `DcTopUpEndpointsTests.cs`, `DcTransferForm.test.tsx`, `DcTopUpDialog.test.tsx`.

**Правим:** `Program.cs`, `PlatformDbContext.cs`, `PlayerSelfServiceEndpoints.cs`, `EndpointHelpers.Http.cs`, `appsettings.json`, ~30 файлов со стрейной `using ...DcGate;`, `PaymentMethodsSection.tsx`, `operatorApiClients.ts`, `api/clients/index.ts`, `locales/*.json`, `Operator.App.Web/package.json` (+`qrcode`).

---

## Task 1: Снос dcgate — backend

**Files:**
- Delete: `src/AFK4.Platform.Api/Payments/DcGate/*` (все 7), `Payments/IBranchPaymentGatewayResolver.cs`, `Payments/EfBranchPaymentGatewayResolver.cs`, `Payments/BranchPaymentGatewayStatus.cs`, `Data/BranchPaymentGatewayEntity.cs`, `Data/DcGateWebhookEventEntity.cs`, `Endpoints/PaymentGatewayEndpoints.cs`, `Shared.Contracts/Payments/DcGateWebhookPayload.cs`, `Shared.Contracts/Payments/OwnerPaymentGatewayDtos.cs`.
- Delete tests: `tests/AFK4.Platform.Api.Tests/{DcGateTopUpIntentTests,DcGateAdminClientTests,DcGateClientTests,DcGateWebhookEndpointTests,BranchPaymentGatewayEntityTests,BranchPaymentGatewayResolverTests,OwnerPaymentGatewayEndpointTests}.cs`.
- Modify: `Program.cs`, `Data/PlatformDbContext.cs`, `Endpoints/PlayerSelfServiceEndpoints.cs`, `Endpoints/EndpointHelpers.Http.cs`, `appsettings.json`, ~30 файлов со стрейной using.
- Migration: `src/AFK4.Platform.Api/Data/Migrations/<ts>_RemoveDcGate.cs`.

**Interfaces:**
- Produces: `/api/me/wallet/top-up-intent` больше не принимает `method="dcgate"` (только `counter`/`eskhata`); удалены типы `IDcGateClientFactory`, `IBranchPaymentGatewayResolver`, `BranchPaymentGatewayEntity`, `DcGateWebhookEventEntity` и namespace `AFK4.Platform.Api.Payments.DcGate`.

Это механический атомарный снос: проект не скомпилируется, пока не удалены и файлы, и все ссылки. Порядок шагов подобран так, чтобы прийти к зелёной сборке одним заходом.

- [ ] **Step 1: Удалить файлы реализации и тестов**

```bash
cd /home/fedya/projects/afk4.net
git rm -r src/AFK4.Platform.Api/Payments/DcGate
git rm src/AFK4.Platform.Api/Payments/IBranchPaymentGatewayResolver.cs \
       src/AFK4.Platform.Api/Payments/EfBranchPaymentGatewayResolver.cs \
       src/AFK4.Platform.Api/Payments/BranchPaymentGatewayStatus.cs \
       src/AFK4.Platform.Api/Data/BranchPaymentGatewayEntity.cs \
       src/AFK4.Platform.Api/Data/DcGateWebhookEventEntity.cs \
       src/AFK4.Platform.Api/Endpoints/PaymentGatewayEndpoints.cs \
       src/AFK4.Shared.Contracts/Payments/DcGateWebhookPayload.cs \
       src/AFK4.Shared.Contracts/Payments/OwnerPaymentGatewayDtos.cs
git rm tests/AFK4.Platform.Api.Tests/DcGateTopUpIntentTests.cs \
       tests/AFK4.Platform.Api.Tests/DcGateAdminClientTests.cs \
       tests/AFK4.Platform.Api.Tests/DcGateClientTests.cs \
       tests/AFK4.Platform.Api.Tests/DcGateWebhookEndpointTests.cs \
       tests/AFK4.Platform.Api.Tests/BranchPaymentGatewayEntityTests.cs \
       tests/AFK4.Platform.Api.Tests/BranchPaymentGatewayResolverTests.cs \
       tests/AFK4.Platform.Api.Tests/OwnerPaymentGatewayEndpointTests.cs
```

- [ ] **Step 2: `Program.cs` — снять DI/HttpClient/маппинг**

Удалить строки (по одной, проверяя контекст — не задеть соседнюю Eskhata-регистрацию):
- `using AFK4.Platform.Api.Payments.DcGate;` (верх файла).
- `builder.Services.AddScoped<IBranchPaymentGatewayResolver, EfBranchPaymentGatewayResolver>();`
- `builder.Services.Configure<DcGateOptions>(builder.Configuration.GetSection(DcGateOptions.SectionName));`
- Блок `AddHttpClient(DcGateClientFactory.HttpClientName, ...)` + `AddSingleton<IDcGateClientFactory, DcGateClientFactory>();`
- Блок `AddHttpClient(DcGateAdminClientRegistration.HttpClientName, ...)` + `AddSingleton<IDcGateAdminClient>(...)`.
- `app.MapPaymentGatewayEndpoints();`

⚠️ Между HttpClient-блоками dcgate стоит регистрация `EskhataMerchantClientFactory` — **оставить её**.

- [ ] **Step 3: `PlatformDbContext.cs` — снять DbSet-ы и ModelBuilder-конфиги**

Удалить: `public DbSet<BranchPaymentGatewayEntity> BranchPaymentGateways => ...;`, `public DbSet<DcGateWebhookEventEntity> DcGateWebhookEvents => ...;`, весь `modelBuilder.Entity<BranchPaymentGatewayEntity>(...)` блок и весь `modelBuilder.Entity<DcGateWebhookEventEntity>(...)` блок.

- [ ] **Step 4: `PlayerSelfServiceEndpoints.cs` — убрать dcgate-ветку top-up-intent**

В обработчике `POST /api/me/wallet/top-up-intent`:
- Удалить параметры `IDcGateClientFactory dcGateClientFactory` и `IBranchPaymentGatewayResolver gatewayResolver`.
- Удалить `using AFK4.Platform.Api.Payments.DcGate;` (строка 9).
- Заменить валидацию метода:

```csharp
            var method = string.IsNullOrWhiteSpace(request.Method)
                ? "counter"
                : request.Method.Trim().ToLowerInvariant();
            if (method != "counter" && method != "eskhata")
            {
                return Results.BadRequest(new { Error = "Method must be 'counter' or 'eskhata'." });
            }
```

- Удалить целиком блок `if (method == "dcgate") { ... }` (резолв gateway → CreatePaymentAsync → запись Gateway*-полей). Блок `if (method == "eskhata")` и сборку `PlayerTopUpIntentDto` НЕ трогать.

- [ ] **Step 5: `EndpointHelpers.Http.cs` — удалить `DcGateSignatureIsValid` + using**

Удалить метод `public static bool DcGateSignatureIsValid(HttpRequest request, string rawBody, string secret) { ... }` целиком и строку `using AFK4.Platform.Api.Payments.DcGate;`.

- [ ] **Step 6: `appsettings.json` — удалить секцию `"DcGate"`**

Удалить объект `"DcGate": { "BaseUrl": "", "AdminSecret": "", "WebhookUrl": "", "PaymentExpiresInMinutes": 30 }` (и висящую запятую).

- [ ] **Step 7: Вычистить ~30 стрейных `using AFK4.Platform.Api.Payments.DcGate;`**

Эти usings скопированы по шаблону и не используют типы DcGate, но после удаления namespace дадут CS0234. Найти и удалить строку из каждого:

```bash
cd /home/fedya/projects/afk4.net
grep -rln "using AFK4.Platform.Api.Payments.DcGate;" src
# Удалить эту строку в каждом найденном файле (кроме уже правленных в шагах 4-5).
# Проверить, что ни один из файлов больше не ссылается на типы DcGate:
grep -rn "DcGate\|BranchPaymentGateway\|IBranchPaymentGatewayResolver" src || echo "чисто"
```

- [ ] **Step 8: Собрать проект (обязательно перед миграцией)**

```bash
dotnet build src/AFK4.Platform.Api
```
Expected: **Build succeeded**, 0 errors. Если CS0234 — остался неудалённый `using`/ссылка (вернуться к шагу 7).

- [ ] **Step 9: Сгенерировать миграцию дропа таблиц**

```bash
dotnet ef migrations add RemoveDcGate --project src/AFK4.Platform.Api --no-build
```
Открыть сгенерированный `Up` — должен дропать **только** `branch_payment_gateways` и `dcgate_webhook_events` (таблицы). Если EF предлагает трогать колонки `payment_intents` — **удалить эти строки из миграции руками** (колонки не трогаем, см. Global Constraints). `Down` — воссоздание таблиц (оставить как есть).

- [ ] **Step 10: Прогнать полный backend-тест-сьют**

```bash
dotnet test tests/AFK4.Platform.Api.Tests
```
Expected: **Failed: 0**. (Часть dcgate-тестов удалена в шаге 1; остальные не должны падать.)

- [ ] **Step 11: Commit**

```bash
git add -A
git commit -m "refactor(payments): снести старый dcgate money-path (клиент/резолвер/webhook/entity/таблицы/онлайн-метод)"
```

---

## Task 2: Снос dcgate — frontend (Operator.App.Web)

**Files:**
- Delete: `src/AFK4.Operator.App.Web/src/PaymentGatewaysWorkspace.tsx`, `src/api/clients/paymentGateways.ts`, `src/PaymentGatewaysWorkspace.test.tsx`.
- Modify: `src/management/destinations/payments/PaymentMethodsSection.tsx`, `src/operatorApiClients.ts`, `src/api/clients/index.ts`, `locales/{ru,en,tg}.json`, `src/styles/24-payments-setup.css`, `src/management/destinations/PaymentsLoyaltyDestination.test.tsx` (если ломается).

**Interfaces:**
- Produces: `backend.paymentGateways` клиента больше нет; в PaymentMethodsSection остаётся только Eskhata-блок (DC-конфиг добавит Task 7).

- [ ] **Step 1: Удалить компонент, клиент и его тест**

```bash
cd /home/fedya/projects/afk4.net
git rm src/AFK4.Operator.App.Web/src/PaymentGatewaysWorkspace.tsx \
       src/AFK4.Operator.App.Web/src/PaymentGatewaysWorkspace.test.tsx \
       src/AFK4.Operator.App.Web/src/api/clients/paymentGateways.ts
```

- [ ] **Step 2: Снять экспорт/регистрацию клиента**

- `src/operatorApiClients.ts`: удалить `export * from './api/clients/paymentGateways';`.
- `src/api/clients/index.ts`: удалить `import { createPaymentGatewayClient } ...` и строку `paymentGateways: createPaymentGatewayClient(api),`.

- [ ] **Step 3: `PaymentMethodsSection.tsx` — убрать dcgate-блок**

Удалить импорт `import { PaymentGatewaysWorkspace } from '../../../PaymentGatewaysWorkspace';` и JSX-строки блока DushanbeCity:

```tsx
        <div className="payset-divider" />
        <div className="payset-subhead">{t('op.payments.dc.subhead')}</div>
        <p className="payset-note">{t('op.payments.dc.note')}</p>
        <PaymentGatewaysWorkspace backend={backend} />
```

(Eskhata-форму на строке выше — оставить. Слот под новый DC-конфиг заполнит Task 7.)

- [ ] **Step 4: Удалить dcgate-ключи i18n и перегенерировать**

В `locales/{ru,en,tg}.json` удалить все ключи `payments_cards.*`, `op.payments.cards.*`, `op.payments.dc.subhead`, `op.payments.dc.note`. Затем:

```bash
cd packages/i18n && bun run gen && cd ../..
```

- [ ] **Step 5: Удалить только dcgate-эксклюзивные CSS-классы**

В `src/AFK4.Operator.App.Web/src/styles/24-payments-setup.css` удалить правила, используемые **только** `PaymentGatewaysWorkspace`: `.payset-cards`, `.payset-card`, `.payset-card:hover`, `.payset-card-pan`, `.payset-card-meta`, `.payset-card-tags`, `.payset-card-note`, `.payset-card > .ui-btn`, `.payset-add` (+`:hover`/`:focus-visible`), `.payset-attach` (+`-row`/label/input/`:focus-visible`), `.payset-attach-done`, `.payset-inline-error`.
⚠️ НЕ удалять общие: `.payset-subhead`, `.payset-divider`, `.payset-note`, `.payset-reveal`, `.payset-loading`, `.payset-method*` (используются Eskhata/Loyalty).

- [ ] **Step 6: Починить/убрать сломанные фронт-тесты**

```bash
cd src/AFK4.Operator.App.Web
grep -rln "paymentGateways\|PaymentGatewaysWorkspace\|payments_cards\|op.payments.dc" src
```
В `management/destinations/PaymentsLoyaltyDestination.test.tsx` убрать ожидания dcgate-блока (оставить проверку Eskhata + прав). Тест `managementNav.test.ts` (право `managePaymentGateways`) — право сохраняется (используется новым DC-конфигом), не трогать.

- [ ] **Step 7: Гейты фронта**

```bash
cd src/AFK4.Operator.App.Web
bun test
bun run build
```
Expected: `bun test` — 0 fail; `bun run build` — ✓ built.

- [ ] **Step 8: Commit**

```bash
cd /home/fedya/projects/afk4.net
git add -A
git commit -m "refactor(operator): убрать dcgate-блок конфига и клиента из Платежей"
```

---

## Task 3: `DcPayLink` — сборщик ссылки и комментария

**Files:**
- Create: `src/AFK4.Platform.Api/Payments/Dc/DcPayLink.cs`
- Test: `tests/AFK4.Platform.Api.Tests/DcPayLinkTests.cs`

**Interfaces:**
- Produces:
  - `DcPayLink.FormatAmount(long minorUnits) → string` — «0.00» InvariantCulture.
  - `DcPayLink.BuildComment(string template, string reference) → string` — подстановка `{ref}`.
  - `DcPayLink.BuildUrl(string cardNumber, long amountMinor, string comment) → string` — итоговый URL.

- [ ] **Step 1: Написать падающий тест**

```csharp
using AFK4.Platform.Api.Payments.Dc;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class DcPayLinkTests
{
    [Fact]
    public void FormatAmount_TwoDecimals_Invariant()
    {
        Assert.Equal("50.00", DcPayLink.FormatAmount(5000));
        Assert.Equal("0.05", DcPayLink.FormatAmount(5));
        Assert.Equal("1234.50", DcPayLink.FormatAmount(123450));
    }

    [Fact]
    public void BuildComment_SubstitutesRef()
    {
        Assert.Equal("AFK4-1a2b3c4d", DcPayLink.BuildComment("AFK4-{ref}", "1a2b3c4d"));
    }

    [Fact]
    public void BuildUrl_HasCardAmountEncodedCommentAndConstant()
    {
        var url = DcPayLink.BuildUrl("1234567890123456", 5000, "AFK4 заказ 7");
        Assert.Equal(
            "http://pay.dc.tj/?A=1234567890123456&s=50.00&c=AFK4%20%D0%B7%D0%B0%D0%BA%D0%B0%D0%B7%207&f1=133",
            url);
    }
}
```

- [ ] **Step 2: Запустить — упадёт**

```bash
dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~DcPayLinkTests"
```
Expected: FAIL (тип `DcPayLink` не найден).

- [ ] **Step 3: Реализовать**

```csharp
using System.Globalization;

namespace AFK4.Platform.Api.Payments.Dc;

// Сборщик платёжной ссылки DushanbeCity. Ссылка — «тупая»: банк не подтверждает оплату,
// подтверждает кассир вручную. Формат фиксирован: pay.dc.tj/?A=карта&s=сумма&c=коммент&f1=133.
public static class DcPayLink
{
    private const string BaseUrl = "http://pay.dc.tj/";
    private const string ConstParams = "f1=133";

    // Minor units → мажорные сомони, ровно 2 знака.
    public static string FormatAmount(long minorUnits) =>
        (minorUnits / 100m).ToString("0.00", CultureInfo.InvariantCulture);

    // Подставляет {ref} в шаблон комментария (напр. "AFK4-{ref}").
    public static string BuildComment(string template, string reference) =>
        template.Replace("{ref}", reference, StringComparison.Ordinal);

    public static string BuildUrl(string cardNumber, long amountMinor, string comment)
    {
        var s = FormatAmount(amountMinor);
        var c = Uri.EscapeDataString(comment);
        return $"{BaseUrl}?A={cardNumber}&s={s}&c={c}&{ConstParams}";
    }
}
```

- [ ] **Step 4: Запустить — пройдёт**

```bash
dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~DcPayLinkTests"
```
Expected: PASS (3 теста).

- [ ] **Step 5: Commit**

```bash
git add -A && git commit -m "feat(payments): DcPayLink — сборщик pay-link/комментария DushanbeCity"
```

---

## Task 4: Конфиг DC — entity, DbContext, миграция, DTO

**Files:**
- Create: `src/AFK4.Platform.Api/Data/DcPayLinkConfigEntity.cs`, `src/AFK4.Shared.Contracts/Payments/DcPayLinkConfigDtos.cs`
- Modify: `src/AFK4.Platform.Api/Data/PlatformDbContext.cs`
- Migration: `src/AFK4.Platform.Api/Data/Migrations/<ts>_AddDcPayLinkConfig.cs`
- Test: `tests/AFK4.Platform.Api.Tests/DcConfigEndpointsTests.cs` (создаётся в Task 5; здесь только round-trip проверяется сборкой)

**Interfaces:**
- Produces:
  - `DcPayLinkConfigEntity { Guid DcPayLinkConfigId; Guid OrganizationId; Guid? BranchId; string ReceivingCardEncrypted; string CardLast4; string CommentTemplate; bool IsActive; DateTimeOffset CreatedAtUtc, UpdatedAtUtc; }`
  - `PlatformDbContext.DcPayLinkConfigs`
  - `DcPayLinkConfigDto(bool CardSet, string CardLast4, string CommentTemplate, bool IsActive)`
  - `UpdateDcPayLinkConfigRequest(string? CardNumber, string CommentTemplate, bool IsActive)`

- [ ] **Step 1: Создать entity**

```csharp
namespace AFK4.Platform.Api.Data;

// Конфиг приёма DushanbeCity: карта приёма (шифрованный PAN) + шаблон комментария. Org-уровень
// (BranchId=null) в v1, как EskhataMerchantConfig. Отдельно от удалённого dcgate — тут нет
// API-проекта/Telegram: DC-ссылка «тупая», подтверждает кассир.
public sealed class DcPayLinkConfigEntity
{
    public Guid DcPayLinkConfigId { get; set; }

    public Guid OrganizationId { get; set; }

    // null => org-уровень (v1 использует только его).
    public Guid? BranchId { get; set; }

    // Полный номер карты приёма, шифрован ISecretProtector. Нужен для сборки ссылки.
    public string ReceivingCardEncrypted { get; set; } = string.Empty;

    // Последние 4 цифры для показа в UI (наружу PAN не отдаём).
    public string CardLast4 { get; set; } = string.Empty;

    // Шаблон комментария платежа, {ref} заменяется на короткий id намерения.
    public string CommentTemplate { get; set; } = "AFK4-{ref}";

    public bool IsActive { get; set; }

    public DateTimeOffset CreatedAtUtc { get; set; }

    public DateTimeOffset UpdatedAtUtc { get; set; }
}
```

- [ ] **Step 2: Создать DTO**

```csharp
namespace AFK4.Shared.Contracts.Payments;

// GET-ответ: PAN не возвращаем — только факт наличия и last4.
public sealed record DcPayLinkConfigDto(
    bool CardSet,
    string CardLast4,
    string CommentTemplate,
    bool IsActive);

// POST-запрос: CardNumber опционален — null/пусто сохраняет прежнюю карту, непустой заменяет.
public sealed record UpdateDcPayLinkConfigRequest(
    string? CardNumber,
    string CommentTemplate,
    bool IsActive);
```

- [ ] **Step 3: Зарегистрировать в `PlatformDbContext.cs`**

Добавить DbSet (рядом с прочими Payment*-сетами):

```csharp
    public DbSet<DcPayLinkConfigEntity> DcPayLinkConfigs => Set<DcPayLinkConfigEntity>();
```

Добавить конфиг в `OnModelCreating` (рядом с `EskhataMerchantConfigEntity`):

```csharp
        modelBuilder.Entity<DcPayLinkConfigEntity>(entity =>
        {
            entity.ToTable("dc_paylink_configs");
            entity.HasKey(e => e.DcPayLinkConfigId);
            entity.Property(e => e.ReceivingCardEncrypted).IsRequired();
            entity.Property(e => e.CardLast4).HasMaxLength(4).IsRequired();
            entity.Property(e => e.CommentTemplate).HasMaxLength(64).IsRequired();
            entity.HasIndex(e => new { e.OrganizationId, e.BranchId }).IsUnique();
        });
```

- [ ] **Step 4: Собрать перед миграцией**

```bash
dotnet build src/AFK4.Platform.Api
```
Expected: Build succeeded.

- [ ] **Step 5: Сгенерировать миграцию**

```bash
dotnet ef migrations add AddDcPayLinkConfig --project src/AFK4.Platform.Api --no-build
```
Проверить `Up`: создаёт таблицу `dc_paylink_configs` с уникальным индексом `(OrganizationId, BranchId)`. Если `Up` пустой — модель не пересобралась (повторить Step 4 → Step 5).

- [ ] **Step 6: Собрать + прогнать сьют**

```bash
dotnet build src/AFK4.Platform.Api && dotnet test tests/AFK4.Platform.Api.Tests
```
Expected: Build succeeded, Failed: 0.

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "feat(payments): DcPayLinkConfig entity + миграция + DTO"
```

---

## Task 5: Эндпоинты конфига DC (`/api/owner/dc-config`)

**Files:**
- Create: `src/AFK4.Platform.Api/Endpoints/DcConfigEndpoints.cs`
- Modify: `Program.cs` (маппинг), `src/AFK4.Platform.Api/Audit/AuditActionNames.cs` (новое действие)
- Test: `tests/AFK4.Platform.Api.Tests/DcConfigEndpointsTests.cs`

**Interfaces:**
- Consumes: `DcPayLinkConfigEntity`, `DcPayLinkConfigDto`, `UpdateDcPayLinkConfigRequest`, `ISecretProtector`, `StaffAuthorizationService.RequireOrganizationPermission`.
- Produces: `GET /api/owner/dc-config` → `DcPayLinkConfigDto`; `POST /api/owner/dc-config` → `DcPayLinkConfigDto`. Метод расширения `MapDcConfigEndpoints`.

Зеркалит `EskhataConfigEndpoints` (тот же скелет авторизации/аудита; секрет наружу не отдаётся).

- [ ] **Step 1: Написать падающий тест**

Опираться на образец `EskhataConfigEndpointsTests.cs` (та же `PlatformApiFactory`, `TestIds`, `StaffAuthTestHelper`). Тест:

```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Shared.Contracts.Payments;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class DcConfigEndpointsTests : IClassFixture<PlatformApiFactory>
{
    private readonly PlatformApiFactory factory;
    public DcConfigEndpointsTests(PlatformApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task Post_ThenGet_StoresCardEncrypted_ReturnsLast4Only()
    {
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthenticateOwnerAsync(client, factory); // как в EskhataConfigEndpointsTests

        var post = await client.PostAsJsonAsync("/api/owner/dc-config",
            new UpdateDcPayLinkConfigRequest("1234567890123456", "AFK4-{ref}", true));
        Assert.Equal(HttpStatusCode.OK, post.StatusCode);
        var dto = await post.Content.ReadFromJsonAsync<DcPayLinkConfigDto>();
        Assert.True(dto!.CardSet);
        Assert.Equal("3456", dto.CardLast4);
        Assert.True(dto.IsActive);

        var get = await client.GetFromJsonAsync<DcPayLinkConfigDto>("/api/owner/dc-config");
        Assert.Equal("3456", get!.CardLast4);
        Assert.True(get.CardSet);

        // PAN зашифрован в БД, не хранится в открытом виде.
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var row = await db.DcPayLinkConfigs.AsNoTracking().SingleAsync();
        Assert.DoesNotContain("1234567890123456", row.ReceivingCardEncrypted);
    }

    [Fact]
    public async Task Post_EmptyCardOnFirstSave_Fails()
    {
        using var client = factory.CreateClient();
        await StaffAuthTestHelper.AuthenticateOwnerAsync(client, factory);
        var post = await client.PostAsJsonAsync("/api/owner/dc-config",
            new UpdateDcPayLinkConfigRequest(null, "AFK4-{ref}", true));
        Assert.Equal(HttpStatusCode.BadRequest, post.StatusCode);
    }
}
```
(Если хелпер аутентификации называется иначе — взять точную сигнатуру из `EskhataConfigEndpointsTests.cs`.)

- [ ] **Step 2: Запустить — упадёт**

```bash
dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~DcConfigEndpointsTests"
```
Expected: FAIL (404 — эндпоинтов нет).

- [ ] **Step 3: Реализовать эндпоинты**

```csharp
using AFK4.Platform.Api.Audit;
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Security;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Payments;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

// Конфиг приёма DushanbeCity, org-level (BranchId=null), гейт ManagePaymentGateways.
// Номер карты пишется шифрованным и наружу не возвращается — GET отдаёт только CardLast4 + CardSet.
internal static class DcConfigEndpoints
{
    public static void MapDcConfigEndpoints(this WebApplication app)
    {
        app.MapGet("/api/owner/dc-config", async (
            StaffAuthorizationService authorizationService,
            PlatformDbContext db,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(StaffPermissionNames.ManagePaymentGateways);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var orgId = authorization.StaffContext!.OrganizationId;
            var row = await db.DcPayLinkConfigs.AsNoTracking()
                .SingleOrDefaultAsync(c => c.OrganizationId == orgId && c.BranchId == null, ct);

            return Results.Ok(row is null
                ? new DcPayLinkConfigDto(false, string.Empty, "AFK4-{ref}", false)
                : new DcPayLinkConfigDto(
                    !string.IsNullOrEmpty(row.ReceivingCardEncrypted),
                    row.CardLast4,
                    row.CommentTemplate,
                    row.IsActive));
        });

        app.MapPost("/api/owner/dc-config", async (
            UpdateDcPayLinkConfigRequest request,
            StaffAuthorizationService authorizationService,
            ISecretProtector secretProtector,
            IAuditRecordWriter auditRecordWriter,
            TimeProvider timeProvider,
            PlatformDbContext db,
            CancellationToken ct) =>
        {
            var authorization = authorizationService.RequireOrganizationPermission(StaffPermissionNames.ManagePaymentGateways);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var orgId = authorization.StaffContext!.OrganizationId;
            var row = await db.DcPayLinkConfigs.SingleOrDefaultAsync(c => c.OrganizationId == orgId && c.BranchId == null, ct);

            var errors = ValidateRequest(request, hasStoredCard: row is not null && !string.IsNullOrEmpty(row.ReceivingCardEncrypted));
            if (errors.Count > 0) return Results.ValidationProblem(errors);

            var now = timeProvider.GetUtcNow();
            if (row is null)
            {
                row = new DcPayLinkConfigEntity
                {
                    DcPayLinkConfigId = Guid.NewGuid(),
                    OrganizationId = orgId,
                    BranchId = null,
                    CreatedAtUtc = now
                };
                db.DcPayLinkConfigs.Add(row);
            }

            if (!string.IsNullOrWhiteSpace(request.CardNumber))
            {
                var digits = new string(request.CardNumber.Where(char.IsDigit).ToArray());
                row.ReceivingCardEncrypted = secretProtector.Protect(digits);
                row.CardLast4 = digits[^4..];
            }
            row.CommentTemplate = string.IsNullOrWhiteSpace(request.CommentTemplate) ? "AFK4-{ref}" : request.CommentTemplate.Trim();
            row.IsActive = request.IsActive;
            row.UpdatedAtUtc = now;
            await db.SaveChangesAsync(ct);

            var staff = authorization.StaffContext!;
            await auditRecordWriter.WriteAsync(new AuditRecordWriteRequest(
                orgId, BranchId: null, ActorStaffUserId: staff.StaffUserId,
                Action: AuditActionNames.UpdateDcPayLinkConfig,
                TargetType: "DcPayLinkConfig", TargetId: orgId.ToString("N"),
                Outcome: AuditOutcome.Succeeded, SourceApp: "PlatformApi",
                // Карту не логируем: только факт ротации + last4.
                DetailsJson: System.Text.Json.JsonSerializer.Serialize(new
                {
                    CardRotated = !string.IsNullOrWhiteSpace(request.CardNumber),
                    row.CardLast4, row.CommentTemplate, row.IsActive
                })), ct);

            return Results.Ok(new DcPayLinkConfigDto(
                !string.IsNullOrEmpty(row.ReceivingCardEncrypted), row.CardLast4, row.CommentTemplate, row.IsActive));
        });
    }

    private static Dictionary<string, string[]> ValidateRequest(UpdateDcPayLinkConfigRequest request, bool hasStoredCard)
    {
        var errors = new Dictionary<string, string[]>();
        var digits = new string((request.CardNumber ?? string.Empty).Where(char.IsDigit).ToArray());

        // Карта обязательна на первом сохранении; пустая на последующих — сохраняет прежнюю.
        if (!hasStoredCard && digits.Length < 12)
        {
            errors["cardNumber"] = ["Card number is required (at least 12 digits)."];
        }
        else if (digits.Length > 0 && digits.Length < 12)
        {
            errors["cardNumber"] = ["Card number must have at least 12 digits."];
        }

        if (string.IsNullOrWhiteSpace(request.CommentTemplate) || !request.CommentTemplate.Contains("{ref}", StringComparison.Ordinal))
        {
            errors["commentTemplate"] = ["Comment template must contain {ref}."];
        }

        return errors;
    }
}
```

- [ ] **Step 4: Добавить `AuditActionNames.UpdateDcPayLinkConfig`**

В `src/AFK4.Platform.Api/Audit/AuditActionNames.cs` добавить константу рядом с `UpdateEskhataMerchantConfig`:

```csharp
    public const string UpdateDcPayLinkConfig = "payments.dc_config.update";
```

- [ ] **Step 5: Замапить в `Program.cs`**

Рядом с `app.MapEskhataConfigEndpoints();` добавить:

```csharp
app.MapDcConfigEndpoints();
```

- [ ] **Step 6: Запустить — пройдёт + собрать**

```bash
dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~DcConfigEndpointsTests"
```
Expected: PASS (2 теста).

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "feat(payments): эндпоинты конфига DC (/api/owner/dc-config)"
```

---

## Task 6: POS-эндпоинты DC-пополнения (create + cancel)

**Files:**
- Create: `src/AFK4.Platform.Api/Endpoints/DcTopUpEndpoints.cs`, `src/AFK4.Shared.Contracts/Payments/DcTopUpDtos.cs`
- Modify: `Program.cs` (маппинг)
- Test: `tests/AFK4.Platform.Api.Tests/DcTopUpEndpointsTests.cs`

**Interfaces:**
- Consumes: `DcPayLink`, `DcPayLinkConfigEntity`, `PaymentIntentEntity`, `ISecretProtector`, `StaffAuthorizationService.RequireBranchPermissionAsync`, `StaffPermissionNames.TopUpWallet`.
- Produces:
  - `POST /api/branches/{branchId:guid}/pos/dc-topups` (body `CreateDcTopUpRequest(Guid PlayerAccountId, long AmountMinorUnits, string? CurrencyCode)`) → `DcTopUpDto(Guid IntentId, string PayUrl, string Comment, long AmountMinorUnits, string CurrencyCode, string CardLast4)`.
  - `POST /api/branches/{branchId:guid}/pos/dc-topups/{intentId:guid}/cancel` → `204` / `409`.
  - Подтверждение — существующий `POST /api/wallet/top-up-intents/{intentId}/fulfil` (НЕ дублировать).

- [ ] **Step 1: Создать DTO**

```csharp
namespace AFK4.Shared.Contracts.Payments;

public sealed record CreateDcTopUpRequest(
    Guid PlayerAccountId,
    long AmountMinorUnits,
    string? CurrencyCode);

public sealed record DcTopUpDto(
    Guid IntentId,
    string PayUrl,
    string Comment,
    long AmountMinorUnits,
    string CurrencyCode,
    string CardLast4);
```

- [ ] **Step 2: Написать падающие тесты**

Образец инфраструктуры — `EskhataTopUpIntentTests.cs` / `WalletEndpoints`-тесты (`PlatformApiFactory`, `SeedPlayerAsync`, `StaffAuthTestHelper`). Использовать реальные хелперы репозитория (НЕ выдуманные):

```csharp
using System.Net;
using System.Net.Http.Json;
using AFK4.Shared.Contracts.Payments;
using Microsoft.EntityFrameworkCore;
using Xunit;

namespace AFK4.Platform.Api.Tests;

public class DcTopUpEndpointsTests : IClassFixture<PlatformApiFactory>
{
    private readonly PlatformApiFactory factory;
    public DcTopUpEndpointsTests(PlatformApiFactory factory) => this.factory = factory;

    [Fact]
    public async Task Create_BuildsPayLink_AndPendingIntent()
    {
        using var client = factory.CreateClient();
        var (branchId, playerId) = await DcTestSetup.SeedActiveConfigAndPlayerAsync(client, factory);

        var resp = await client.PostAsJsonAsync($"/api/branches/{branchId}/pos/dc-topups",
            new CreateDcTopUpRequest(playerId, 5000, "TJS"));
        Assert.Equal(HttpStatusCode.OK, resp.StatusCode);
        var dto = await resp.Content.ReadFromJsonAsync<DcTopUpDto>();
        Assert.StartsWith("http://pay.dc.tj/?A=", dto!.PayUrl);
        Assert.Contains("&s=50.00&", dto.PayUrl);
        Assert.Equal(5000, dto.AmountMinorUnits);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var intent = await db.PaymentIntents.AsNoTracking().SingleAsync(i => i.PaymentIntentId == dto.IntentId);
        Assert.Equal("dc", intent.Method);
        Assert.Equal("pending", intent.State);
        Assert.Equal(dto.PayUrl, intent.GatewayPayUrl);
    }

    [Fact]
    public async Task Create_NoActiveConfig_Returns409()
    {
        using var client = factory.CreateClient();
        var (branchId, playerId) = await DcTestSetup.SeedPlayerNoConfigAsync(client, factory);
        var resp = await client.PostAsJsonAsync($"/api/branches/{branchId}/pos/dc-topups",
            new CreateDcTopUpRequest(playerId, 5000, "TJS"));
        Assert.Equal(HttpStatusCode.Conflict, resp.StatusCode);
    }

    [Fact]
    public async Task Confirm_ViaFulfil_CreditsWalletOnce()
    {
        using var client = factory.CreateClient();
        var (branchId, playerId) = await DcTestSetup.SeedActiveConfigAndPlayerAsync(client, factory);
        var create = await (await client.PostAsJsonAsync($"/api/branches/{branchId}/pos/dc-topups",
            new CreateDcTopUpRequest(playerId, 5000, "TJS"))).Content.ReadFromJsonAsync<DcTopUpDto>();

        var f1 = await client.PostAsync($"/api/wallet/top-up-intents/{create!.IntentId}/fulfil", null);
        var f2 = await client.PostAsync($"/api/wallet/top-up-intents/{create.IntentId}/fulfil", null);
        Assert.Equal(HttpStatusCode.OK, f1.StatusCode);
        Assert.Equal(HttpStatusCode.OK, f2.StatusCode); // идемпотентно

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var summary = await WalletTestHelper.GetBalanceMinorAsync(db, playerId);
        Assert.Equal(5000, summary); // зачислено ровно раз
    }

    [Fact]
    public async Task Cancel_PendingIntent_SetsCancelled()
    {
        using var client = factory.CreateClient();
        var (branchId, playerId) = await DcTestSetup.SeedActiveConfigAndPlayerAsync(client, factory);
        var create = await (await client.PostAsJsonAsync($"/api/branches/{branchId}/pos/dc-topups",
            new CreateDcTopUpRequest(playerId, 5000, "TJS"))).Content.ReadFromJsonAsync<DcTopUpDto>();

        var cancel = await client.PostAsync($"/api/branches/{branchId}/pos/dc-topups/{create!.IntentId}/cancel", null);
        Assert.Equal(HttpStatusCode.NoContent, cancel.StatusCode);

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<PlatformDbContext>();
        var intent = await db.PaymentIntents.AsNoTracking().SingleAsync(i => i.PaymentIntentId == create.IntentId);
        Assert.Equal("cancelled", intent.State);
    }
}
```

⚠️ Хелперы `DcTestSetup.*` и `WalletTestHelper.GetBalanceMinorAsync` — тонкие обёртки, которые имплементер пишет поверх РЕАЛЬНЫХ примитивов репозитория (см. `EskhataTopUpIntentTests.cs`, `WalletEndpoints`-тесты): аутентификация staff с правом `billing.wallet.top_up`, сидинг филиала/игрока (активного), запись `DcPayLinkConfigEntity` (IsActive=true, карта через `ISecretProtector`), чтение баланса через `LedgerBalanceProjector`/wallet-summary. Точные сигнатуры сидеров скопировать из существующих тестов, НЕ изобретать.

- [ ] **Step 3: Запустить — упадёт**

```bash
dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~DcTopUpEndpointsTests"
```
Expected: FAIL (404).

- [ ] **Step 4: Реализовать эндпоинты**

```csharp
using AFK4.Platform.Api.Data;
using AFK4.Platform.Api.Identity;
using AFK4.Platform.Api.Payments.Dc;
using AFK4.Platform.Api.Security;
using AFK4.Shared.Contracts.Identity;
using AFK4.Shared.Contracts.Payments;
using Microsoft.EntityFrameworkCore;

namespace AFK4.Platform.Api.Endpoints;

// DC-пополнение в Кассе: кассир заводит намерение → ссылка/QR → игрок платит → кассир
// подтверждает существующим /api/wallet/top-up-intents/{id}/fulfil. Здесь только create + cancel.
internal static class DcTopUpEndpoints
{
    public static void MapDcTopUpEndpoints(this WebApplication app)
    {
        app.MapPost("/api/branches/{branchId:guid}/pos/dc-topups", async (
            Guid branchId,
            CreateDcTopUpRequest request,
            StaffAuthorizationService authorizationService,
            ISecretProtector secretProtector,
            PlatformDbContext db,
            TimeProvider timeProvider,
            CancellationToken ct) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, StaffPermissionNames.TopUpWallet, ct);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            if (request.AmountMinorUnits <= 0)
                return Results.BadRequest(new { Error = "Amount must be greater than zero." });

            var orgId = authorization.StaffContext!.OrganizationId;
            var config = await db.DcPayLinkConfigs.AsNoTracking()
                .SingleOrDefaultAsync(c => c.OrganizationId == orgId && c.BranchId == null && c.IsActive, ct);
            if (config is null || string.IsNullOrEmpty(config.ReceivingCardEncrypted))
                return Results.Json(new { Error = "dc_not_configured" }, statusCode: StatusCodes.Status409Conflict);

            var player = await db.PlayerAccounts.AsNoTracking()
                .SingleOrDefaultAsync(p => p.PlayerAccountId == request.PlayerAccountId && p.OrganizationId == orgId, ct);
            if (player is null) return Results.NotFound(new { Error = "Player was not found." });

            var currencyCode = string.IsNullOrWhiteSpace(request.CurrencyCode)
                ? "TJS" : request.CurrencyCode.Trim().ToUpperInvariant();

            var intentId = Guid.NewGuid();
            var reference = intentId.ToString("N")[..8];
            var comment = DcPayLink.BuildComment(config.CommentTemplate, reference);
            var card = secretProtector.Unprotect(config.ReceivingCardEncrypted);
            var payUrl = DcPayLink.BuildUrl(card, request.AmountMinorUnits, comment);

            var now = timeProvider.GetUtcNow();
            var intent = new PaymentIntentEntity
            {
                PaymentIntentId = intentId,
                PlayerAccountId = request.PlayerAccountId,
                OrganizationId = orgId,
                BranchId = branchId,
                AmountMinorUnits = request.AmountMinorUnits,
                CurrencyCode = currencyCode,
                Purpose = "wallet_topup",
                State = "pending",
                Method = "dc",
                GatewayPayUrl = payUrl,
                GatewayComment = comment,
                CreatedAtUtc = now
            };
            db.PaymentIntents.Add(intent);
            await db.SaveChangesAsync(ct);

            return Results.Ok(new DcTopUpDto(
                intentId, payUrl, comment, request.AmountMinorUnits, currencyCode, config.CardLast4));
        });

        app.MapPost("/api/branches/{branchId:guid}/pos/dc-topups/{intentId:guid}/cancel", async (
            Guid branchId,
            Guid intentId,
            StaffAuthorizationService authorizationService,
            PlatformDbContext db,
            CancellationToken ct) =>
        {
            var authorization = await authorizationService.RequireBranchPermissionAsync(
                branchId, StaffPermissionNames.TopUpWallet, ct);
            if (!authorization.IsAuthenticated) return Results.Unauthorized();
            if (!authorization.IsAllowed) return Results.StatusCode(StatusCodes.Status403Forbidden);

            var orgId = authorization.StaffContext!.OrganizationId;
            var intent = await db.PaymentIntents.SingleOrDefaultAsync(
                i => i.PaymentIntentId == intentId && i.OrganizationId == orgId && i.BranchId == branchId, ct);
            if (intent is null || intent.Method != "dc")
                return Results.NotFound(new { Error = "DC top-up was not found." });
            if (intent.State != "pending")
                return Results.Conflict(new { Error = "Only a pending DC top-up can be cancelled." });

            intent.State = "cancelled";
            await db.SaveChangesAsync(ct);
            return Results.NoContent();
        });
    }
}
```

- [ ] **Step 5: Замапить в `Program.cs`**

Рядом с `app.MapDcConfigEndpoints();`:

```csharp
app.MapDcTopUpEndpoints();
```

- [ ] **Step 6: Запустить — пройдёт + полный сьют**

```bash
dotnet test tests/AFK4.Platform.Api.Tests --filter "FullyQualifiedName~DcTopUpEndpointsTests"
dotnet test tests/AFK4.Platform.Api.Tests
```
Expected: целевые PASS; полный сьют Failed: 0.

- [ ] **Step 7: Commit**

```bash
git add -A && git commit -m "feat(payments): POS-эндпоинты DC-пополнения (create + cancel), confirm через fulfil"
```

---

## Task 7: Operator.App — форма конфига DC

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/management/destinations/payments/DcTransferForm.tsx`, `src/api/clients/dcConfig.ts`
- Modify: `src/api/clients/index.ts`, `src/operatorApiClients.ts`, `src/management/destinations/payments/PaymentMethodsSection.tsx`, `locales/{ru,en,tg}.json`
- Test: `src/AFK4.Operator.App.Web/src/management/destinations/payments/DcTransferForm.test.tsx`

**Interfaces:**
- Consumes: `DcPayLinkConfigDto`, `UpdateDcPayLinkConfigRequest` (форма ↔ `/api/owner/dc-config`).
- Produces: `backend.dcConfig.{get,update}`; компонент `<DcTransferForm backend={...} />` в PaymentMethodsSection.

Зеркалит `EskhataGatewayForm.tsx` (тот же паттерн: раскрытие «Настроить», свой «Сохранить», секрет не приходит — только `cardSet`/`cardLast4`).

- [ ] **Step 1: Клиент `dcConfig.ts`**

Опираться на `EskhataGatewayForm`-клиент (`clients.eskhataConfig`). Создать:

```ts
import type { ApiClient, Guid } from './types'; // точные импорты — как в соседних clients/*.ts

export interface DcPayLinkConfigDto {
  cardSet: boolean;
  cardLast4: string;
  commentTemplate: string;
  isActive: boolean;
}
export interface UpdateDcPayLinkConfigRequest {
  cardNumber: string | null;
  commentTemplate: string;
  isActive: boolean;
}

export function createDcConfigClient(api: ApiClient) {
  return {
    get(): Promise<DcPayLinkConfigDto> {
      return api.get<DcPayLinkConfigDto>('/api/owner/dc-config');
    },
    update(request: UpdateDcPayLinkConfigRequest): Promise<DcPayLinkConfigDto> {
      return api.post<DcPayLinkConfigDto, UpdateDcPayLinkConfigRequest>('/api/owner/dc-config', request);
    },
  };
}
```
(Точную форму `ApiClient`/импортов взять из существующего `api/clients/eskhataConfig.ts`.)

- [ ] **Step 2: Зарегистрировать клиент**

- `src/api/clients/index.ts`: импорт + `dcConfig: createDcConfigClient(api),`.
- `src/operatorApiClients.ts`: `export * from './api/clients/dcConfig';`.

- [ ] **Step 3: Написать падающий тест формы**

Образец — `EskhataGatewayForm.test.tsx` (happy-dom, мок `backend.dcConfig`). Тест: рендер свёрнутого блока со статусом; «Настроить» раскрывает форму; сохранение с картой/шаблоном зовёт `update`; при `cardSet` поле карты показывает `••••{last4}` и пустой ввод не обязателен.

```tsx
import { describe, it, expect, mock } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { DcTransferForm } from './DcTransferForm';
// ... построить backend-мок как в EskhataGatewayForm.test.tsx

describe('DcTransferForm', () => {
  it('сохраняет карту и шаблон', async () => {
    const update = mock(async () => ({ cardSet: true, cardLast4: '3456', commentTemplate: 'AFK4-{ref}', isActive: true }));
    const backend = makeBackend({ get: async () => ({ cardSet: false, cardLast4: '', commentTemplate: 'AFK4-{ref}', isActive: false }), update });
    render(<DcTransferForm backend={backend} />);
    fireEvent.click(await screen.findByText(/настроить/i));
    fireEvent.change(screen.getByLabelText(/карт/i), { target: { value: '1234567890123456' } });
    fireEvent.click(screen.getByRole('button', { name: /сохранить/i }));
    await waitFor(() => expect(update).toHaveBeenCalled());
  });
});
```
(Точный `makeBackend`/утилиты — скопировать из `EskhataGatewayForm.test.tsx`.)

- [ ] **Step 4: Запустить — упадёт**

```bash
cd src/AFK4.Operator.App.Web && bun test DcTransferForm.test.tsx
```
Expected: FAIL (нет компонента).

- [ ] **Step 5: Реализовать `DcTransferForm.tsx`**

Скопировать структуру `EskhataGatewayForm.tsx`, заменив поля на: номер карты приёма (`type="text"`, `inputMode="numeric"`, `placeholder` при `cardSet` = `••••{cardLast4}`), шаблон комментария (`text`, дефолт `AFK4-{ref}`, валидация — должен содержать `{ref}`), тумблер «Включён». Клиент — `backend`-обёртка `dcConfig`. Валидность: `cardSet || cardNumber.trim().length >= 12`, и `commentTemplate.includes('{ref}')`. Тексты — через `t('op.dc.*')`. Иконка — `CreditCard` (lucide), как у Eskhata. Namespace-классы `payset-*` переиспользуются.

- [ ] **Step 6: Встроить в `PaymentMethodsSection.tsx`**

В слот, освобождённый Task 2 (под Eskhata-формой), добавить:

```tsx
        <div className="payset-divider" />
        <div className="payset-subhead">{t('op.dc.subhead')}</div>
        <p className="payset-note">{t('op.dc.note')}</p>
        <DcTransferForm backend={backend} />
```
+ импорт `import { DcTransferForm } from './DcTransferForm';`.

- [ ] **Step 7: i18n-ключи `op.dc.*`**

В `locales/{ru,en,tg}.json` добавить (реальный перевод, tg — таджикский):
```
op.dc.subhead, op.dc.note, op.dc.title, op.dc.configure, op.dc.card, op.dc.cardSet,
op.dc.commentTemplate, op.dc.active, op.dc.feedbackLabel, op.dc.invalid, op.dc.statusConfigured, op.dc.note
```
Затем `cd packages/i18n && bun run gen && cd ../..`.

- [ ] **Step 8: Запустить — пройдёт + гейты**

```bash
cd src/AFK4.Operator.App.Web && bun test DcTransferForm.test.tsx && bun test && bun run build
```
Expected: целевой PASS; общий `bun test` 0 fail; build ✓.

- [ ] **Step 9: Commit**

```bash
cd /home/fedya/projects/afk4.net && git add -A
git commit -m "feat(operator): форма конфига DC (карта приёма + шаблон комментария) в Платежах"
```

---

## Task 8: Operator.App — DC-пополнение в Кассе (QR + подтверждение)

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/players/DcTopUpDialog.tsx`, `src/api/clients/dcTopUps.ts`
- Modify: `src/api/clients/index.ts`, `src/operatorApiClients.ts`, `src/players/ClientDrawer.tsx` (кнопка-триггер в блоке пополнения), `src/AFK4.Operator.App.Web/package.json` (+`qrcode`), `locales/{ru,en,tg}.json`
- Test: `src/AFK4.Operator.App.Web/src/players/DcTopUpDialog.test.tsx`

**Interfaces:**
- Consumes: `DcTopUpDto`, `CreateDcTopUpRequest`; POST create/cancel + существующий fulfil.
- Produces: `backend.dcTopUps.{create,cancel,confirm}`; `<DcTopUpDialog>` открывается кнопкой «DushanbeCity (перевод)» в пополнении кошелька.

- [ ] **Step 1: Добавить зависимость `qrcode`**

```bash
cd src/AFK4.Operator.App.Web
# версия — как в Player.Shell.Web/package.json (qrcode ^1.5.4) + типы
bun add qrcode@^1.5.4 && bun add -d @types/qrcode
```

- [ ] **Step 2: Клиент `dcTopUps.ts`**

```ts
import type { ApiClient, Guid } from './types';

export interface DcTopUpDto {
  intentId: Guid;
  payUrl: string;
  comment: string;
  amountMinorUnits: number;
  currencyCode: string;
  cardLast4: string;
}
export interface CreateDcTopUpRequest {
  playerAccountId: Guid;
  amountMinorUnits: number;
  currencyCode: string;
}

export function createDcTopUpClient(api: ApiClient) {
  return {
    create(branchId: Guid, request: CreateDcTopUpRequest): Promise<DcTopUpDto> {
      return api.post<DcTopUpDto, CreateDcTopUpRequest>(`/api/branches/${branchId}/pos/dc-topups`, request);
    },
    cancel(branchId: Guid, intentId: Guid): Promise<void> {
      return api.post<void, undefined>(`/api/branches/${branchId}/pos/dc-topups/${intentId}/cancel`, undefined);
    },
    confirm(intentId: Guid): Promise<unknown> {
      return api.post<unknown, undefined>(`/api/wallet/top-up-intents/${intentId}/fulfil`, undefined);
    },
  };
}
```
(Точные типы `ApiClient`/`api.post` — из соседних `api/clients/*.ts`.)

- [ ] **Step 3: Зарегистрировать клиент**

- `src/api/clients/index.ts`: импорт + `dcTopUps: createDcTopUpClient(api),`.
- `src/operatorApiClients.ts`: `export * from './api/clients/dcTopUps';`.

- [ ] **Step 4: Написать падающий тест диалога**

```tsx
import { describe, it, expect, mock } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { DcTopUpDialog } from './DcTopUpDialog';

describe('DcTopUpDialog', () => {
  it('создаёт намерение, рендерит QR, подтверждает', async () => {
    const create = mock(async () => ({ intentId: 'i1', payUrl: 'http://pay.dc.tj/?A=1&s=50.00&c=AFK4-abc&f1=133', comment: 'AFK4-abc', amountMinorUnits: 5000, currencyCode: 'TJS', cardLast4: '3456' }));
    const confirm = mock(async () => ({}));
    const backend = makeBackend({ dcTopUps: { create, cancel: mock(async () => {}), confirm } });
    render(<DcTopUpDialog backend={backend} branchId="b1" playerAccountId="p1" onClose={() => {}} onCredited={() => {}} />);
    fireEvent.change(screen.getByLabelText(/сумм/i), { target: { value: '50' } });
    fireEvent.click(screen.getByRole('button', { name: /показать qr|создать/i }));
    await waitFor(() => expect(create).toHaveBeenCalled());
    await screen.findByRole('img'); // QR отрисован
    fireEvent.click(screen.getByRole('button', { name: /оплата получена/i }));
    await waitFor(() => expect(confirm).toHaveBeenCalledWith('i1'));
  });
});
```

- [ ] **Step 5: Запустить — упадёт**

```bash
cd src/AFK4.Operator.App.Web && bun test DcTopUpDialog.test.tsx
```
Expected: FAIL.

- [ ] **Step 6: Реализовать `DcTopUpDialog.tsx`**

Диалог с тремя фазами: (1) ввод суммы (мажорные единицы → minor через `*100`), кнопка «Показать QR» зовёт `backend.dcTopUps.create(branchId, {playerAccountId, amountMinorUnits, currencyCode:'TJS'})`; (2) показ QR — `QRCode.toDataURL(dto.payUrl).then(setQr)` (как в `Player.Shell.Web/src/screens/TopUpScreen.tsx:30`), рядом `dto.comment`, сумма, `••••{dto.cardLast4}`, кнопки «Оплата получена» → `confirm(intentId)` → `onCredited()` + `onClose()`; «Отмена» → `cancel(branchId, intentId)` → `onClose()`; (3) состояния saving/feedback через `useFeedbackToasts` (как в других экранах). Мгновенный feedback на клики, отложенный спиннер. Тексты `t('op.dc.topup.*')`.

- [ ] **Step 7: Триггер в `ClientDrawer.tsx`**

В блоке пополнения кошелька (`WalletTopUpControls`/рядом с текущим счётчиком суммы) добавить кнопку «DushanbeCity (перевод)», открывающую `DcTopUpDialog` (state `dcOpen`), передав `branchId`, `playerAccountId`, `onCredited` = обновление баланса (тот же рефетч, что после counter-пополнения). Следовать существующему паттерну открытия модалок в этом файле.

- [ ] **Step 8: i18n-ключи `op.dc.topup.*`**

`op.dc.topup.open`, `.amount`, `.showQr`, `.received`, `.cancel`, `.hint`, `.comment`, `.cardLast4`, `.feedbackLabel` — в `locales/{ru,en,tg}.json`, затем `cd packages/i18n && bun run gen`.

- [ ] **Step 9: Запустить — пройдёт + гейты**

```bash
cd src/AFK4.Operator.App.Web && bun test DcTopUpDialog.test.tsx && bun test && bun run build
```
Expected: целевой PASS; общий `bun test` 0 fail; build ✓ (важно: `tsc -b` тайпчекает и тест-файлы — типизировать bun-моки).

- [ ] **Step 10: Commit**

```bash
cd /home/fedya/projects/afk4.net && git add -A
git commit -m "feat(operator): DC-пополнение в Кассе — QR + подтверждение кассиром"
```

---

## Финальные гейты слайса

- [ ] `dotnet build` + `dotnet test tests/AFK4.Platform.Api.Tests` — зелёные, Failed: 0.
- [ ] `cd src/AFK4.Operator.App.Web && bun test && bun run build` — зелёные.
- [ ] `grep -rn "DcGate\|BranchPaymentGateway\|payments_cards\|PaymentGatewaysWorkspace" src` — пусто (кроме новых `Dc*`-имён и `dc-config`/`dc-topups`).
- [ ] Проверить, что `Player.Shell.Web` НЕ изменён (`git diff --stat` не содержит `Player.Shell.Web`).
