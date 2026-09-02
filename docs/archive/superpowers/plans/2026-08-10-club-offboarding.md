# Офбординг клуба — Implementation Plan

Спека: `docs/superpowers/specs/2026-08-08-platform-product-operations-design.md`, §4.
Волна D, план 5 из пяти — последний и самый опасный. Планы 1-4 закрыты (#143, #144, #145+#146,
#148, ревью-фиксы #147).

`deletion_pending` из таблички на двери становится настоящим циклом: заявка → выгрузка →
стирание. Между шагами — человек.

## Global Constraints

- **Необратимое запускает человек.** Автостирания по расписанию нет; срок только показывает «пора».
- Подтверждение стирания — ввод `Slug` клуба руками.
- Записи в аудит платформы до и после стирания; выгрузка тоже пишется в аудит (это вынос
  персональных данных наружу).
- Одна транзакция на всё стирание. Порядок таблиц важен из-за внешних ключей.
- Тест обязан проверять **обе стороны**: что личное исчезло и что финансовое уцелело.
  Обратная проверка важнее прямой — снести лишнее легче, чем недоснести, и заметно это станет
  через месяцы.
- Postgres-тест обязателен: настоящие внешние ключи и каскады InMemory не проверяет вовсе.

## Ключевое решение: что стирается, что остаётся

Разделительная черта — **чьи это данные**. Персональные и операционные данные клуба стираются;
финансовые отношения клуба с платформой и след действий платформы остаются.

### Стираем (52 таблицы)

| Группа | Сущности |
|---|---|
| Игроки | `PlayerAccount`, `PlayerCredential`, `PlayerAccessToken`, `PlayerRefreshToken`, `PlayerPackage`, `LedgerEntry` |
| Сотрудники | `StaffUser`, `StaffRoleAssignment`, `StaffAccessToken`, `StaffRefreshToken`, `StaffInvite`, `StaffPhoneOtp`, `PasswordResetToken`, `StaffMoneyCap`, `MoneyActionRequest`, `NotificationPreference`, `AnnouncementRead` |
| Сеансы и брони | `Session`, `SessionEvent`, `SessionLease`, `SessionCommandIdempotency`, `Reservation` |
| Касса и продажи | `Shift`, `CashMovement`, `PosSale`, `PosSaleLine`, `Receipt`, `ShopOrder`, `ShopOrderLine`, `Payment`, `PaymentIntent`, `BillingCommandIdempotency` |
| Склад и каталог | `PosProduct`, `PosProductCategory`, `ProductBarcode`, `StockMovement`, `Tariff`, `TariffVersion`, `PackageDefinition` |
| Устройства | `Device`, `DeviceCredential`, `DeviceSeatAssignment`, `DeviceEnrollmentCode`, `DeviceCommand`, `DeviceInstalledApp`, `DeviceUpdateStatus`, `UpdateRolloutTarget` |
| Филиалы и топология | `Branch`, `Zone`, `Seat`, `Wall`, `BranchDailySnapshot` |
| Прочее клуба | `NewsItem`, `UploadedMedia`, `OrganizationLoyaltySettings`, `EskhataMerchantConfig`, `DcPayLinkConfig`, `ReportSchedule`, `OrganizationOwnerInvite`, `OrganizationFeatureOverride`, `OutboxMessage`, `NotificationOutbox` (+`Attachment`) |
| Аудит клуба | `AuditRecord`, у которых `ActorPlatformAdminUserId IS NULL` |

### Оставляем

| Сущность | Зачем |
|---|---|
| `Organization` | Архивная строка: имя, `Slug`, даты, финальный статус `purged` |
| `OrganizationSubscription`, `Invoice`, `SubscriptionDailySnapshot` | «Сколько этот клуб заплатил в марте» — ответ должен остаться |
| `AuditRecord` с `ActorPlatformAdminUserId IS NOT NULL` | След действий платформы, включая записи о самом стирании |
| `OrganizationSupportNote`, `PlatformSupportAccessGrant` | Записи платформы о работе с клубом — та же природа, что аудит |

**Почему аудит режется по актору, а не целиком.** Одна таблица хранит два разных журнала: действия
сотрудников клуба (кто выдал скидку, кто открыл смену — персональные данные) и действия платформы
(кто сменил статус, кто вошёл под клиента). Первое стирается вместе с людьми, второе обязано
пережить стирание — иначе исчезнет запись о самом стирании.

**Контактные поля организации не чистятся.** `ContactEmail`/`ContactPhone` — реквизиты юрлица в
счетах, которые мы оставляем. Обнулить их значит оставить счета без плательщика.

## Модель данных

К `OrganizationEntity` добавляются два поля:

| Колонка | Тип | Смысл |
|---|---|---|
| `PurgeEligibleAtUtc` | timestamptz? | Раньше этой даты стирать нельзя. Ставится при переводе в `deletion_pending`, по умолчанию +30 дней |
| `PurgedAtUtc` | timestamptz? | Когда стёрли. Вместе со статусом `purged` отличает «ушёл и стёрт» от «собирается уйти» |

Новый статус `purged` в `OrganizationStatusNames`. Списки клубов и аналитика исключают его так же,
как `deletion_pending` (`EfDebtOverviewService` уже фильтрует — добавить второй статус).

## Структура файлов

```
src/AFK4.Platform.Api/
  Data/OrganizationEntity.cs                              +2 поля
  Data/Migrations/*_AddOrganizationPurge.cs               новый
  Platform/Offboarding/OrganizationPurgeService.cs        новый  (стирание, одна транзакция)
  Platform/Offboarding/OrganizationExportService.cs       новый  (ZIP из CSV, потоком)
  Endpoints/PlatformOffboardingEndpoints.cs               новый
  Platform/Tenancy/EfPlatformOrganizationService.cs       срок при deletion_pending, фильтр purged
  Audit/AuditActionNames.cs                               +3 имени

src/AFK4.Shared.Contracts/Platform/Organizations/
  OrganizationStatusNames.cs                              +purged
  OffboardingContracts.cs                                 новый

src/AFK4.PlatformControl.Web/src/
  api/platformClients/offboarding.ts                      новый
  platform/organizations/OffboardingSection.tsx           новый  (вкладка карточки клуба)
  platform/organizations/offboardingModel.ts              новый
```

---

### Task 1: Срок стирания и статус `purged`

**Файлы:** `OrganizationEntity`, миграция, `OrganizationStatusNames`, `EfPlatformOrganizationService`,
контракты.

- Перевод в `deletion_pending` ставит `PurgeEligibleAtUtc = now + 30 дней`, если он ещё пуст
  (повторный перевод срок НЕ продлевает — иначе достаточно потыкать статус, чтобы стирание
  отодвигалось вечно).
- Возврат в `active` очищает `PurgeEligibleAtUtc`: передумать — нормальный сценарий, и следа от
  несостоявшегося ухода оставаться не должно.
- `purged` — терминальный статус: из него не переводят никуда, попытка → `organization_purged`.
- Списки и аналитика исключают `purged` наравне с `deletion_pending`.

**Тесты:** срок ставится один раз; возврат в `active` его снимает; из `purged` статус не меняется;
`purged` не появляется в списках и в обзоре задолженности.

### Task 2: Выгрузка данных клуба

**Файл:** `OrganizationExportService.cs`, эндпоинт `GET /api/platform/organizations/{id}/export`
под правом `platform.organizations.limits.update`… — **нет**: заводится отдельное право
`platform.organizations.offboarding.manage`. Выгрузка персональных данных и стирание — не то же
самое, что правка лимитов, и одалживать чужое право здесь нельзя.

ZIP с CSV: `players.csv`, `sessions.csv`, `sales.csv`, `products.csv`, `reservations.csv`.
Отдаётся потоком в ответ; на диске ничего не остаётся и фоновой сборки нет.

- CSV пишется с `;` (Excel в русской локали) и BOM — иначе кириллица открывается кракозябрами.
- Значения экранируются: кавычки удваиваются, поля с разделителем/переводом строки берутся в
  кавычки. Иначе имя «Иванов; Пётр» ломает раскладку файла.
- Деньги — в мажорных единицах строкой, даты — ISO-8601 UTC.
- Факт выгрузки пишется в аудит: это вынос персональных данных наружу.

**Тесты:** архив содержит все пять файлов; строки игроков попадают в `players.csv`; поле с
разделителем экранировано; выгрузка чужой организации невозможна; факт в аудите.

### Task 3: Стирание

**Файл:** `OrganizationPurgeService.cs`, эндпоинт
`POST /api/platform/organizations/{id}/purge` с телом `{ "slug": "..." }`.

Предусловия (каждое — свой код отказа):
- статус ровно `deletion_pending` → иначе `not_deletion_pending`;
- `PurgeEligibleAtUtc` наступил → иначе `purge_not_due`;
- присланный `slug` совпадает с настоящим → иначе `slug_mismatch`.

Порядок удаления — от листьев к корню (дочерние строки прежде родительских). Одна транзакция.
После стирания: статус `purged`, `PurgedAtUtc = now`, `PurgeEligibleAtUtc` очищается.

Запись в аудит **до** (намерение) и **после** (факт, с числом удалённых строк по группам). Обе с
`ActorPlatformAdminUserId`, поэтому обе переживают собственное стирание.

**Тесты (обе стороны обязательны):**
- личное исчезло: игроки, сотрудники, сеансы, продажи, устройства, филиалы, аудит клуба;
- финансовое уцелело: строка организации, подписка, счета, суточные снимки подписки, аудит
  платформы, заметки поддержки;
- аудит стирания на месте **после** стирания (запись о самом событии не должна себя же уничтожить);
- отказ при неверном `slug`, при недошедшем сроке, при статусе не `deletion_pending`;
- Postgres-тест: стирание проходит на настоящих внешних ключах (порядок таблиц верен), повторный
  вызов отвечает понятным отказом, а не падает.

### Task 4: Панель — вкладка «Уход клуба»

**Файлы:** клиент, `OffboardingSection.tsx`, `offboardingModel.ts`, вкладка в карточке клуба,
i18n на ru/en/tg.

- Показывает статус, дату «стирать можно с», кнопку выгрузки и кнопку стирания.
- Кнопка стирания активна только при `deletion_pending` и наступившем сроке; иначе выключена **с
  объяснением почему** — рычаг, который заведомо ответит отказом, хуже отсутствующего.
- Подтверждение — `ConfirmDialog` с обязательным вводом `Slug` (`reasonLabel` уже умеет требовать
  непустое значение; здесь проверяется точное совпадение).
- У стёртого клуба вкладка показывает дату стирания и больше ничего не предлагает.

**Тесты:** кнопка выключена до срока и объясняет причину; подтверждение требует точный `slug`;
стёртый клуб не предлагает действий; каждый код отказа получает свою фразу.

## Финальная проверка перед завершением ветки

1. `dotnet build` + весь `dotnet test tests/AFK4.Platform.Api.Tests`.
2. Прогон с настоящим Postgres — все четыре строки подключения + `AFK4_REQUIRE_POSTGRES_TESTS=1`.
3. `bun test` и **`bun run build`** в `AFK4.PlatformControl.Web`.
4. `bun test` в `packages/i18n`.

## Что сознательно не делается

- **Автостирание по расписанию** — необратимое действие запускает человек.
- **Фоновая сборка выгрузки с хранением файла** — поток в ответ проще и не оставляет персональных
  данных лежать на диске.
- **Восстановление стёртого клуба** — стирание на то и стирание; «корзина» превратила бы его в
  видимость удаления.
- **Выгрузка по частям и докачка** — при масштабе клуба один поток достаточен; докачка требует
  хранения файла, от которого мы отказались.
