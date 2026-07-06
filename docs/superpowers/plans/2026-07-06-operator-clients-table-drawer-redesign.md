# Оператор «Клиенты» — таблица + drawer — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Переписать раздел «Клиенты» оператор-аппа с раскладки «список + карточка-воркспейс» на «широкая таблица клиентов + узкий drawer выбранного клиента», убрать продажу пакетов (уходит в Кассу), обогатить строки данными (Новый-тег, Визит, Сейчас/пакет).

**Architecture:** Бэкенд расширяет read-проекцию operator-search тремя полями (`createdAtUtc`, `lastActivityAtUtc`, активный пакет), избегая N+1 через batch-запросы. Фронт: новые компоненты `ClientsTable` (замена `ClientList`) и `ClientDrawer` (замена `ClientDetail`), оркестратор `BackendPlayersWorkspace` меняет только layout + строит live-context для всех строк из одного `sessions.timeline`. Продажа пакетов удаляется целиком.

**Tech Stack:** .NET (AFK4.Platform.Api, EF Core, xUnit, InMemory-провайдер в тестах) · React 18 + TypeScript · `bun test` (happy-dom + @testing-library/react + jest-dom) · сборка `tsc -b && vite build` · i18n `@afk4/i18n` (locales/{ru,en,tg}.json → gen).

## Global Constraints

- **Продажа пакетов — ТОЛЬКО в Кассе.** Из Клиентов inline-покупка удаляется полностью. В таблице пакет лишь показывается.
- **Долг — раздельный счёт, показывать УСЛОВНО** (только когда ≠ 0). Не сливать со счётом, не переименовывать. Wallet и debt — независимые ledger accountType.
- **НЕ вводить VIP / «Потрачено»** в этом плане (VIP → отдельный будущий эпик; временный `isVip` не городить).
- **money-path IsActive-guard не трогать.** Изменения бэкенда — только read-проекция (`AsNoTracking`), не money-write, не команды. Новые batch-запросы по `LedgerEntries` для остатка пакета обязаны фильтровать `AccountType IN (package_time, bonus_time)` — не суммировать все accountType.
- **N+1 запрещён в списке.** Новые агрегаты (lastActivity, остаток пакета) — batch-запросом по `playerIds` через `GroupBy`, по образцу существующих balance/packageCount запросов. НЕ переиспользовать per-player `PlayerHistoryProjector.GetVisitsAsync` / `LedgerBalanceProjector.GetPackageRemainingSecondsAsync` в цикле.
- **EF Core InMemory** (тестовый провайдер) не транслирует часть LINQ (напр. `Guid.CompareTo` в `Where`) — новые GroupBy/Max прогонять через бэк-тесты сразу.
- **`PlayerSearchResultDto` — позиционный record.** Добавление полей ломает компиляцию всех мест конструирования — чинить каждое: `EfOperatorReferenceDataService`, WPF `PlayerSearchViewModel`, WPF-тесты (`PlayerSearchViewModelTests`, `OperatorPlayerApiClientTests`), контракт-тест.
- **i18n:** новые ключи `op.players.*` добавлять в ВСЕ три `locales/{ru,en,tg}.json`, затем `cd packages/i18n && bun run gen`. Guard'ы: `messages.test.ts` (parity ru/en/tg + tg≠ru, реальный таджикский не копия ru), `i18nKeysExist.test.ts` (каждый `t('key')` заведён).
- **CSS-каскад:** все интерактивные элементы несут `.ui-btn`/`.ui-chip`/свой класс — не полагаться на голый `<button>` (глобальный `button{cursor:default}`). Не вводить tag-селекторы `button`/`.X button` в новых контейнерах (бьют `.ui-btn` по специфичности).
- **Визуальный источник:** утверждённый макет `scratchpad/clients-redesign-mock-v7.html` (структура таблицы/drawer + готовый CSS на реальных токенах). Разметку и стили брать оттуда.
- **bun = полный путь** `/home/fedya/.bun/bin/bun`, фронт-команды из `src/AFK4.Operator.App.Web`. Финал каждой фронт-задачи: `bun test <файл>`; финал всего плана обязательно `bun run build` (тайпчекает и тест-файлы).
- **Субагенты НЕ используют git worktree** (десинк HEAD основного репо). Никаких AI-подписей в коммитах.

---

## File Structure

**Бэкенд (создать/изменить):**
- `src/AFK4.Shared.Contracts/Operator/PlayerSearchResultDto.cs` — +4 поля.
- `src/AFK4.Platform.Api/Billing/EfOperatorReferenceDataService.cs` — batch-запросы + проекция.
- `src/AFK4.Operator.App/Players/PlayerSearchViewModel.cs` (WPF) — заполнить новые поля в конструкторе (передать/дефолт).
- Тесты: `tests/AFK4.Platform.Api.Tests/OperatorReferenceDataEndpointTests.cs`, `tests/AFK4.Shared.Contracts.Tests/OperatorReferenceContractSerializationTests.cs`, WPF `tests/AFK4.Operator.App.Tests/PlayerSearchViewModelTests.cs` + `OperatorPlayerApiClientTests.cs`.

**Фронт (создать):**
- `src/AFK4.Operator.App.Web/src/players/ClientsTable.tsx` (+ `.test.tsx`) — замена `ClientList`.
- `src/AFK4.Operator.App.Web/src/players/ClientDrawer.tsx` (+ `.test.tsx`) — замена `ClientDetail` в drawer-форме.

**Фронт (изменить):**
- `src/api/clients/players.ts` — зеркало DTO.
- `src/operatorHelpers.ts` — `PlayerClientItem` + `projectPlayerClient`.
- `src/players/playersModel.ts` (+ `.test.ts`) — новые проекции (тег Новый, давность визита, банк пакета, `buildClientContextMap`).
- `src/BackendPlayersWorkspace.tsx` — layout table+drawer, live-context map, удаление `buyPackage`.
- `src/players/WalletZone.tsx` — пресеты пополнения (переезжает внутрь drawer).
- `src/players/PackagesSection.tsx` — удалить inline-покупку.
- `src/players/HistorySection.tsx` — режим мини-списка (последние N).
- `src/devMockBackend.ts` — фикстуры с новыми полями (dev-preview).
- `src/App.test.tsx` — переписать players-сценарии, удалить purchase-кейсы.
- `src/styles/12-players.css` — layout таблицы + drawer, удалить список/карточку/покупку.
- `locales/{ru,en,tg}.json` — новые ключи.

**Фронт (удалить):**
- `src/players/ClientList.tsx` + `.test.tsx`.
- `src/players/ClientDetail.tsx` + `.test.tsx` (содержимое переезжает в `ClientDrawer`).

---

### Task 1: Backend — расширить `PlayerSearchResultDto` + `createdAtUtc`

**Files:**
- Modify: `src/AFK4.Shared.Contracts/Operator/PlayerSearchResultDto.cs`
- Modify: `src/AFK4.Platform.Api/Billing/EfOperatorReferenceDataService.cs` (проекция ~97-106)
- Modify: `src/AFK4.Operator.App/Players/PlayerSearchViewModel.cs` (место `new PlayerSearchResultDto(...)` ~295, если конструирует; иначе пропустить)
- Test: `tests/AFK4.Shared.Contracts.Tests/OperatorReferenceContractSerializationTests.cs`, `tests/AFK4.Platform.Api.Tests/OperatorReferenceDataEndpointTests.cs`
- Fix compile: `tests/AFK4.Operator.App.Tests/PlayerSearchViewModelTests.cs`, `tests/AFK4.Operator.App.Tests/OperatorPlayerApiClientTests.cs`

**Interfaces:**
- Produces: `PlayerSearchResultDto` с новыми полями (в конце record, чтобы именованные вызовы не рушились по порядку):
  ```csharp
  public sealed record PlayerSearchResultDto(
      Guid PlayerAccountId,
      string DisplayName,
      string? PhoneNumber,
      long WalletBalanceMinorUnits,
      long DebtBalanceMinorUnits,
      int ActivePackageCount,
      bool IsActive,
      DateTimeOffset CreatedAtUtc,
      DateTimeOffset? LastActivityAtUtc,
      string? ActivePackageName,
      long ActivePackageRemainingMinutes);
  ```
  В этой задаче реально заполняется только `CreatedAtUtc` (из `PlayerAccountEntity.CreatedAtUtc`); `LastActivityAtUtc`/`ActivePackageName`/`ActivePackageRemainingMinutes` — заглушки `null`/`null`/`0` (наполняются в Task 2 и Task 3).

- [ ] **Step 1: Обновить контракт-тест (сериализация)**

В `OperatorReferenceContractSerializationTests.cs` в тест `PlayerSearchResultDto_RoundTripsThroughJson` добавить новые поля в конструирование и ассерты:
```csharp
var dto = new PlayerSearchResultDto(
    PlayerAccountId: Guid.NewGuid(),
    DisplayName: "Alex",
    PhoneNumber: "+992900000000",
    WalletBalanceMinorUnits: 4600,
    DebtBalanceMinorUnits: 0,
    ActivePackageCount: 1,
    IsActive: true,
    CreatedAtUtc: new DateTimeOffset(2026, 1, 1, 0, 0, 0, TimeSpan.Zero),
    LastActivityAtUtc: new DateTimeOffset(2026, 6, 1, 0, 0, 0, TimeSpan.Zero),
    ActivePackageName: "Ночной 5ч",
    ActivePackageRemainingMinutes: 150);
// ... round-trip ...
Assert.Equal(dto.CreatedAtUtc, restored.CreatedAtUtc);
Assert.Equal(dto.LastActivityAtUtc, restored.LastActivityAtUtc);
Assert.Equal(dto.ActivePackageName, restored.ActivePackageName);
Assert.Equal(dto.ActivePackageRemainingMinutes, restored.ActivePackageRemainingMinutes);
```

- [ ] **Step 2: Запустить тест — убедиться, что не компилируется/падает**

Run: `dotnet test tests/AFK4.Shared.Contracts.Tests --filter PlayerSearchResultDto_RoundTripsThroughJson`
Expected: ошибка компиляции (в record нет новых полей).

- [ ] **Step 3: Добавить поля в record**

Внести сигнатуру из блока Interfaces в `PlayerSearchResultDto.cs`.

- [ ] **Step 4: Починить проекцию сервиса (createdAtUtc реально, остальное заглушки)**

В `EfOperatorReferenceDataService.SearchPlayersAsync` в финальной проекции (`new PlayerSearchResultDto(...)`, ~97-106) добавить именованные аргументы:
```csharp
CreatedAtUtc: player.CreatedAtUtc,
LastActivityAtUtc: null,           // Task 2
ActivePackageName: null,           // Task 3
ActivePackageRemainingMinutes: 0   // Task 3
```
(Первый EF-запрос уже выбирает `PlayerAccountEntity` целиком — `CreatedAtUtc` доступно без доп. запроса.)

- [ ] **Step 5: Починить все прочие места конструирования (компиляция)**

Найти: `grep -rn "new PlayerSearchResultDto(" src tests` — добавить недостающие именованные аргументы (`CreatedAtUtc: ...`, `LastActivityAtUtc: null`, `ActivePackageName: null`, `ActivePackageRemainingMinutes: 0`) в WPF `PlayerSearchViewModel.cs` и WPF-тестах. Для WPF-VM: если бизнес-значение неважно для WPF — передать разумные дефолты (`CreatedAtUtc: default`, остальные `null`/`0`).

- [ ] **Step 6: Обновить бэк-тест эндпоинта под новое поле**

В `OperatorReferenceDataEndpointTests.SearchPlayers_WithCashier_ReturnsMatchingActivePlayersFromBranch`: у сидируемого `PlayerAccountEntity` задать `CreatedAtUtc` явно и добавить ассерт `Assert.Equal(seededCreatedAt, dto.CreatedAtUtc)`.

- [ ] **Step 7: Прогнать тесты**

Run: `dotnet test tests/AFK4.Shared.Contracts.Tests` и `dotnet test tests/AFK4.Platform.Api.Tests --filter OperatorReferenceData`
Expected: PASS. Также убедиться, что решение компилируется целиком: `dotnet build`.

- [ ] **Step 8: Commit**

```bash
git add src/AFK4.Shared.Contracts src/AFK4.Platform.Api src/AFK4.Operator.App tests
git commit -m "feat(operator-players): расширить PlayerSearchResultDto (createdAtUtc + заделы под визит/пакет)"
```

---

### Task 2: Backend — `lastActivityAtUtc` (batch по сессиям)

**Files:**
- Modify: `src/AFK4.Platform.Api/Billing/EfOperatorReferenceDataService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/OperatorReferenceDataEndpointTests.cs`

**Interfaces:**
- Consumes: `PlayerSearchResultDto.LastActivityAtUtc` (поле из Task 1).
- Produces: заполненный `LastActivityAtUtc` = время последней сессии клиента (последняя по `EndedAtUtc`, а для незавершённых — `StartedAtUtc ?? RequestedAtUtc`); `null` если сессий не было. Семантика «визита» = последняя активность на месте.

- [ ] **Step 1: Написать падающий бэк-тест**

Добавить в `OperatorReferenceDataEndpointTests.cs` тест: сидировать игрока + 2 сессии (`SessionEntity` с `PlayerAccountId`, разными `StartedAtUtc`/`EndedAtUtc`) → `GET .../players` → ассерт `dto.LastActivityAtUtc` == время более поздней сессии; для игрока без сессий — `null`.
```csharp
[Fact]
public async Task SearchPlayers_ReturnsLastSessionActivity()
{
    // seed player + sessions (earlier + later), player without sessions
    // GET .../players
    Assert.Equal(laterSessionTime, withSessions.LastActivityAtUtc);
    Assert.Null(withoutSessions.LastActivityAtUtc);
}
```

- [ ] **Step 2: Запустить — убедиться, что падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter SearchPlayers_ReturnsLastSessionActivity`
Expected: FAIL (`LastActivityAtUtc` сейчас всегда null).

- [ ] **Step 3: Добавить batch-запрос последней активности**

В `SearchPlayersAsync` после существующих batch-агрегатов добавить (по образцу balance/packageCount groupby):
```csharp
var lastActivityLookup = await dbContext.Sessions
    .AsNoTracking()
    .Where(s => playerIds.Contains(s.PlayerAccountId!.Value))
    .GroupBy(s => s.PlayerAccountId!.Value)
    .Select(g => new
    {
        PlayerAccountId = g.Key,
        Last = g.Max(s => s.EndedAtUtc ?? s.StartedAtUtc ?? s.RequestedAtUtc)
    })
    .ToDictionaryAsync(x => x.PlayerAccountId, x => x.Last, cancellationToken);
```
(`PlayerAccountId` в `SessionEntity` nullable — фильтр `playerIds.Contains(..Value)` отсекает гостевые сессии без аккаунта. `playerIds` — уже собранный список из первого запроса.)

- [ ] **Step 4: Пробросить в проекцию**

Заменить заглушку в `new PlayerSearchResultDto(...)`:
```csharp
LastActivityAtUtc: lastActivityLookup.TryGetValue(player.PlayerAccountId, out var last) ? last : (DateTimeOffset?)null,
```

- [ ] **Step 5: Прогнать тест**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter SearchPlayers`
Expected: PASS. Если InMemory не транслирует `g.Max(... ?? ...)` — вынести coalesce в промежуточный `.Select` до `GroupBy` (выбрать `EffectiveAt` полем), затем `Max(EffectiveAt)`.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests
git commit -m "feat(operator-players): lastActivityAtUtc из последней сессии (batch, без N+1)"
```

---

### Task 3: Backend — активный пакет (имя + остаток минут, batch)

**Files:**
- Modify: `src/AFK4.Platform.Api/Billing/EfOperatorReferenceDataService.cs`
- Test: `tests/AFK4.Platform.Api.Tests/OperatorReferenceDataEndpointTests.cs`

**Interfaces:**
- Consumes: `PlayerSearchResultDto.ActivePackageName`, `.ActivePackageRemainingMinutes`.
- Produces: имя пакета с максимальным суммарным остатком (`package_time`+`bonus_time` секунды) и остаток в минутах (`секунды / 60`, целочисленно), среди пакетов игрока где `(ExpiresAtUtc == null || ExpiresAtUtc > now)` И `остаток > 0`. Если таких нет — `null`/`0`.

- [ ] **Step 1: Написать падающий бэк-тест**

Сидировать игрока + `PlayerPackageEntity` (не истёкший) + `LedgerEntryEntity` с `PlayerPackageId`, `AccountType=package_time`, `QuantitySeconds` (напр. 9000с = 150мин) → `GET .../players` → ассерт `dto.ActivePackageName == "..."` и `dto.ActivePackageRemainingMinutes == 150`. Для игрока с истёкшим/нулевым пакетом → `null`/`0`.

- [ ] **Step 2: Запустить — убедиться, что падает**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter SearchPlayers_ReturnsActivePackage`
Expected: FAIL (сейчас `null`/`0`).

- [ ] **Step 3: Batch остаток по пакетам**

Существующий запрос активных пакетов (для `ActivePackageCount`, ~79-95) уже выбирает `PlayerPackages` с фильтром по дате — расширить его, чтобы получить `(PlayerAccountId, PlayerPackageId, Name)` списком (не только count). Затем один batch-запрос остатков:
```csharp
var packageIds = activePackages.Select(p => p.PlayerPackageId).ToList();
var remainingLookup = await dbContext.LedgerEntries
    .AsNoTracking()
    .Where(e => e.PlayerPackageId != null
        && packageIds.Contains(e.PlayerPackageId.Value)
        && (e.AccountType == LedgerAccountTypeNames.PackageTime
            || e.AccountType == LedgerAccountTypeNames.BonusTime))
    .GroupBy(e => e.PlayerPackageId!.Value)
    .Select(g => new { PlayerPackageId = g.Key, Seconds = g.Sum(e => e.QuantitySeconds) })
    .ToDictionaryAsync(x => x.PlayerPackageId, x => x.Seconds, cancellationToken);
```
В памяти: для каждого игрока среди его активных пакетов взять пакет с максимальным `remainingLookup[pkgId]`, отфильтровав `остаток > 0`; имя = `pkg.Name`, минуты = `seconds / 60`.

- [ ] **Step 4: Пробросить в проекцию**

Заменить заглушки `ActivePackageName`/`ActivePackageRemainingMinutes` реальными значениями из построенного per-player словаря (напр. `bestPackageLookup[playerAccountId]`).

- [ ] **Step 5: Прогнать тест + проверить money-guard**

Run: `dotnet test tests/AFK4.Platform.Api.Tests --filter SearchPlayers`
Expected: PASS. Проверить вручную: batch остатка фильтрует ТОЛЬКО `package_time`/`bonus_time` (не `wallet`/`debt`) — иначе смешаются деньги с временем.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Platform.Api tests/AFK4.Platform.Api.Tests
git commit -m "feat(operator-players): активный пакет (имя+остаток минут) в списке, batch по ledger"
```

---

### Task 4: Frontend — контракт, модель, проекции, dev-mock, i18n

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/api/clients/players.ts` (зеркало DTO)
- Modify: `src/AFK4.Operator.App.Web/src/operatorHelpers.ts` (`PlayerClientItem` ~1319-1330, `projectPlayerClient` ~1319-1355)
- Modify: `src/AFK4.Operator.App.Web/src/players/playersModel.ts`
- Test: `src/AFK4.Operator.App.Web/src/players/playersModel.test.ts`
- Modify: `src/AFK4.Operator.App.Web/src/devMockBackend.ts` (фикстуры игроков)
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`

**Interfaces:**
- Produces (фронт-зеркало):
  ```ts
  export interface PlayerSearchResultDto {
    playerAccountId: Guid; displayName: string; phoneNumber: string | null;
    walletBalanceMinorUnits: number; debtBalanceMinorUnits: number;
    activePackageCount: number; isActive: boolean;
    createdAtUtc: string; lastActivityAtUtc: string | null;
    activePackageName: string | null; activePackageRemainingMinutes: number;
  }
  ```
- Produces (`PlayerClientItem`, добавить в конец):
  ```ts
  createdAtUtc: string | null;
  lastActivityAtUtc: string | null;
  activePackageName: string | null;
  activePackageRemainingMinutes: number;
  ```
- Produces (playersModel):
  - `isNewClient(createdAtUtc: string | null, nowMs: number, thresholdDays?: number): boolean` — `thresholdDays` дефолт 7.
  - `relativeVisitLabel(lastActivityAtUtc: string | null, nowMs: number, t: TFunc): string` — `сейчас`/`вчера`/`N дн.`/`N нед.`/`—`.
  - `activePackageLabel(name: string | null, minutes: number, t: TFunc): string | null` — `«Ночной 5ч» · 150 мин` или `null`.
  - `buildClientContextMap(sessions, reservations, clients): Map<string, ClientLiveContext>` — один проход, ключ = playerAccountId.

- [ ] **Step 1: Добавить i18n-ключи**

В `locales/ru.json`, `en.json`, `tg.json` добавить (реальные переводы, tg ≠ ru):
- `op.players.tag.new` — ru «Новый», en «New», tg «Нав»
- `op.players.table.col.client` / `.balance` / `.debt` / `.now` / `.visit`
- `op.players.visit.now` (ru «сейчас»), `.yesterday` (ru «вчера»), `.daysAgo` (ICU `{n} дн.`), `.weeksAgo` (`{n} нед.`), `.none` («—»)
- `op.players.package.timeBank` (ICU `«{name}» · {minutes} мин`)
Затем: `cd packages/i18n && /home/fedya/.bun/bin/bun run gen`.

- [ ] **Step 2: Написать падающие тесты модели**

В `playersModel.test.ts` добавить кейсы для `isNewClient` (дата < 7 дней → true; старая → false; null → false), `relativeVisitLabel` (0 дней→«сейчас», 1→«вчера», 3→«3 дн.», 10→«2 нед.», null→«—»), `activePackageLabel` (name+minutes → строка; null→null), `buildClientContextMap` (сессия+бронь матчатся по playerAccountId в map). И расширить существующий тест `projectPlayerClient` ассертами новых полей.

- [ ] **Step 3: Запустить — убедиться, что падает**

Run: `/home/fedya/.bun/bin/bun test src/players/playersModel.test.ts` (из `src/AFK4.Operator.App.Web`)
Expected: FAIL (функции не существуют).

- [ ] **Step 4: Реализовать зеркало DTO + PlayerClientItem + projectPlayerClient**

Добавить новые поля в `players.ts` (DTO), в `PlayerClientItem` (operatorHelpers), и в `projectPlayerClient` читать их из сырого DTO: `createdAtUtc: readString(player, 'createdAtUtc') || null`, `lastActivityAtUtc: readString(player, 'lastActivityAtUtc') || null`, `activePackageName: readString(player, 'activePackageName') || null`, `activePackageRemainingMinutes: readNumber(player, 'activePackageRemainingMinutes', 0)`. Обновить `fixturePlayers` — задать новые поля (напр. один клиент с пакетом, один недавний).

- [ ] **Step 5: Реализовать проекции playersModel**

Добавить `isNewClient`, `relativeVisitLabel`, `activePackageLabel`, `buildClientContextMap` (последняя переиспользует логику `buildClientContext` в цикле по клиентам, но грузит sessions/reservations один раз).

- [ ] **Step 6: Обновить dev-mock**

В `devMockBackend.ts` в фикстурах игроков добавить новые поля (createdAtUtc — свежая дата у одного, lastActivityAtUtc, activePackageName+minutes у одного) — иначе dev-preview покажет пусто/`—`.

- [ ] **Step 7: Прогнать тесты**

Run: `/home/fedya/.bun/bin/bun test src/players/playersModel.test.ts`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/api src/AFK4.Operator.App.Web/src/operatorHelpers.ts src/AFK4.Operator.App.Web/src/players/playersModel.ts src/AFK4.Operator.App.Web/src/players/playersModel.test.ts src/AFK4.Operator.App.Web/src/devMockBackend.ts locales packages/i18n/src/messages.ts
git commit -m "feat(operator-players): фронт-контракт+проекции для таблицы (Новый/визит/банк пакета)"
```

---

### Task 5: Frontend — компонент `ClientsTable`

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/players/ClientsTable.tsx`
- Create: `src/AFK4.Operator.App.Web/src/players/ClientsTable.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/styles/12-players.css` (стили таблицы — из mock-v7)

**Interfaces:**
- Consumes: `PlayerClientItem` (с новыми полями), `ClientSegment`, `ClientSegmentId`, `ClientLiveContext`, `isNewClient`/`relativeVisitLabel`/`activePackageLabel` из playersModel, `<Money>` из operatorPrimitives, `.status-pill`/`.ui-chip--filter` атомы.
- Produces: компонент
  ```ts
  ClientsTable(props: {
    clients: PlayerClientItem[];
    segments: ClientSegment[];
    activeSegment: ClientSegmentId;
    selectedClientId: string | null;
    search: string;
    showSkeleton: boolean;
    isLoading: boolean;
    emptyDescription: string;
    currencyCode: string;
    canCreatePlayer: boolean;
    liveContextByClient: Map<string, ClientLiveContext>;
    nowMs: number;
    onNewClient: () => void;
    onSearchChange: (value: string) => void;
    onSelectSegment: (id: ClientSegmentId) => void;
    onSelectClient: (playerAccountId: string | null) => void;
  })
  ```

**Разметка/стили:** взять из `scratchpad/clients-redesign-mock-v7.html` — блоки `.table-panel`, `.table-toolbar`, `.ctable-head`/`.ctable-row`/`.ctable-grid`, `.cc-*`. Колонки: **Клиент** (`.cc-client`: аватар-инициалы + имя + телефон; теги `.cc-tag` «Новый» если `isNewClient`, «Неактивен» если status inactive) · **Баланс** (`<Money>`) · **Долг** (`<Money>`, только если `debtMinorUnits>0`, строка получает класс-модификатор `debt` с красным кантом) · **Сейчас/пакет** (`.cc-now`: если `liveContextByClient.get(id)?.session` → `PC-01 · до HH:MM` с `.live`; иначе если `activePackageName` → `activePackageLabel`; иначе «—» с `.none`) · **Визит** (`relativeVisitLabel`; если сейчас играет — «сейчас»). Строка — `<button>` (сохранить accessible-name = имя клиента для тестов и клавиатуры), НЕ `<tr>`.

- [ ] **Step 1: Написать падающий тест**

`ClientsTable.test.tsx` по паттерну `ClientList.test.tsx` (рендер-хелпер `client()`+`renderTable()`, `I18nProvider initialLocale="ru"`). Кейсы: рендерит строку с именем (`screen.getByRole('button', { name: /Фариза/ })`), показывает баланс, показывает долг-кант только при долге, тег «Новый» для свежего клиента, «играет на PC» из liveContext, банк пакета когда не играет, клик по строке → `onSelectClient(id)`, сегмент-чипы → `onSelectSegment`, поиск → `onSearchChange`, skeleton при `showSkeleton`, empty при пустом+не loading.

- [ ] **Step 2: Запустить — убедиться, что падает**

Run: `/home/fedya/.bun/bin/bun test src/players/ClientsTable.test.tsx`
Expected: FAIL (нет файла компонента).

- [ ] **Step 3: Реализовать `ClientsTable.tsx`**

Реализовать по сигнатуре Interfaces + разметка из mock-v7. Toolbar: поиск (`.tt-search` + input) + сегмент-чипы (`.ui-chip--filter`) + «Новый клиент» (`.ui-btn--primary`). Тело: `.ctable-body` со строками-кнопками. Логика колонок — см. блок выше. Skeleton/empty как в `ClientList`.

- [ ] **Step 4: Добавить стили таблицы в 12-players.css**

Скопировать из mock-v7 CSS блоки `.table-panel`, `.table-toolbar`, `.tt-*`, `.ctable-grid`, `.ctable-head`, `.ctable-row` (+ `.debt`/`.selected`/`.inactive`), `.cc-*`, `.cc-tag`(+`.vip` не нужен, только базовый/neutral). Именование классов префиксом `clients-`/`ctable-` уже уникально.

- [ ] **Step 5: Прогнать тест**

Run: `/home/fedya/.bun/bin/bun test src/players/ClientsTable.test.tsx`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/players/ClientsTable.tsx src/AFK4.Operator.App.Web/src/players/ClientsTable.test.tsx src/AFK4.Operator.App.Web/src/styles/12-players.css
git commit -m "feat(operator-players): компонент ClientsTable (широкая таблица клиентов)"
```

---

### Task 6: Frontend — компонент `ClientDrawer` + пресеты пополнения

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/players/ClientDrawer.tsx`
- Create: `src/AFK4.Operator.App.Web/src/players/ClientDrawer.test.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/players/WalletZone.tsx` (пресеты сумм)
- Modify: `src/AFK4.Operator.App.Web/src/players/HistorySection.tsx` (проп `limit`/mini-режим)
- Modify: `src/AFK4.Operator.App.Web/src/styles/12-players.css` (drawer — из mock-v7)
- Modify: `locales/{ru,en,tg}.json` (пресеты/«своя сумма»/«вся история»/«последние операции»)

**Interfaces:**
- Consumes: `PlayerClientItem`, `ClientLiveContext`, `WalletSummaryDto`/`balanceMinorUnits`/`debtMinorUnits`, `LedgerEntryDto[]`, `LedgerRow`, `ClientActionsMenu`, `.status-pill`.
- Produces: `ClientDrawer` — узкая панель выбранного клиента. Пропсы = деньги + действия + последние операции + меню + контекст + `onClose`. Переиспользует бо́льшую часть пропс-контура `ClientDetail` (см. разведку), НО: без сплита пакеты/история, история = мини-список (последние 4-6) + «вся история →», продажи пакетов НЕТ.
  ```ts
  ClientDrawer(props: {
    client: PlayerClientItem;
    liveContext: ClientLiveContext;
    balanceMinorUnits: number; debtMinorUnits: number; currencyCode: string;
    recentEntries: LedgerEntryDto[];          // последние N для мини-списка
    topUpAmount: string; canTopUp: boolean;
    onChangeTopUpAmount: (v: string) => void; onTopUp: () => void;
    onPresetTopUp: (amount: number) => void;  // пресет +100/+200/+500
    canPayDebt: boolean; onOpenPayDebt: () => void;
    canManageClient: boolean; canCorrect: boolean;
    canCreateReservation: boolean;
    onCorrect: () => void; onCreateReservation: () => void;
    onSetPin: () => void; onEditProfile: () => void; onToggleActive: () => void;
    onOpenFullHistory: () => void;
    onClose: () => void;
  })
  ```
- Produces (`WalletZone` расширение): пресеты — props `presets: number[]` (напр. `[100,200,500]`) + `onPreset: (n:number)=>void`; сохранить существующие поля.

- [ ] **Step 1: Написать падающие тесты**

`ClientDrawer.test.tsx`: рендерит имя+телефон, показывает контекст-пилюли (`.status-pill.ok` при live-сессии, `.status-pill.neutral` при брони), баланс, долг-callout только при долге, пресет-кнопки → `onPresetTopUp(100)`, «Пополнить» → `onTopUp`, «Списать долг» видна только при `canPayDebt`, мини-список последних операций (ровно N строк), «вся история →» → `onOpenFullHistory`, меню «…» открывает пункты (Бронь/Изменить/ПИН/Коррекция/Деактивировать по правам), `onClose`. Плюс в `WalletZone.test.tsx` — кейс на пресеты.

- [ ] **Step 2: Запустить — убедиться, что падает**

Run: `/home/fedya/.bun/bin/bun test src/players/ClientDrawer.test.tsx src/players/WalletZone.test.tsx`
Expected: FAIL.

- [ ] **Step 3: Расширить WalletZone пресетами**

Добавить в `WalletZone` ряд пресет-кнопок (`.topup-presets` из mock-v7: `.preset-chip`), проп `presets`/`onPreset`. Оставить поле «своя сумма» + «Пополнить». Убрать из WalletZone плитки-стат (баланс/долг рисует drawer выше), либо оставить — согласовать с drawer-структурой (в drawer баланс = герой, долг = callout, отдельно от WalletZone-формы). Рекомендация: WalletZone превращается в форму пополнения (пресеты+поле+кнопка) + «Списать долг»/«Корректировка», а баланс/долг рисует drawer.

- [ ] **Step 4: Добавить mini-режим в HistorySection**

Добавить проп `limit?: number` — при заданном рендерить только первые `limit` записей без фильтр-чипов и «Показать ещё», плюс `onOpenFull?: () => void` для ссылки «вся история →». Полноценный фильтруемый режим (без limit) сохранить для будущего экрана полной истории.

- [ ] **Step 5: Реализовать ClientDrawer**

Разметка из mock-v7: `.drawer-panel` → `.drawer-head` (аватар+имя+телефон, «…»+«закрыть») → `.drawer-context` (пилюли из `liveContext`) → `.drawer-body` (баланс-герой, долг-callout, `WalletZone`-форма, мини-`HistorySection`). Меню «…» = `ClientActionsMenu` (расширить пунктами Бронь + Ручная корректировка — добавить пропсы `onCreateReservation`/`onCorrect`).

- [ ] **Step 6: Добавить стили drawer в 12-players.css**

Скопировать из mock-v7: `.drawer-panel`, `.drawer-head`, `.drawer-av`, `.drawer-id`, `.drawer-context`, `.drawer-body`, `.wallet-balance`, `.wallet-debt`, `.topup-presets`/`.preset-chip`, `.topup-row`, `.recent-*`. Пилюли `.status-pill` уже в `06-map-grid.css` — не дублировать.

- [ ] **Step 7: Прогнать тесты**

Run: `/home/fedya/.bun/bin/bun test src/players/ClientDrawer.test.tsx src/players/WalletZone.test.tsx src/players/HistorySection.test.tsx`
Expected: PASS.

- [ ] **Step 8: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/players/ClientDrawer.tsx src/AFK4.Operator.App.Web/src/players/ClientDrawer.test.tsx src/AFK4.Operator.App.Web/src/players/WalletZone.tsx src/AFK4.Operator.App.Web/src/players/HistorySection.tsx src/AFK4.Operator.App.Web/src/styles/12-players.css locales packages/i18n/src/messages.ts
git commit -m "feat(operator-players): компонент ClientDrawer (деньги+пресеты+мини-история+меню)"
```

---

### Task 7: Frontend — удалить продажу пакетов из Клиентов

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/players/PackagesSection.tsx` (удалить inline-покупку ~85-147) — либо удалить файл целиком, если показ активных пакетов в drawer не используется
- Modify: `src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx` (удалить `buyPackage`-ветку, связанный state/effects)
- Modify: `src/AFK4.Operator.App.Web/src/App.test.tsx` (удалить purchase-кейсы)
- Modify: `src/AFK4.Operator.App.Web/src/players/PackagesSection.test.tsx` (удалить/урезать)
- Modify: `src/AFK4.Operator.App.Web/src/styles/12-players.css` (удалить `.clients-package-buy*`, `.clients-package-preview*`, `.clients-package-deposit*`)

**Interfaces:**
- Produces: раздел Клиенты без покупки пакетов. `PlayerActionId` без `'buyPackage'`. Продажа целиком у Кассы.

- [ ] **Step 1: Удалить purchase-тесты**

Из `App.test.tsx` удалить кейсы «purchases a backend package from the selected client card» и «shows active backend packages…» (или переделать второй в read-only, если пакеты показываются в drawer — но по дизайну активный пакет виден в таблице, отдельный показ в drawer не обязателен; согласовать с Task 6). Из `PackagesSection.test.tsx` удалить кейсы покупки (afford-check/shortfall/buy/options-empty).

- [ ] **Step 2: Запустить — убедиться, что упавших нет от удаления (только зелёное сокращение)**

Run: `/home/fedya/.bun/bin/bun test src/App.test.tsx src/players/PackagesSection.test.tsx`
Expected: оставшиеся тесты PASS (удалённые исчезли).

- [ ] **Step 3: Удалить inline-покупку из кода**

Из `PackagesSection.tsx` вырезать блок покупки (select/preview/deposit/кнопка «Купить»); оставить только показ активных пакетов ИЛИ удалить файл, если drawer его не рендерит. Из `BackendPlayersWorkspace.tsx`: удалить ветку `buyPackage` в `runClientAction`, `PlayerActionId` тип, state `selectedPackageDefinitionId`/`packageBusy`, эффект загрузки `getPlayerPackages` (если пакеты больше не показываются в drawer), проп-передачи покупки. Удалить неиспользуемые импорты (`purchasePackage`, `getPackageOptions` если больше не нужны).

- [ ] **Step 4: Убрать CSS покупки**

Удалить из `12-players.css` блоки `.clients-package-buy`, `.clients-package-select`, `.clients-package-preview*`, `.clients-package-deposit*`.

- [ ] **Step 5: Прогнать тесты + сборку**

Run: `/home/fedya/.bun/bin/bun test src/App.test.tsx src/players/` и `/home/fedya/.bun/bin/bun run build`
Expected: PASS + сборка зелёная (проверит, что нет висячих импортов/типов).

- [ ] **Step 6: Commit**

```bash
git add -A src/AFK4.Operator.App.Web
git commit -m "refactor(operator-players): убрать продажу пакетов из Клиентов (уходит в Кассу)"
```

---

### Task 8: Frontend — интеграция layout table+drawer в оркестраторе

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx` (layout, live-context map, рендер ClientsTable+ClientDrawer)
- Modify: `src/AFK4.Operator.App.Web/src/App.test.tsx` (переписать players-сценарии под новый DOM)
- Modify: `src/AFK4.Operator.App.Web/src/styles/12-players.css` (layout `.clients-grid`, удалить остатки `.clients-layout`/`.client-row*`/`.clients-list*`/`.clients-detail-split`)
- Delete: `src/AFK4.Operator.App.Web/src/players/ClientList.tsx` + `.test.tsx`, `src/players/ClientDetail.tsx` + `.test.tsx`

**Interfaces:**
- Consumes: `ClientsTable`, `ClientDrawer`, `buildClientContextMap`.
- Produces: рабочий раздел Клиенты в новой раскладке.

- [ ] **Step 1: Переписать live-context на map для всех строк**

В эффекте кросс-контекста (сейчас грузит `sessions.timeline` + `reservations` для одного `selectedClient`) — строить `Map<playerAccountId, ClientLiveContext>` для ВСЕХ клиентов через `buildClientContextMap` (один fetch `sessions.timeline(branchId,{limit:null})` + reservations). Хранить `liveContextByClient` в state. Для drawer выбранного клиента — `liveContextByClient.get(selectedId) ?? empty`.

- [ ] **Step 2: Заменить layout JSX**

В рендере заменить `<section className="clients-layout"><ClientList/><ClientDetail/></section>` на `<div className="clients-grid">` с `<ClientsTable ...>` (всегда) и `<ClientDrawer ...>` (только если `selectedClient !== null`; иначе drawer не рендерится / показывает empty-state, таблица занимает всю ширину). Пробросить пресет-хендлер `onPresetTopUp(n)` (устанавливает `walletTopUpAmount` в `String(n)` и вызывает `topUp`). `onOpenFullHistory` — пока открывает существующий полный `HistorySection` в модалке/оставить заглушкой-TODO без функционала? НЕТ: реализовать простую модалку `PanelModal` с полной `HistorySection` (фильтры+пагинация) — переиспользовать существующий компонент.

- [ ] **Step 3: Обновить `.clients-grid` в CSS + удалить мёртвое**

Добавить `.clients-grid { display:grid; grid-template-columns: minmax(0,1fr) 372px; gap:10px; ... }` (из mock-v7). Удалить `.clients-layout`, `.clients-panel*` (списочные), `.clients-search`(если не переиспользован), `.clients-segment-chips`(перенесён в таблицу — проверить), `.clients-list*`, `.client-row*`, `.clients-detail-panel`, `.clients-detail-top`, `.clients-detail-split`, `.clients-subpanel*`. Оставить: `.clients-head*`, модалки, `.client-actions-menu*`, `.client-detail-banner`, `.client-context-*`(если ещё нужны).

- [ ] **Step 4: Удалить старые компоненты**

`git rm src/players/ClientList.tsx src/players/ClientList.test.tsx src/players/ClientDetail.tsx src/players/ClientDetail.test.tsx`. Убрать их импорты из оркестратора.

- [ ] **Step 5: Переписать players-сценарии в App.test**

Обновить селекторы: выбор клиента `fireEvent.click(screen.getByRole('button', { name: /Olim K\./ }))` (accessible-name строки сохранён), пополнение/долг/бронь/коррекция/создание — под новый DOM drawer. Сохранить регрессии §7.5 (default-reason для топап/pay-debt). Убедиться, что `playersSnapshotCache.clear()` в `beforeEach` присутствует (изоляция).

- [ ] **Step 6: Прогнать всё + сборка**

Run: `/home/fedya/.bun/bin/bun test src/App.test.tsx` (отдельным прогоном), затем `/home/fedya/.bun/bin/bun test src/players/`, затем `/home/fedya/.bun/bin/bun run build`.
Expected: всё PASS, сборка зелёная.

- [ ] **Step 7: Commit**

```bash
git add -A src/AFK4.Operator.App.Web
git commit -m "feat(operator-players): раскладка таблица+drawer, live-context для всех строк, снос списка/карточки"
```

---

## Финальная проверка (после всех задач)

- [ ] Полный фронт-прогон: `/home/fedya/.bun/bin/bun test` (весь operator-web) + `/home/fedya/.bun/bin/bun run build`.
- [ ] Полный бэк-прогон: `dotnet test tests/AFK4.Platform.Api.Tests tests/AFK4.Shared.Contracts.Tests` + `dotnet build` (включая WPF-проекты — компиляция мест конструирования record).
- [ ] i18n guard: `/home/fedya/.bun/bin/bun test packages/i18n/src/messages.test.ts` (parity + tg≠ru) и `i18nKeysExist.test.ts`.
- [ ] dev-preview: `bun run dev` → открыть URL, глазами проверить таблицу+drawer на dev-mock.
- [ ] Открытый пункт на доводку постфактум: **стиль строк таблицы** (плотность/зазоры) — не блокирует мерж, доводится на живом экране по фидбеку пользователя.

## Self-Review (проведён)

**Spec coverage:** таблица (Task 5) · drawer (Task 6) · долг условно (Task 5/6) · продажа→Касса (Task 7) · createdAtUtc/lastActivity/пакет (Task 1-3) · live-context все строки (Task 4/8) · тесты/i18n (все) · без VIP/Потрачено (не планируется). Открытые пункты спеки (стиль строк — постфактум; «вся история» — модалка в Task 8; активный пакет обогащением — Task 3) закрыты решениями.

**Placeholder scan:** реальные сигнатуры/пути/команды везде; TODO нет (единственное место «onOpenFullHistory» явно доопределено модалкой в Task 8 Step 2).

**Type consistency:** `PlayerSearchResultDto` (бэк record ↔ фронт interface) — одинаковый набор полей; `PlayerClientItem` расширен теми же именами; `activePackageRemainingMinutes` (минуты, не секунды) единообразно бэк↔фронт; `buildClientContextMap` возвращает `Map<string, ClientLiveContext>`, потребляется в Task 5/8.
