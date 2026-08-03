# Platform Control UI Redesign Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Превратить Platform Control из безликого реестра юрлиц в панель наблюдения за парком клубов с явными операционными рычагами, на общем визуальном языке AFK4.

**Architecture:** Главный экран — один раздел «Клубы» с тремя видами; строка = сеть (организация), раскрывается в клубы (филиалы), порядок задаёт тревога. Пульс собирается одним серверным эндпоинтом (без N+1), правила тревог живут на бэке. Карточка клиента — постоянный «паспорт» слева и переключаемые вкладки справа; операционные рычаги лежат рядом со своим предметом.

**Tech Stack:** React 19 + TypeScript + Vite + Tailwind v4 (`AFK4.PlatformControl.Web`), `bun test` + happy-dom, ASP.NET Core minimal APIs + EF Core/Npgsql (`AFK4.Platform.Api`), xUnit.

## Global Constraints

- Спека: `docs/superpowers/specs/2026-08-03-platform-control-ui-redesign-design.md`.
- Все строки — через `@afk4/i18n`, ключи добавляются одновременно в `locales/ru.json`, `locales/en.json`, `locales/tg.json`, затем `cd packages/i18n && "$BUN" run gen`. Хардкод-строк в UI быть не должно.
- Таджикский перевод должен быть настоящим таджикским: guard-тест падает на `tg === ru` вне whitelist заимствований.
- `BUN=/home/fedya/.bun/bin/bun` — bun не на PATH, вызывать полным путём.
- Зелёный `bun test` не равен зелёной сборке: `bun run build` = `tsc -b && vite build` и тайпчекает в том числе тест-файлы. Финальная задача обязана прогнать сборку.
- Деньги в контрактах — минорные единицы (`*MinorUnits`); на границе UI переводить в мажорные перед `formatCurrency`.
- Цвета берутся только из `@afk4/tokens`; собственных hex-значений в компонентах не появляется.
- Правила тревог вычисляет бэк. Фронт не имеет права выводить уровень тревоги из сырых чисел.
- Каждая платформенная мутация пишет аудит через `WritePlatformAuditAsync` с исходом `Succeeded`/`Denied` — по образцу существующих эндпоинтов.
- Порог «молчания агента» — конфигурируемая величина на бэке, не константа в UI.

---

## Фаза 1 — визуальный фундамент и пульс

### Task 1: Перевод панели на общие токены AFK4

**Files:**
- Modify: `src/AFK4.PlatformControl.Web/package.json` (добавить зависимость `@afk4/tokens`)
- Modify: `src/AFK4.PlatformControl.Web/src/index.css` (удалить собственные токены, импортировать общие)
- Modify: `src/AFK4.PlatformControl.Web/src/main.tsx` (порядок импорта стилей)
- Test: `src/AFK4.PlatformControl.Web/src/theme/theme.test.ts`

**Interfaces:**
- Consumes: пакет `@afk4/tokens` (workspace), файл `packages/tokens/tokens.css` с переменными `--accent`, `--surface-*`, `--text-*`, `--border-*`, `--focus-ring`.
- Produces: в панели доступны CSS-переменные `@afk4/tokens`; Tailwind-алиасы `--color-*` в `@theme inline` продолжают работать, но резолвятся в общие токены.

- [ ] **Step 1: Написать падающий тест на отсутствие собственной палитры**

Добавить в `src/theme/theme.test.ts`:

```ts
import { describe, expect, it } from 'bun:test';
import { readFileSync } from 'node:fs';

describe('design tokens', () => {
  it('does not define a private colour palette', () => {
    const css = readFileSync(new URL('../index.css', import.meta.url), 'utf8');
    expect(css).toContain('@afk4/tokens');
    expect(css).not.toMatch(/#1d4ed8/i);
    expect(css).not.toMatch(/#f6f7f9/i);
  });
});
```

- [ ] **Step 2: Запустить тест и убедиться, что он падает**

Run: `cd src/AFK4.PlatformControl.Web && "$BUN" test src/theme/theme.test.ts`
Expected: FAIL — `index.css` содержит `#1d4ed8` и не содержит импорта токенов.

- [ ] **Step 3: Подключить пакет токенов**

В `package.json` в блок `dependencies` добавить (соседний пример — `src/AFK4.OrganizationAdmin.Web/package.json`):

```json
"@afk4/tokens": "workspace:*",
```

Затем: `cd /home/fedya/projects/afk4.net && "$BUN" install`

- [ ] **Step 4: Заменить палитру в `index.css`**

Первые строки файла:

```css
@import "tailwindcss";
@import "@afk4/tokens/tokens.css";

@custom-variant dark (&:is(.dark *));
```

Удалить целиком блоки `:root { --background … --radius }` и `.dark { … }`. В блоке `@theme inline` заменить значения на общие токены; сохранить имена алиасов, чтобы существующие компоненты не сломались:

```css
@theme inline {
  --color-background: var(--surface-base);
  --color-foreground: var(--text-primary);
  --color-card: var(--surface-raised);
  --color-card-foreground: var(--text-primary);
  --color-muted: var(--surface-muted);
  --color-muted-foreground: var(--text-secondary);
  --color-border: var(--border-subtle);
  --color-input: var(--border-subtle);
  --color-ring: var(--accent-ring);
  --color-primary: var(--accent);
  --color-primary-foreground: var(--text-on-accent);
  --color-primary-weak: var(--surface-accent-soft);
  --color-accent: var(--surface-accent-soft);
  --color-accent-foreground: var(--accent);
  --color-secondary: var(--surface-muted);
  --color-secondary-foreground: var(--text-primary);
  --color-destructive: var(--danger);
  --color-destructive-foreground: var(--text-on-accent);
  --color-popover: var(--surface-raised);
  --color-popover-foreground: var(--text-primary);
  --color-success: var(--success);
  --color-warning: var(--warning);
  --color-danger: var(--danger);
  --radius-lg: var(--radius-md);
  --font-sans: var(--font-sans);
}
```

Точные имена переменных сверить с `packages/tokens/tokens.css` перед правкой: если какого-то имени там нет, использовать ближайшее существующее и не выдумывать новое.

- [ ] **Step 5: Проверить дефолтную тему**

`ThemeProvider` уже управляет классом `dark`. Убедиться, что тёмная тема — стартовое значение: в `src/theme/theme.ts` значение по умолчанию должно быть `'dark'`. Если там `'light'` — поменять и поправить существующий тест ожидания темы в `src/theme/theme.test.ts`.

- [ ] **Step 6: Прогнать тесты темы и всей панели**

Run: `cd src/AFK4.PlatformControl.Web && "$BUN" test`
Expected: PASS. Тесты, ожидавшие старые цвета, поправить на токены — но не ослаблять проверки.

- [ ] **Step 7: Коммит**

```bash
git add src/AFK4.PlatformControl.Web package.json bun.lock
git commit -m "refactor(platform-control): move panel onto shared AFK4 tokens"
```

---

### Task 2: Серверный пульс парка

**Files:**
- Create: `src/AFK4.Shared.Contracts/Platform/Pulse/PlatformPulseContracts.cs`
- Create: `src/AFK4.Platform.Api/Platform/Pulse/IPlatformPulseService.cs`
- Create: `src/AFK4.Platform.Api/Platform/Pulse/EfPlatformPulseService.cs`
- Create: `src/AFK4.Platform.Api/Platform/Pulse/PlatformPulseOptions.cs`
- Create: `src/AFK4.Platform.Api/Endpoints/PlatformPulseEndpoints.cs`
- Modify: `src/AFK4.Platform.Api/Program.cs` (регистрация сервиса, опций и эндпоинта)
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformPulseEndpointTests.cs`
- Test: `tests/AFK4.Shared.Contracts.Tests/Platform/PulseContractSerializationTests.cs`

**Interfaces:**
- Consumes: `PlatformDbContext` (`DeviceEntity.IsOnline`/`LastHeartbeatAtUtc`, `SessionEntity.StartedAtUtc`/`EndedAtUtc`, `ShiftEntity.OpenedAtUtc`/`ClosedAtUtc`, `SeatEntity`, `BranchEntity.Name`/`City`, `InvoiceEntity`), `PlatformAdminAuthorizationService`, `PlatformAdminPermissionNames.ViewOrganizations`.
- Produces: `GET /api/platform/pulse` → `PlatformPulseDto`; типы `PlatformPulseDto`, `PulseOrganizationDto`, `PulseClubDto`, `PulseAlertDto`, константы `PulseAlertKindNames`, `PulseAlertLevelNames`.

- [ ] **Step 1: Написать падающий тест эндпоинта**

Создать `tests/AFK4.Platform.Api.Tests/Platform/PlatformPulseEndpointTests.cs` по образцу `PlatformOrganizationHealthEndpointTests.cs` (тот же `PlatformApiFactory`, `PlatformAdminTestHelper`, `TestIds`):

```csharp
[Fact]
public async Task GetPulse_OrganizationWithSilentAgents_ReturnsCriticalAlert()
{
    await using var factory = new PlatformApiFactory();
    using var client = factory.CreateClient();
    var token = await PlatformAdminTestHelper.SignInAsync(factory, client);
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    await factory.SeedAsync(async db =>
    {
        var branch = await db.Branches.SingleAsync(b => b.BranchId == TestIds.BranchId);
        db.Devices.Add(new DeviceEntity
        {
            DeviceId = Guid.NewGuid(),
            OrganizationId = branch.OrganizationId,
            BranchId = branch.BranchId,
            MachineName = "PC-01",
            DisplayName = "PC-01",
            IsOnline = false,
            LastHeartbeatAtUtc = DateTimeOffset.UtcNow.AddHours(-2),
            EnrolledAtUtc = DateTimeOffset.UtcNow.AddDays(-10)
        });
        await db.SaveChangesAsync();
    });

    var pulse = await client.GetFromJsonAsync<PlatformPulseDto>("/api/platform/pulse");

    var organization = Assert.Single(pulse!.Organizations);
    var club = Assert.Single(organization.Clubs);
    Assert.Equal(0, club.DevicesOnline);
    Assert.Equal(1, club.DevicesTotal);
    Assert.Contains(club.Alerts, alert => alert.Kind == PulseAlertKindNames.AgentSilent);
    Assert.Equal(PulseAlertLevelNames.Critical, organization.AlertLevel);
}

[Fact]
public async Task GetPulse_WithoutPermission_ReturnsForbidden()
{
    await using var factory = new PlatformApiFactory();
    using var client = factory.CreateClient();
    var token = await PlatformAdminTestHelper.SignInAsync(factory, client, permissions: Array.Empty<string>());
    client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);

    var response = await client.GetAsync("/api/platform/pulse");

    Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
}
```

Точные сигнатуры `PlatformApiFactory.SeedAsync` и `PlatformAdminTestHelper.SignInAsync` сверить с соседними тестами и использовать как есть.

- [ ] **Step 2: Запустить тест и убедиться, что он падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~PlatformPulseEndpointTests`
Expected: FAIL — не компилируется, типов `PlatformPulseDto` нет.

- [ ] **Step 3: Описать контракты**

Создать `src/AFK4.Shared.Contracts/Platform/Pulse/PlatformPulseContracts.cs`:

```csharp
namespace AFK4.Shared.Contracts.Platform.Pulse;

public static class PulseAlertLevelNames
{
    public const string Normal = "normal";
    public const string Attention = "attention";
    public const string Critical = "critical";
}

public static class PulseAlertKindNames
{
    public const string AgentSilent = "agent_silent";
    public const string ShiftNotClosed = "shift_not_closed";
    public const string PaymentOverdue = "payment_overdue";
    public const string RolloutFailed = "rollout_failed";
}

public sealed record PulseAlertDto(
    string Kind,
    string Level,
    string? Detail);

public sealed record PulseClubDto(
    Guid BranchId,
    string Name,
    string City,
    int DevicesOnline,
    int DevicesTotal,
    int SeatsOccupied,
    int SeatsTotal,
    bool ShiftOpen,
    DateTimeOffset? ShiftOpenedAtUtc,
    DateTimeOffset? LastHeartbeatAtUtc,
    IReadOnlyList<PulseAlertDto> Alerts);

public sealed record PulseOrganizationDto(
    Guid OrganizationId,
    string Name,
    string Status,
    string PlanCode,
    string SubscriptionStatus,
    string AlertLevel,
    long OutstandingMinorUnits,
    string CurrencyCode,
    IReadOnlyList<PulseAlertDto> Alerts,
    IReadOnlyList<PulseClubDto> Clubs);

public sealed record PlatformPulseDto(
    DateTimeOffset GeneratedAtUtc,
    IReadOnlyList<PulseOrganizationDto> Organizations);
```

- [ ] **Step 4: Добавить контракт-тест сериализации**

Создать `tests/AFK4.Shared.Contracts.Tests/Platform/PulseContractSerializationTests.cs` по образцу `OrganizationHealthContractSerializationTests.cs`: сериализовать `PlatformPulseDto` с одной организацией и одним клубом, проверить camelCase-имена полей `organizations`, `clubs`, `devicesOnline`, `seatsOccupied`, `alerts`, `alertLevel`.

- [ ] **Step 5: Описать опции порога**

Создать `src/AFK4.Platform.Api/Platform/Pulse/PlatformPulseOptions.cs`:

```csharp
namespace AFK4.Platform.Api.Platform.Pulse;

public sealed class PlatformPulseOptions
{
    public const string SectionName = "PlatformPulse";

    public int AgentSilenceThresholdMinutes { get; set; } = 15;

    public int ShiftStaleHours { get; set; } = 24;
}
```

- [ ] **Step 6: Реализовать сервис пульса**

Создать `src/AFK4.Platform.Api/Platform/Pulse/IPlatformPulseService.cs`:

```csharp
namespace AFK4.Platform.Api.Platform.Pulse;

public interface IPlatformPulseService
{
    Task<PlatformPulseDto> GetPulseAsync(CancellationToken cancellationToken);
}
```

Создать `EfPlatformPulseService.cs`. Требования к реализации:

- три сгруппированных запроса вместо запроса на клуб: устройства (`GroupBy(BranchId)` → всего и онлайн с учётом порога `LastHeartbeatAtUtc >= now - threshold`), активные сессии (`EndedAtUtc == null && StartedAtUtc != null`, `GroupBy(BranchId)`), места (`GroupBy(BranchId)`), открытые смены (`ClosedAtUtc == null`), просроченные счета (`GroupBy(OrganizationId)`);
- тревога `AgentSilent` уровня `Critical` — если у клуба есть устройства, но онлайн ноль; `Detail` — сколько минут назад был последний heartbeat;
- тревога `ShiftNotClosed` уровня `Attention` — открытая смена старше `ShiftStaleHours`;
- тревога `PaymentOverdue` уровня `Attention` — у организации есть просроченный счёт; вешается на организацию, не на клуб;
- `AlertLevel` организации — максимум уровней её собственных тревог и тревог её клубов (`Critical` > `Attention` > `Normal`);
- клуб без устройств вообще не даёт `AgentSilent` (только что заведённый клуб не должен «гореть»).

- [ ] **Step 7: Добавить эндпоинт**

Создать `src/AFK4.Platform.Api/Endpoints/PlatformPulseEndpoints.cs` — `GET /api/platform/pulse` строго по образцу авторизации из `PlatformOrganizationEndpoints.cs`: `RequirePermission(PlatformAdminPermissionNames.ViewOrganizations)`, `Results.Unauthorized()` при неаутентифицированном, `403` при отсутствии права. Чтение аудита не пишет.

- [ ] **Step 8: Зарегистрировать в Program.cs**

Рядом с регистрацией прочих платформенных сервисов:

```csharp
builder.Services.Configure<PlatformPulseOptions>(
    builder.Configuration.GetSection(PlatformPulseOptions.SectionName));
builder.Services.AddScoped<IPlatformPulseService, EfPlatformPulseService>();
```

и рядом с прочими `Map…Endpoints(app)`:

```csharp
PlatformPulseEndpoints.Map(app);
```

Точные имена методов регистрации подсмотреть у соседних эндпоинтов и повторить их стиль.

- [ ] **Step 9: Прогнать тесты**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~PlatformPulse`
Run: `dotnet test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --filter FullyQualifiedName~Pulse`
Expected: PASS.

- [ ] **Step 10: Коммит**

```bash
git add src/AFK4.Shared.Contracts/Platform/Pulse src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests tests/AFK4.Shared.Contracts.Tests
git commit -m "feat(platform-control): serve fleet pulse in a single query"
```

---

### Task 3: Главный экран «Клубы»

**Files:**
- Create: `src/AFK4.PlatformControl.Web/src/api/platformClients/pulse.ts`
- Create: `src/AFK4.PlatformControl.Web/src/platform/clubs/pulseModel.ts`
- Create: `src/AFK4.PlatformControl.Web/src/platform/clubs/pulseModel.test.ts`
- Create: `src/AFK4.PlatformControl.Web/src/platform/clubs/usePulse.ts`
- Create: `src/AFK4.PlatformControl.Web/src/platform/clubs/ClubsScreen.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/clubs/ClubsScreen.test.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/clubs/OrganizationPulseRow.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/api/platformApi.ts` (подключить `PulseApi`)
- Modify: `src/AFK4.PlatformControl.Web/src/api/types.ts` (типы пульса)

**Interfaces:**
- Consumes: `GET /api/platform/pulse` из Task 2; `PlatformTransport` (метод `send<T>(method, path, body?)`); токены из Task 1.
- Produces: `pulseModel.ts` экспортирует `type PulseView = 'now' | 'all' | 'debt'`, `type PulseDensity = 'roomy' | 'dense'`, `selectView(organizations, view)`, `resolveDensity(count)`, `alertRank(level)`; `ClubsScreen` монтируется по пути `/admin`.

- [ ] **Step 1: Написать падающие тесты модели**

Создать `src/platform/clubs/pulseModel.test.ts`:

```ts
import { describe, expect, it } from 'bun:test';
import { alertRank, resolveDensity, selectView } from './pulseModel';
import type { PulseOrganization } from '@/api/types';

const org = (over: Partial<PulseOrganization>): PulseOrganization => ({
  organizationId: 'o1',
  name: 'Cyber Zone',
  status: 'active',
  planCode: 'pro',
  subscriptionStatus: 'active',
  alertLevel: 'normal',
  outstandingMinorUnits: 0,
  currencyCode: 'TJS',
  alerts: [],
  clubs: [],
  ...over
});

describe('pulseModel', () => {
  it('ranks critical above attention above normal', () => {
    expect(alertRank('critical')).toBeGreaterThan(alertRank('attention'));
    expect(alertRank('attention')).toBeGreaterThan(alertRank('normal'));
  });

  it('puts the loudest alert first in the "now" view', () => {
    const list = [
      org({ organizationId: 'quiet', alertLevel: 'normal', name: 'A' }),
      org({ organizationId: 'loud', alertLevel: 'critical', name: 'Z' })
    ];
    expect(selectView(list, 'now').map(item => item.organizationId)).toEqual(['loud', 'quiet']);
  });

  it('sorts the "all" view alphabetically regardless of alerts', () => {
    const list = [
      org({ organizationId: 'z', name: 'Ярд', alertLevel: 'critical' }),
      org({ organizationId: 'a', name: 'Арена' })
    ];
    expect(selectView(list, 'all').map(item => item.organizationId)).toEqual(['a', 'z']);
  });

  it('keeps only debtors in the "debt" view', () => {
    const list = [
      org({ organizationId: 'paid' }),
      org({ organizationId: 'owing', outstandingMinorUnits: 140000 })
    ];
    expect(selectView(list, 'debt').map(item => item.organizationId)).toEqual(['owing']);
  });

  it('switches to dense rows once there are more than five clients', () => {
    expect(resolveDensity(5)).toBe('roomy');
    expect(resolveDensity(6)).toBe('dense');
  });
});
```

- [ ] **Step 2: Запустить и убедиться, что падает**

Run: `cd src/AFK4.PlatformControl.Web && "$BUN" test src/platform/clubs/pulseModel.test.ts`
Expected: FAIL — модуль `./pulseModel` не существует.

- [ ] **Step 3: Добавить типы пульса**

В `src/api/types.ts` (рядом с существующими типами платформы):

```ts
export type PulseAlertLevel = 'normal' | 'attention' | 'critical';

export interface PulseAlert {
  kind: string;
  level: PulseAlertLevel;
  detail: string | null;
}

export interface PulseClub {
  branchId: string;
  name: string;
  city: string;
  devicesOnline: number;
  devicesTotal: number;
  seatsOccupied: number;
  seatsTotal: number;
  shiftOpen: boolean;
  shiftOpenedAtUtc: string | null;
  lastHeartbeatAtUtc: string | null;
  alerts: PulseAlert[];
}

export interface PulseOrganization {
  organizationId: string;
  name: string;
  status: string;
  planCode: string;
  subscriptionStatus: string;
  alertLevel: PulseAlertLevel;
  outstandingMinorUnits: number;
  currencyCode: string;
  alerts: PulseAlert[];
  clubs: PulseClub[];
}

export interface PlatformPulse {
  generatedAtUtc: string;
  organizations: PulseOrganization[];
}
```

- [ ] **Step 4: Реализовать модель**

Создать `src/platform/clubs/pulseModel.ts`:

```ts
import type { PulseAlertLevel, PulseOrganization } from '@/api/types';

export type PulseView = 'now' | 'all' | 'debt';
export type PulseDensity = 'roomy' | 'dense';

const RANK: Record<PulseAlertLevel, number> = { normal: 0, attention: 1, critical: 2 };

export function alertRank(level: PulseAlertLevel): number {
  return RANK[level];
}

export function resolveDensity(clientCount: number): PulseDensity {
  return clientCount > 5 ? 'dense' : 'roomy';
}

export function selectView(
  organizations: readonly PulseOrganization[],
  view: PulseView
): PulseOrganization[] {
  const byName = (left: PulseOrganization, right: PulseOrganization) =>
    left.name.localeCompare(right.name, 'ru');

  if (view === 'all') {
    return [...organizations].sort(byName);
  }

  if (view === 'debt') {
    return organizations
      .filter(item => item.outstandingMinorUnits > 0)
      .sort((left, right) => right.outstandingMinorUnits - left.outstandingMinorUnits);
  }

  return [...organizations].sort((left, right) => {
    const delta = alertRank(right.alertLevel) - alertRank(left.alertLevel);
    return delta !== 0 ? delta : byName(left, right);
  });
}
```

- [ ] **Step 5: Прогнать тесты модели**

Run: `cd src/AFK4.PlatformControl.Web && "$BUN" test src/platform/clubs/pulseModel.test.ts`
Expected: PASS.

- [ ] **Step 6: Добавить клиент API и хук**

Создать `src/api/platformClients/pulse.ts` строго по стилю `organizations.ts`:

```ts
import type { PlatformTransport } from '../platformTransport';
import type { PlatformPulse } from '../types';

export class PulseApi {
  public constructor(private readonly transport: PlatformTransport) {}

  public getPulse(): Promise<PlatformPulse> {
    return this.transport.send<PlatformPulse>('GET', '/api/platform/pulse');
  }
}
```

Подключить `PulseApi` в `platformApi.ts` тем же способом, каким там подключены остальные суб-клиенты. Создать `usePulse.ts` по образцу существующего `platform/overview/useOrganizationMetrics.ts` — те же состояния загрузки, ошибки и повторного запроса.

- [ ] **Step 7: Написать падающий тест экрана**

Создать `src/platform/clubs/ClubsScreen.test.tsx`. Проверить три вещи: аварийная сеть выводится первой; переключение вида отражается в URL; при отказе пульса экран показывает ошибку, а не пустоту. Моки — как в соседних тестах экранов (`OrganizationsScreen.test.tsx`), с типизированными `mock.module`, иначе `tsc -b` покраснеет.

- [ ] **Step 8: Реализовать экран и строку**

`ClubsScreen.tsx`: заголовок раздела, переключатель видов (`Сейчас` / `Все` / `Долги`) с записью в URL-параметр `view`, список `OrganizationPulseRow`, состояния загрузки/пусто/ошибка через существующие примитивы `components/ui/states.tsx`.

`OrganizationPulseRow.tsx`: строка сети с левой кромкой состояния (`normal` / `attention` / `critical` → токены `--accent` / `--warning` / `--danger`), именем, планом, агрегатом занятости по клубам, чипами тревог. Раскрытие по клику показывает клубы сети. Агрегат обязан подписывать конкретику: при тревоге выводится текст вида «2 из 3 клубов молчат», а не только общее число. В режиме `roomy` клубы раскрыты сразу.

- [ ] **Step 9: Прогнать тесты панели**

Run: `cd src/AFK4.PlatformControl.Web && "$BUN" test`
Expected: PASS.

- [ ] **Step 10: Коммит**

```bash
git add src/AFK4.PlatformControl.Web/src locales packages/i18n
git commit -m "feat(platform-control): rebuild the main screen around fleet pulse"
```

---

## Фаза 2 — операционные рычаги

### Task 4: Правка профиля клиента

**Files:**
- Create: `src/AFK4.Shared.Contracts/Platform/Organizations/UpdateOrganizationProfileRequest.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Tenancy/IPlatformOrganizationService.cs`
- Modify: `src/AFK4.Platform.Api/Platform/Tenancy/EfPlatformOrganizationService.cs`
- Modify: `src/AFK4.Platform.Api/Endpoints/PlatformOrganizationEndpoints.cs`
- Modify: `src/AFK4.Shared.Contracts/Platform/Auth/PlatformAdminPermissionNames.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformOrganizationProfileEndpointTests.cs`

**Interfaces:**
- Consumes: `PlatformOrganizationOperationResult<OrganizationDetailDto>`, `WritePlatformAuditAsync`, `AuditActionNames`.
- Produces: `PATCH /api/platform/organizations/{organizationId}` → `OrganizationDetailDto`; право `PlatformAdminPermissionNames.UpdateOrganizationProfile = "platform.organizations.profile.update"`; метод `IPlatformOrganizationService.UpdateProfileAsync(Guid organizationId, UpdateOrganizationProfileRequest request, Guid actorPlatformAdminUserId, CancellationToken cancellationToken)`.

- [ ] **Step 1: Написать падающие тесты**

Создать `PlatformOrganizationProfileEndpointTests.cs` с тремя случаями: успешное переименование возвращает `200` и новое имя в `OrganizationDetailDto`; пустое имя даёт `400`; отсутствие права даёт `403`. Образец структуры — `PlatformOrganizationLifecycleEndpointTests.cs`.

- [ ] **Step 2: Запустить и убедиться, что падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~PlatformOrganizationProfileEndpointTests`
Expected: FAIL — не компилируется.

- [ ] **Step 3: Описать контракт запроса**

```csharp
namespace AFK4.Shared.Contracts.Platform.Organizations;

public sealed record UpdateOrganizationProfileRequest(
    string Name,
    string? ContactEmail,
    string? ContactPhone,
    string? LegalDetails);
```

Проверить `OrganizationDetailDto`: если полей контактов там нет, добавить их одновременно с этим шагом и обновить существующий контракт-тест сериализации организации, иначе UI не сможет их показать.

- [ ] **Step 4: Добавить право**

В `PlatformAdminPermissionNames.cs`:

```csharp
public const string UpdateOrganizationProfile = "platform.organizations.profile.update";
```

Внести право в каталог ролей платформы там же, где перечислены остальные `platform.organizations.*` (искать по `UpdateOrganizationLimits` и повторить все места, включая тест каталога `PlatformAdminPermissionCatalogTests`).

- [ ] **Step 5: Реализовать сервис и эндпоинт**

`UpdateProfileAsync` валидирует непустое имя (обрезка пробелов), пишет поля в `OrganizationEntity`, обновляет `UpdatedAtUtc`, возвращает свежий `OrganizationDetailDto`. Эндпоинт `MapPatch("/api/platform/organizations/{organizationId:guid}")` — копия структуры обработчика лимитов с правом `UpdateOrganizationProfile` и действием аудита `AuditActionNames.UpdateOrganizationProfile` (константу добавить рядом с существующими).

- [ ] **Step 6: Прогнать тесты**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~PlatformOrganization`
Expected: PASS.

- [ ] **Step 7: Коммит**

```bash
git add src tests
git commit -m "feat(platform-control): allow editing the client profile"
```

---

### Task 5: Подписка целиком, триал и отсрочка платежа

**Files:**
- Modify: `src/AFK4.Shared.Contracts/Platform/Billing/UpdateSubscriptionRequest.cs`
- Modify: `src/AFK4.Shared.Contracts/Platform/Billing/OrganizationSubscriptionDto.cs`
- Modify: `src/AFK4.Platform.Api/Data/OrganizationSubscriptionEntity.cs`
- Create: `src/AFK4.Platform.Api/Data/Migrations/<timestamp>_AddSubscriptionPaymentGrace.cs` (через `dotnet ef`)
- Modify: `src/AFK4.Platform.Api/Endpoints/PlatformBillingEndpoints.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformSubscriptionEditingTests.cs`

**Interfaces:**
- Consumes: существующий обработчик `PATCH /api/platform/organizations/{organizationId}/subscription`, право `PlatformAdminPermissionNames.ManageSubscriptions`.
- Produces: расширенный `UpdateSubscriptionRequest(string? PlanCode, string? BillingInterval, string? Status, bool? CancelAtPeriodEnd, long? AmountMinorUnits, DateTimeOffset? CurrentPeriodEndUtc, DateTimeOffset? PaymentGraceUntilUtc)`; поле `OrganizationSubscriptionEntity.PaymentGraceUntilUtc`; поле `OrganizationSubscriptionDto.PaymentGraceUntilUtc`.

- [ ] **Step 1: Написать падающие тесты**

`PlatformSubscriptionEditingTests.cs`, случаи: индивидуальная цена сохраняется и возвращается; перевод в `trial` с датой окончания периода; установка и снятие отсрочки; отрицательная сумма отклоняется с `400`; отсрочка в прошлом отклоняется с `400`.

- [ ] **Step 2: Запустить и убедиться, что падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~PlatformSubscriptionEditingTests`
Expected: FAIL.

- [ ] **Step 3: Расширить сущность и контракты**

В `OrganizationSubscriptionEntity` добавить `public DateTimeOffset? PaymentGraceUntilUtc { get; set; }`. В `OrganizationSubscriptionDto` добавить последним параметром `DateTimeOffset? PaymentGraceUntilUtc`. В `UpdateSubscriptionRequest` добавить три поля из блока Interfaces.

- [ ] **Step 4: Создать миграцию**

```bash
dotnet build src/AFK4.Platform.Api
dotnet ef migrations add AddSubscriptionPaymentGrace \
  --project src/AFK4.Platform.Api \
  --output-dir Data/Migrations \
  --no-build
```

Открыть созданный файл и убедиться, что `Up`/`Down` не пустые — пустая миграция означает устаревшую сборку, тогда пересобрать и повторить.

- [ ] **Step 5: Реализовать обработку**

В обработчике подписки: применять только переданные (не `null`) поля; валидировать `AmountMinorUnits >= 0`, `CurrentPeriodEndUtc > CurrentPeriodStartUtc`, `PaymentGraceUntilUtc > DateTimeOffset.UtcNow`; при `Status == SubscriptionStatusNames.Trial` требовать заданную дату окончания периода. Аудит пишет все изменённые поля.

- [ ] **Step 6: Прогнать тесты**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~Subscription`
Expected: PASS.

- [ ] **Step 7: Коммит**

```bash
git add src tests
git commit -m "feat(platform-control): make subscription terms editable"
```

---

### Task 6: Канал обновлений клиента и смена владельца

**Files:**
- Modify: `src/AFK4.Platform.Api/Data/OrganizationEntity.cs`
- Create: `src/AFK4.Platform.Api/Data/Migrations/<timestamp>_AddOrganizationUpdateChannel.cs`
- Create: `src/AFK4.Shared.Contracts/Platform/Organizations/UpdateOrganizationUpdateChannelRequest.cs`
- Create: `src/AFK4.Shared.Contracts/Platform/Organizations/TransferOrganizationOwnerRequest.cs`
- Modify: `src/AFK4.Platform.Api/Endpoints/PlatformOrganizationEndpoints.cs`
- Modify: `src/AFK4.Shared.Contracts/Platform/Auth/PlatformAdminPermissionNames.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformOrganizationUpdateChannelTests.cs`
- Test: `tests/AFK4.Platform.Api.Tests/Platform/PlatformOrganizationOwnerTransferTests.cs`

**Interfaces:**
- Consumes: `PulseOrganizationDto` (Task 2) не меняется; существующий механизм приглашений владельца.
- Produces: `PATCH /api/platform/organizations/{organizationId}/update-channel`; `POST /api/platform/organizations/{organizationId}/owner-transfer`; поля `OrganizationEntity.UpdateChannel`, `OrganizationEntity.PinnedClientVersion`; право `PlatformAdminPermissionNames.TransferOrganizationOwner = "platform.organizations.owner.transfer"`.

- [ ] **Step 1: Написать падающие тесты**

Канал: смена на `beta` сохраняется и возвращается; неизвестный канал даёт `400`; закрепление версии сохраняется и снимается передачей `null`.
Владелец: передача создаёт приглашение новому владельцу и отзывает доступ прежнего; передача самому себе даёт `400`; без права — `403`.

- [ ] **Step 2: Запустить и убедиться, что падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter "FullyQualifiedName~UpdateChannelTests|FullyQualifiedName~OwnerTransferTests"`
Expected: FAIL.

- [ ] **Step 3: Расширить сущность и создать миграцию**

```csharp
public string UpdateChannel { get; set; } = "stable";

public string? PinnedClientVersion { get; set; }
```

```bash
dotnet build src/AFK4.Platform.Api
dotnet ef migrations add AddOrganizationUpdateChannel \
  --project src/AFK4.Platform.Api \
  --output-dir Data/Migrations \
  --no-build
```

Допустимые каналы брать из существующего перечня каналов пакетов обновлений, а не заводить второй список.

- [ ] **Step 4: Описать контракты запросов**

```csharp
public sealed record UpdateOrganizationUpdateChannelRequest(
    string Channel,
    string? PinnedClientVersion);

public sealed record TransferOrganizationOwnerRequest(
    string NewOwnerEmail,
    string Reason);
```

- [ ] **Step 5: Реализовать эндпоинты**

Оба — по образцу обработчика лимитов, с аудитом. Передача владельца выполняется одной транзакцией: выпуск приглашения новому владельцу и отзыв прежнего должны быть атомарны, иначе клиент останется без владельца вовсе.

- [ ] **Step 6: Прогнать тесты**

Run: `dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --filter FullyQualifiedName~PlatformOrganization`
Expected: PASS.

- [ ] **Step 7: Коммит**

```bash
git add src tests
git commit -m "feat(platform-control): add per-client update channel and owner transfer"
```

---

## Фаза 3 — карточка клиента и завершение

### Task 7: Карточка клиента «паспорт + вкладки»

**Files:**
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/ClientPassport.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/ClientPassport.test.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationProfileDialog.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/SubscriptionDialog.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/PaymentGraceDialog.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/OwnerTransferDialog.tsx`
- Create: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationClubsTab.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationPage.tsx`
- Modify: `src/AFK4.PlatformControl.Web/src/api/platformClients/organizations.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/api/platformClients/subscriptions.ts`

**Interfaces:**
- Consumes: эндпоинты из задач 4-6; `PlatformPulse` из Task 3 (вкладка «Клубы» переиспользует данные пульса по этой организации); существующие секции `OrganizationStatusSection`, `OrganizationLimitsSection`, `OrganizationInvoicesSection`, `OrganizationSupportNotesSection`, `OrganizationOwnerInvitesSection`.
- Produces: `OrganizationPage` в раскладке «паспорт слева + вкладки справа»; методы клиента `updateProfile`, `updateSubscription`, `updateUpdateChannel`, `transferOwner`.

- [ ] **Step 1: Написать падающий тест паспорта**

`ClientPassport.test.tsx`: паспорт показывает имя, тариф, цену в мажорных единицах через `formatCurrency`, дату следующего счёта, владельца и канал обновлений; при просрочке показывает чип долга; кнопки рычагов скрыты, если у сессии нет соответствующего права (`billing.manage`, `organizations.manage`).

- [ ] **Step 2: Запустить и убедиться, что падает**

Run: `cd src/AFK4.PlatformControl.Web && "$BUN" test src/platform/organizations/ClientPassport.test.tsx`
Expected: FAIL — модуль не существует.

- [ ] **Step 3: Добавить методы клиента API**

В `organizations.ts`:

```ts
public updateProfile(
  organizationId: string,
  request: { name: string; contactEmail: string | null; contactPhone: string | null; legalDetails: string | null }
): Promise<OrganizationDetail> {
  return this.transport.send<OrganizationDetail>(
    'PATCH',
    `/api/platform/organizations/${organizationId}`,
    request
  );
}

public updateUpdateChannel(
  organizationId: string,
  request: { channel: string; pinnedClientVersion: string | null }
): Promise<OrganizationDetail> {
  return this.transport.send<OrganizationDetail>(
    'PATCH',
    `/api/platform/organizations/${organizationId}/update-channel`,
    request
  );
}

public transferOwner(
  organizationId: string,
  request: { newOwnerEmail: string; reason: string }
): Promise<OrganizationDetail> {
  return this.transport.send<OrganizationDetail>(
    'POST',
    `/api/platform/organizations/${organizationId}/owner-transfer`,
    request
  );
}
```

В `subscriptions.ts` расширить существующий метод обновления подписки новыми полями запроса из Task 5.

- [ ] **Step 4: Реализовать паспорт**

`ClientPassport.tsx` — левая колонка: имя, число клубов и города, чипы статуса и долга, строки «Тариф», «Цена», «Следующий счёт», «Владелец», «Канал обновлений», и кнопки «Изменить подписку», «Выставить счёт», «Отсрочка», «Править профиль», «Приостановить». Каждая кнопка скрывается при отсутствии права через `can(session, …)`.

- [ ] **Step 5: Реализовать диалоги рычагов**

Четыре диалога на базе существующего `components/ui/dialog.tsx` и `ConfirmDialog`. Важное правило проекта: тело диалога рендерится без промежуточной обёртки, иначе компонент ремаунтится на каждый ввод и теряет фокус. Успешное действие закрывает диалог и показывает тост через существующий `ToastProvider`; не ставить `setFeedback` и `onClose` в один батч — уведомление не успеет отрисоваться.

- [ ] **Step 6: Перестроить страницу организации**

`OrganizationPage.tsx` — сетка `280px + 1fr`: слева `ClientPassport`, справа вкладки «Клубы», «Счета», «Лимиты», «Обновления», «Доступ», «Журнал». Каждая вкладка имеет собственную границу отказа: падение одной не гасит остальные и не гасит паспорт.

- [ ] **Step 7: Прогнать тесты панели**

Run: `cd src/AFK4.PlatformControl.Web && "$BUN" test`
Expected: PASS.

- [ ] **Step 8: Коммит**

```bash
git add src/AFK4.PlatformControl.Web/src locales packages/i18n
git commit -m "feat(platform-control): rebuild the client card around a passport"
```

---

### Task 8: Навигация, копия и снос старого

**Files:**
- Modify: `src/AFK4.PlatformControl.Web/src/platform/nav.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/platform/nav.test.ts`
- Modify: `src/AFK4.PlatformControl.Web/src/App.tsx`
- Delete: `src/AFK4.PlatformControl.Web/src/platform/overview/OverviewScreen.tsx`, `OverviewScreen.test.tsx`, `AttentionQueue.tsx`
- Delete: `src/AFK4.PlatformControl.Web/src/platform/organizations/OrganizationsScreen.tsx`, `OrganizationsScreen.test.tsx`, `OrganizationsTable.tsx`
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`

**Interfaces:**
- Consumes: `ClubsScreen` из Task 3, `OrganizationPage` из Task 7.
- Produces: навигация из пяти разделов; ключи `nav.platform.clubs`, `nav.platform.money`, `nav.platform.journal`, `platform.clubs.view.now|all|debt`.

- [ ] **Step 1: Написать падающий тест навигации**

В `nav.test.ts` заменить ожидания на новый состав: `clubs`, `money`, `updates`, `journal`, `settings` в группе контроля и `profile` в группе аккаунта; проверить, что раздела `overview` и раздела `organizations` больше нет.

- [ ] **Step 2: Запустить и убедиться, что падает**

Run: `cd src/AFK4.PlatformControl.Web && "$BUN" test src/platform/nav.test.ts`
Expected: FAIL — навигация всё ещё содержит `overview` и `organizations`.

- [ ] **Step 3: Обновить навигацию и маршруты**

`nav.ts`: `clubs` → `/admin` (право `organizations.read`), `money` → `/admin/money` (`billing.read`), `updates` → `/admin/updates`, `journal` → `/admin/journal` (`audit.read`), `settings` → `/admin/settings`. В `App.tsx` смонтировать `ClubsScreen` на `/admin`, сохранить `/admin/organizations/:organizationId` как путь карточки клиента.

- [ ] **Step 4: Добавить копию на три языка**

Новые ключи внести в `locales/ru.json`, `locales/en.json`, `locales/tg.json`, затем:

```bash
cd packages/i18n && "$BUN" run gen
```

Таджикский писать настоящим таджикским; копирование русского значения завалит guard-тест.

- [ ] **Step 5: Удалить отслужившие экраны**

Удалить файлы из блока Files. Убедиться, что не осталось ни одного импорта на них и ни одного осиротевшего ключа `platform.overview.*` в локалях.

- [ ] **Step 6: Прогнать тесты и локализацию**

Run: `cd src/AFK4.PlatformControl.Web && "$BUN" test`
Run: `dotnet test tests/AFK4.Localization.Tests/AFK4.Localization.Tests.csproj`
Expected: PASS.

- [ ] **Step 7: Коммит**

```bash
git add src locales packages/i18n
git commit -m "refactor(platform-control): retire the superseded registry screens"
```

---

### Task 9: Финальный гейт

**Files:**
- Modify: `docs/superpowers/specs/README.md` (перенос спеки в раздел реализованных при необходимости)
- Modify: `docs/superpowers/plans/README.md`

- [ ] **Step 1: Прогнать фронтовые проверки**

```bash
cd src/AFK4.PlatformControl.Web && "$BUN" test && "$BUN" run build
```
Expected: тесты зелёные, сборка проходит. Сборка тайпчекает и тест-файлы — падение здесь при зелёных тестах означает нетипизированные моки.

- [ ] **Step 2: Прогнать бэкенд**

```bash
dotnet test tests/AFK4.Platform.Api.Tests/AFK4.Platform.Api.Tests.csproj --nologo
dotnet test tests/AFK4.Shared.Contracts.Tests/AFK4.Shared.Contracts.Tests.csproj --nologo
dotnet test tests/AFK4.Localization.Tests/AFK4.Localization.Tests.csproj --nologo
```
Expected: 0 failed.

- [ ] **Step 3: Проверить живьём**

Поднять локальный стек и пройти сценарии: главный экран с тремя видами, раскрытие сети, карточка клиента, каждый из рычагов.

```bash
export ConnectionStrings__PlatformDatabase="Host=127.0.0.1;Port=5432;Database=afk4_dev;Username=postgres;Password=postgres"
dotnet run --project src/AFK4.Platform.DevSeed
ASPNETCORE_ENVIRONMENT=Development ASPNETCORE_URLS=http://127.0.0.1:5000 dotnet run --project src/AFK4.Platform.Api --no-launch-profile
cd src/AFK4.PlatformControl.Web && VITE_PLATFORM_API_BASE_URL=http://127.0.0.1:5000 "$BUN" run dev
```

Миграции из задач 5 и 6 применяются DevSeed автоматически. Отдать пользователю ссылку `http://127.0.0.1:5175/` для визуальной приёмки — она и есть гейт слияния.

- [ ] **Step 4: Обновить документы и закоммитить**

```bash
git add docs
git commit -m "docs: record the Platform Control UI redesign"
```

---

## Self-review

**Покрытие спеки:** навигация — Task 8; главный экран и сигнальные строки — Task 3; адаптивная плотность — Task 3 (`resolveDensity`); сеть с раскрытием и честный агрегат — Task 3 (Step 8); карточка «паспорт + вкладки» — Task 7; правка профиля — Task 4; подписка, триал, лимиты — Task 5 и Task 7; отсрочка — Task 5; канал обновлений и смена владельца — Task 6; эндпоинт пульса и правила тревог — Task 2; токены — Task 1; состояния экранов — Task 3 (Step 8) и Task 7 (Step 6); тестирование — в каждой задаче плюс Task 9.

**Осознанные допущения:** точные имена переменных `@afk4/tokens` и сигнатуры тестовых помощников (`PlatformApiFactory.SeedAsync`, `PlatformAdminTestHelper.SignInAsync`) сверяются с кодом на месте — в плане указано откуда их брать, потому что выдуманное имя дороже сверки.
