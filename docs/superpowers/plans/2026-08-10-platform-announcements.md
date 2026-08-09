# Анонсы платформы — Implementation Plan

Спека: `docs/superpowers/specs/2026-08-08-platform-product-operations-design.md`, §3.
Волна D, план 4 из пяти. Планы 1-3 закрыты (PR #143, #144, #145+#146, ревью-фиксы #147).

Платформа пишет сообщение клубам. Один источник, три канала: полоса в Операторе, экран истории
там же, письмо владельцу.

## Global Constraints

- Новые таблицы snake_case (`platform_announcements`, `announcement_reads`), колонки PascalCase в
  кавычках; сырой SQL миграции сверяется с `PlatformDbContext`, а не с именем C#-класса.
- Каждое действие платформы пишется в аудит: создание, правка, публикация, снятие.
- Ни одного пре-рендеренного текста с сервера — **кроме тела анонса**: это осознанное исключение,
  тело пишет живой человек (спека §3).
- Отказ несёт машинный код, фразу собирает клиент из `packages/i18n`.
- Тесты на гонки — только на настоящем Postgres (`[PlatformAdminPostgresFact]`).
- Права проверяются по текущему составу роли (плана 3 достижение) — новое право просто добавляется
  в `PlatformAdminPermissionNames`, страж полноты подхватит его сам.

## Что уже есть в коде (контекст для исполнителя)

- **Outbox писем** — `INotificationService.SendAsync(NotificationRequest)`, ключ идемпотентности
  строкой, шаблоны в `src/AFK4.Platform.Api/Notifications/Templates/{ru,en,tg}/<key>.json`
  (`subject`/`bodyText`/`bodyHtml`, плейсхолдеры `{{token}}`). Ключ регистрируется в
  `NotificationTemplateKeys` **и в его `All`** — иначе старт падает (это защита, а не помеха).
  Образец постановки письма владельцу: `Platform/Billing/EfInvoiceNotifier.cs`.
- **Права платформы** — `PlatformAdminPermissionNames` (+ reflection-страж полноты),
  роль резолвится из базы на каждом запросе.
- **Аудит** — `WritePlatformAuditAsync` в `EndpointHelpers.Audit.cs`, имена в `AuditActionNames`.
- **Оператор** — `src/AFK4.OrganizationAdmin.Web`. Шелл: `ShellHeader.tsx`, `ShellStatusBar.tsx`,
  рейл `WorkspaceRail.tsx` (секции в `navSections`), роутер `WorkspaceRouter.tsx`.
  Клиенты API — `src/api/clients/*.ts`, собираются в `operatorApiClients.ts`.
- **Панель платформы** — `src/AFK4.PlatformControl.Web`, клиенты в `src/api/platformClients/*.ts`,
  экраны по фиче, ошибки через `describeApiError` + своя таблица код→ключ.
- **`MgmtTable`/`MgmtDrawer`** — общий kit Оператора для «таблица + drawer».
- **`ConfirmDialog`** в панели платформы — для необратимого.

## Ловушка имени: «Новости» уже заняты

В Операторе есть `NewsWorkspace.tsx` и `/api/news` — это **новости клуба для его игроков**
(branchId, картинка, витрина в Customer.Web). Анонсы платформы — противоположное направление:
платформа → клубу. Два разных объекта, ни одной общей строки кода.

Поэтому в интерфейсе Оператора раздел называется **«Сообщения платформы»** (`platform.messages`),
никогда не «новости». Спутать их — значит показать оператору чужой экран под знакомым именем.

## Модель данных

### `platform_announcements`

| Колонка | Тип | Смысл |
|---|---|---|
| `PlatformAnnouncementId` | uuid PK | |
| `Title` | text (≤200) | |
| `Body` | text (≤4000) | Пишет человек, один язык, машинного перевода нет |
| `Severity` | text | `info` \| `warning` \| `critical` |
| `ShowFromUtc` | timestamptz | Начало окна показа |
| `ShowUntilUtc` | timestamptz | Конец окна показа |
| `AudienceKind` | text | `all` \| `plans` \| `organizations` |
| `AudiencePlanCodesJson` | text | Коды тарифов при `plans`, иначе `[]` |
| `AudienceOrganizationIdsJson` | text | Id клубов при `organizations`, иначе `[]` |
| `Status` | text | `draft` \| `published` \| `withdrawn` |
| `CreatedByPlatformAdminUserId` | uuid | |
| `CreatedAtUtc` / `UpdatedAtUtc` | timestamptz | |
| `PublishedAtUtc` | timestamptz? | |
| `EmailDispatchedAtUtc` | timestamptz? | **Ставится один раз**, второй публикацией не переписывается |

### `announcement_reads`

`PlatformAnnouncementId` + `StaffUserId` — составной PK, плюс `ReadAtUtc`.

Отметка на **сотрудника**, не на клуб: иначе первый закрывший полосу оператор скроет её у всех
семи остальных.

## Ключевые решения

1. **Письмо уходит ровно один раз** — при первом переходе в `published` и только при
   `severity >= warning`. Сторож — `EmailDispatchedAtUtc`, а не «статус изменился»: снял и
   опубликовал заново = то же сообщение, а не второе письмо владельцу. Второй слой — ключ
   идемпотентности outbox `announcement:{announcementId:N}:{organizationId:N}`.
2. **Правка опубликованного разрешена и писем не шлёт.** Исправленная опечатка не должна
   превращаться во второе письмо (спека). Аудиторию и важность у опубликованного менять **нельзя**:
   письма уже ушли по старому списку, смена аудитории задним числом делает след аудита ложью.
3. **Аудитория считается на сервере при выдаче**, а не разворачивается в список получателей при
   публикации. Клуб, заведённый завтра, попадает под «всех» — это и ожидается от «плановых работ».
   Исключение — письма: они уходят по составу аудитории **на момент публикации**, иначе рассылать
   пришлось бы вечно.
4. **Одно право `platform.announcements.manage`.** Отдельное «view» не заводится: как и у ролей,
   кто ведёт анонсы, тот их и смотрит. Появится потребность у поддержки — добавится тогда.
5. **Полоса показывает не больше одного анонса за раз** — самый важный из непрочитанных
   (`critical` > `warning` > `info`, при равенстве — свежий). Три полосы подряд не читает никто.
6. **Окно показа обязательно с обеих сторон.** Анонс без конца — это анонс про субботние работы,
   который висит до декабря.

## Структура файлов

```
src/AFK4.Platform.Api/
  Data/PlatformAnnouncementEntity.cs                    новый
  Data/AnnouncementReadEntity.cs                        новый
  Data/PlatformDbContext.cs                             +2 DbSet, конфигурация
  Data/Migrations/*_AddPlatformAnnouncements.cs         новый
  Platform/Announcements/PlatformAnnouncementService.cs новый  (ведение: CRUD + переходы)
  Platform/Announcements/AnnouncementFeedService.cs     новый  (выдача клубу + отметка прочтения)
  Platform/Announcements/AnnouncementAudience.cs        новый  (чистая функция «подходит ли клуб»)
  Endpoints/PlatformAnnouncementEndpoints.cs            новый  (панель платформы)
  Endpoints/AnnouncementFeedEndpoints.cs                новый  (Оператор)
  Notifications/NotificationTemplateKeys.cs             +ключ platform.announcement
  Notifications/Templates/{ru,en,tg}/platform.announcement.json  новые
  Audit/AuditActionNames.cs                             +4 имени
  Program.cs                                            регистрация

src/AFK4.Shared.Contracts/Platform/Announcements/AnnouncementContracts.cs  новый

src/AFK4.PlatformControl.Web/src/
  api/platformClients/announcements.ts                  новый
  platform/announcements/AnnouncementsScreen.tsx        новый
  platform/announcements/announcementsModel.ts          новый

src/AFK4.OrganizationAdmin.Web/src/
  api/clients/platformMessages.ts                       новый
  PlatformMessageBanner.tsx                             новый  (полоса в шелле)
  PlatformMessagesWorkspace.tsx                         новый  (экран истории)
```

---

### Task 1: Схема, контракты и правило аудитории

**Файлы:** сущности, `PlatformDbContext`, миграция, `AnnouncementAudience.cs`, контракты,
`PlatformAdminPermissionNames` (+`ManageAnnouncements = "platform.announcements.manage"`),
`AuditActionNames` (+`platform.announcements.{create,update,publish,withdraw}`).

`AnnouncementAudience` — чистая функция, ЕДИНСТВЕННОЕ место, где живёт правило «подходит ли клуб»:

```
bool Matches(AudienceKind kind, IReadOnlySet<string> planCodes, IReadOnlySet<Guid> organizationIds,
             Guid organizationId, string? organizationPlanCode)
```

Её же зовёт и выдача клубу, и разворачивание получателей письма — два разных пути на одном правиле,
иначе полоса и письмо разойдутся, и никто этого не заметит.

**Тесты:** `all` берёт любой клуб; `plans` — только совпадающий код тарифа и **не берёт клуб без
подписки** (`null` — не совпадение, а не «подходит всем»); `organizations` — только перечисленных;
пустой список при `plans`/`organizations` не берёт никого (а не всех).

### Task 2: Ведение анонсов — сервис и эндпоинты платформы

**Файлы:** `PlatformAnnouncementService.cs`, `PlatformAnnouncementEndpoints.cs`, `Program.cs`.

Эндпоинты (все под `ManageAnnouncements`, отказ и по правам, и по существу пишется в аудит —
урок #147):

- `GET /api/platform/announcements` — список с состоянием рассылки и счётчиком прочтений.
- `POST /api/platform/announcements` — создать черновик.
- `PUT /api/platform/announcements/{id}` — правка.
- `POST /api/platform/announcements/{id}/publish`
- `POST /api/platform/announcements/{id}/withdraw`

Инварианты (все внутри serializable-транзакции, как `PlatformRoleService`):

- заголовок и тело непустые, ≤200 / ≤4000; `ShowUntilUtc > ShowFromUtc`;
- `severity` и `audienceKind` — из закрытого набора, иначе `invalid_announcement`;
- при `plans`/`organizations` список не пуст, коды тарифов существуют, id клубов существуют;
- правка **опубликованного** не меняет `Severity`, `AudienceKind` и списки аудитории →
  `published_audience_locked`;
- правка **снятого** запрещена → `withdrawn_announcement`;
- `publish` только из `draft` → иначе `invalid_transition`; ставит `Status`, `PublishedAtUtc`,
  и при `severity >= warning` — письма владельцам клубов из аудитории **на этот момент**;
- `EmailDispatchedAtUtc` ставится только если он был `null`;
- `withdraw` только из `published`.

**Тесты:**
- публикация `critical` ставит письма всем владельцам аудитории; повторная публикация после
  `withdraw` **не** ставит вторых (проверять по числу строк outbox, не по флагу);
- публикация `info` писем не ставит вовсе;
- правка опубликованного меняет текст и НЕ трогает `EmailDispatchedAtUtc`;
- правка опубликованного с другой аудиторией отклоняется;
- `plans`-аудитория ставит письма только клубам на этом тарифе;
- отказ по существу лёг в аудит.

### Task 3: Выдача клубу и отметка прочтения

**Файлы:** `AnnouncementFeedService.cs`, `AnnouncementFeedEndpoints.cs`.

- `GET /api/announcements` — опубликованные, попадающие в окно (`ShowFromUtc <= now < ShowUntilUtc`)
  и в аудиторию организации звонящего; каждый несёт `isRead` для **текущего сотрудника**.
- `POST /api/announcements/{id}/read` — идемпотентно (повторный вызов — 200, не 409).

Организация берётся из `StaffContext`, а не из тела запроса — иначе клуб читает чужие сообщения
(тот же IDOR-урок, что и в org-эндпоинтах панели).

**Тесты:**
- клуб вне аудитории не видит анонс;
- анонс до `ShowFromUtc` и после `ShowUntilUtc` не отдаётся;
- черновик и снятый не отдаются;
- прочтение одним сотрудником **не** помечает прочитанным для второго (это весь смысл таблицы);
- повторное прочтение не падает и не плодит строк;
- запрос без организации в контексте не отдаёт ничего.

### Task 4: Панель платформы — экран анонсов

**Файлы:** `announcements.ts`, `AnnouncementsScreen.tsx`, `announcementsModel.ts`, роутинг,
i18n-ключи на ru/en/tg (настоящие таджикские, не копия ru).

Таблица: заголовок, важность, окно, аудитория, статус, «письма ушли», «прочитали N из M».
Drawer: форма создания/правки. Публикация и снятие — через `ConfirmDialog` (публикация рассылает
письма, снятие гасит полосу у всех: оба видны снаружи).

У опубликованного анонса поля важности и аудитории **выключены** с объяснением почему — рычаг,
который заведомо ответит отказом, хуже отсутствующего.

**Тесты:** список рисуется; публикация спрашивает подтверждение; у опубликованного аудитория не
редактируется; каждый машинный код отказа получает свою фразу.

### Task 5: Оператор — полоса и экран истории

**Файлы:** `platformMessages.ts`, `PlatformMessageBanner.tsx`, `PlatformMessagesWorkspace.tsx`,
рейл + роутер, i18n.

- **Полоса** в шелле: самый важный непрочитанный анонс, цвет по важности (`critical` — тревожный,
  `warning` — янтарный, `info` — нейтральный; амбер только для предупреждения, деньги остаются
  белыми — правило Кассы). Кнопка «Прочитано» гасит её для этого сотрудника и открывает следующую.
  Ссылка «Все сообщения» ведёт на экран истории.
- **Экран истории**: все действующие и прошедшие анонсы клуба, прочитанные приглушены.

Фолбэк как у фич: список не пришёл — полосы просто нет, экран показывает состояние ошибки с
повтором. Некорректная форма 200-ответа уходит в тот же путь (`Array.isArray`) — иначе `undefined`
роняет весь шелл в белое.

**Тесты:** полоса показывает самый важный непрочитанный; «Прочитано» зовёт эндпоинт и открывает
следующий; при пустом списке полосы нет; кривой ответ не роняет шелл.

## Финальная проверка перед завершением ветки

1. `dotnet build` + `dotnet test tests/AFK4.Platform.Api.Tests` целиком.
2. Прогон с настоящим Postgres: четыре строки подключения + `AFK4_REQUIRE_POSTGRES_TESTS=1`.
3. `bun test` и **`bun run build`** в `AFK4.PlatformControl.Web` и `AFK4.OrganizationAdmin.Web`
   (зелёный `bun test` ≠ зелёная сборка: `tsc -b` типизирует и тесты).
4. `bun test` в `packages/i18n` — паритет ключей трёх локалей и гард `tg !== ru`.

## Отклонения от плана, принятые при реализации

- **Фид клуба живёт в org-группе** (`/api/organizations/{id}/announcements`), а не в корне
  `/api/announcements`: так устроены все клубские эндпоинты, и организация берётся из
  `StaffContext`, а не из адреса.
- **Тариф читается из `Organizations.PlanCode`**, а не из `OrganizationSubscriptions`: оттуда его
  берут лимиты (`EfPlanLimitGuard`) и фичи (`EfOrganizationEntitlements`). Два источника означали
  бы, что «клуб на тарифе X» для анонса и для фичи — разные утверждения.
- **Кроме полосы, в шапке Оператора появилась кнопка с числом непрочитанных.** Слот полосы один и
  занят по приоритету (режим поддержки → задолженность → анонс), а после прочтения полоса исчезает
  вовсе — без постоянного входа история была бы недостижима. Отдельная рабочая зона не заводилась:
  разделы рейла гейтятся правами настройки, а сообщение адресовано всем сотрудникам.
- **Экран истории — модальное окно (`PanelModal`), а не рабочая зона**, по той же причине.
- **`ExecuteInSerializableTransactionAsync` ловит и нарушение уникальности.** Postgres-тест гонки
  показал: два одновременных «опубликовать» доходят до постановки писем оба, и проигравший
  упирается в уникальный ключ идемпотентности очереди. Без этого он получал бы сырой 500 вместо
  «повторите». Найдено тестом, не ревью.

## Что сознательно не делается

- **Перевод тела анонса на en/tg** — три поля означали бы, что кто-то реально переводит каждый
  анонс на таджикский; в жизни туда скопируют русский. Интерфейс вокруг локализован как обычно.
- **Планирование публикации на будущее** — `ShowFromUtc` уже даёт отложенный показ; отдельный
  планировщик публикации добавил бы фоновой процесс ради того же результата.
- **Ответ клуба на анонс** — это переписка, а не анонс; отдельная тема со своей моделью.
- **Показ анонсов игрокам** — адресат платформы это клуб, а не его посетители.
