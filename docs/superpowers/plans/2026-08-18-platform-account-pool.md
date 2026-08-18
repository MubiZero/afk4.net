# Волна 1 — фундамент «общего котла платформы»

Дата: 2026-08-18. Статус: план, код не начат.

Источники решения: решение владельца от 18.08.2026 о трёх слоях (личность/PIN/репутация —
платформенные; деньги, кешбэк, стаж, долг, чеки, локальные запреты — клубные), два концепта
интерфейса (приложение игрока и сторона стойки) и собранная из них очередь бэкенда на 29 работ.
Волна 1 — это семь работ, без которых модель не собирается вовсе.

Общего кошелька платформы не будет: у каждого клуба своя касса и отчётность. Клуб не заводит
человека — он получает доступ к уже существующей записи в момент первого действия.

---

## 0. Что уже есть в коде (проверено чтением, не предположения)

Прежде чем планировать, я сверил семь пунктов с кодом. Четыре утверждения из постановки задачи
оказались неточными — это меняет объём работ, и лучше знать это до, а не после.

| Утверждение | Как на самом деле |
|---|---|
| «Заморозки нет ни в модели, ни в словах» | **Есть и работает.** `src/AFK4.Platform.Api/Reservations/ReservationHold.cs`: холд — отрицательная запись журнала `EntryType = "reservation_hold"`, связь с бронью через `Reason`, снятие — реверс с `ReversesLedgerEntryId`, идемпотентное. Причины снятия: `cancelled`, `seated`, `no_show`. Тесты: `tests/AFK4.Platform.Api.Tests/ReservationHoldTests.cs`. |
| «Неявка не разобрана» | Разбирается фоном: `ReservationNoShowRunner` + `ReservationNoShowHostedService`, grace 15 минут. Но **деньги при неявке всегда возвращаются** — удержания в пользу клуба нет, и «не приехал» изображается отменой с `CancelReason = "no-show"`, отдельного состояния нет. |
| «Авто/ручное подтверждение надо сделать» | Авто-подтверждение уже есть, но **зашито в код**: `EfReservationService.ShouldAutoConfirmOnlineAsync` — «кошелёк > 0 и нет долга». Плюс любая бронь с выбранным тарифом подтверждается автоматически всегда. Настройки филиала нет вообще. |
| «PIN хранится отдельно» | PIN — это `PlayerCredentialEntity.PasswordHash` (тот же ASP.NET `PasswordHasher`, что и пароль), запись 1:1 с клубным `PlayerAccountEntity`, то есть **PIN сегодня клубный по факту хранения**. Блокировка после 5 ошибок на 15 минут уже есть. |

Ещё три факта, которые определяют форму работ:

- **Денег как колонки не существует нигде.** Баланс и долг — это `SUM` по `ledger_entries`
  (`Billing/LedgerBalanceProjector.cs`). Холд уже вычтен из баланса, потому что он и есть
  отрицательная запись. Значит «третья сумма» — это не новая сущность денег, а **новое число в
  сводке**, и это радикально дешевле, чем кажется из концепта.
- **Токен игрока привязан к организации.** `OpaquePlayerTokenService.ValidateAsync` возвращает
  `PlayerContext(PlayerAccountId, OrganizationId, PhoneVerified)`; вход
  `POST /api/public/player/sign-in/code` принимает `organizationId`. Приложение уже выбирает клуб
  первым экраном. Перевод входа на личность — это и есть самая рискованная часть волны.
- **Одна миграция = одна ветка.** 108 миграций и один `PlatformDbContextModelSnapshot.cs`
  (4714 строк) на весь проект, один `PlatformDbContext`. Параллельные миграции гарантированно
  дают конфликт в снапшоте.

---

## 1. Целевая модель данных

```
platform_persons                  ← личность: телефон, имя, язык, PIN, сетевой запрет
      │ 1
      │
      │ N  (PlatformPersonId, nullable)
player_accounts                   ← связь человека с клубом + держатель клубного счёта
      │
      └── ledger_entries          ← деньги клуба: остаток, долг, холд (как есть сегодня)
```

Ключевые решения:

1. **`PlayerAccountEntity` не переименовывается и не разрезается.** Он и есть «связь и счёт».
   Добавляется одна колонка `PlatformPersonId uuid NULL`. Это сохраняет ~40 маршрутов `/api/me`,
   весь биллинг и всю операторскую часть работающими.
2. **`PlatformPersonId` остаётся nullable навсегда.** Записи, заведённые на стойке без телефона,
   — это не переходный мусор, а нормальный случай: гость без приложения. Они живут как чисто
   клубные и подшиваются к личности в тот день, когда человек подтвердит номер.
3. **Токен выдаётся личности, а клуб выбирается запросом.** У токена есть необязательный
   «закреплённый клуб» — так старые клиенты продолжают работать без единой правки (см. фазу 2).
4. **Холд остаётся отрицательной записью журнала.** Отдельного счёта `hold` не заводим: сегодня
   любая проверка «хватает ли денег» автоматически видит холд, а с отдельным счётом каждую из
   девяти таких проверок пришлось бы переписывать, и каждая — это потенциальное «списали дважды».

---

## 2. Одна миграция на всю волну

**Имя:** `AddPlatformAccountPool`.
**Команда:** `dotnet ef migrations add AddPlatformAccountPool --project src/AFK4.Platform.Api --startup-project src/AFK4.Platform.Api`.
**Файлы:** `src/AFK4.Platform.Api/Data/Migrations/<ts>_AddPlatformAccountPool.cs`, `.Designer.cs`,
обновлённый `PlatformDbContextModelSnapshot.cs`.

Миграция создаётся **целиком в фазе 1**, до того как написана хоть одна строка бизнес-логики
остальных шести пунктов. Это единственный способ совместить «фазы размером с коммит» с «одной
миграцией»: схема проектируется вперёд, дальнейшие фазы только пишут код.

Состав, в порядке применения:

| # | Что | Зачем |
|---|---|---|
| 1 | `platform_persons`: `PlatformPersonId`, `PhoneNumber varchar(32) NOT NULL`, `DisplayName varchar(160)`, `PreferredLocale varchar(16) NULL`, `PhoneVerifiedAtUtc`, `PinHash varchar(512) NULL`, `PinSetAtUtc NULL`, `PinFailedCount int`, `PinLockedUntilUtc NULL`, `NetworkBanAtUtc NULL`, `NetworkBanReason varchar(500) NULL`, `IsActive bool`, `CreatedAtUtc`, `UpdatedAtUtc`. Уникальный индекс по `PhoneNumber`. | Пункт 1, 3, 4 |
| 2 | `platform_person_access_tokens`, `platform_person_refresh_tokens` — по образцу `player_access_tokens`/`player_refresh_tokens`: `TokenHash bytea`, `PinnedOrganizationId uuid NULL`, `ExpiresAtUtc`, `RevokedAtUtc`. | Фаза 2 |
| 3 | `platform_phone_otps`: `Phone varchar(32)`, `Purpose int`, `CodeHash`, `CreatedAtUtc`, `ExpiresAtUtc`, `AttemptCount`, `ConsumedAtUtc`. Индекс `(Phone, Purpose, CreatedAtUtc)`. | Пункт 3: у незнакомого номера нет `PlayerAccountId`, а сегодняшний `player_phone_otps` требует его обязательно |
| 4 | `player_accounts.PlatformPersonId uuid NULL` + индекс; `player_accounts.CreatedFromApp bool NOT NULL DEFAULT false`; **уникальный** индекс `(PlatformPersonId, OrganizationId) WHERE PlatformPersonId IS NOT NULL` | Пункт 1: у человека в одном клубе ровно один счёт — это и есть защита от второй копии на уровне БД, а не на уровне надежды |
| 5 | `branch_booking_settings`: `BranchId` (PK), `OrganizationId`, `AcceptanceMode varchar(16)`, `RespondWithinMinutes int`, `RequirePrepaymentFromNewGuests bool`, `MaxActiveReservationsForNewGuests int`, `RegularAfterVisits int`, `HoldSeatAfterStartMinutes int`, `KeepPrepaymentOnNoShow bool`, `UpdatedAtUtc`, `UpdatedByStaffUserId` | Пункт 6 |
| 6 | `reservations.RespondByUtc timestamptz NULL`, `reservations.ConfirmedAtUtc timestamptz NULL` | Пункт 7 |
| 7 | `platform_reputation_snapshots`: `PlatformPersonId` (PK), `NetworkVisits int`, `NetworkNoShows int`, `CalculatedAtUtc` | Пункт 2 — защита от вычисления соседа по времени (см. раздел 5) |
| 8 | `platform_identity_migration_findings`: `FindingId`, `Kind varchar(32)`, `PlatformPersonId uuid NULL`, `PlayerAccountId uuid NULL`, `OrganizationId uuid NULL`, `DetailsJson`, `ResolvedAtUtc NULL` | Перенос ничего не блокирует и ничего не решает за человека: спорные случаи он **записывает**, а разбирают их руками после (раздел 4) |
| 9 | SQL-перенос данных `DO $cutover$ ... $cutover$` | Раздел 4 |

`Down` откатывает пункты 1–8 в обратном порядке. **`Up` ничего не удаляет и ничего не
переписывает в существующих таблицах** — только добавляет. Это важнее, чем кажется: см. раздел 4
про откат.

Образец для SQL внутри миграции уже есть в репозитории:
`src/AFK4.Platform.Api/Data/Migrations/20260728161159_SeparatePlatformAndOrganizationIdentity.cs`
(`DO $cutover$` с разбором существующих строк) и тест к нему
`tests/AFK4.Platform.Api.Tests/Migrations/SeparatePlatformAndOrganizationIdentityMigrationTests.cs`
(накатывает миграцию на одноразовую схему настоящего Postgres и проверяет результат построчно).
Повторяем форму, но не поведение при плохих данных: та миграция падала, эта — записывает находку
и идёт дальше (решение владельца, раздел 4).

---

## 3. Фазы

Каждая фаза — один коммит на своей ветке от `main`. Тесты пишутся первыми: AGENTS.md требует TDD
для доменных правил, контрактов и поведения эндпоинтов, а здесь всё три сразу.

Только **фаза 1** трогает миграции и `ModelSnapshot.cs`. Все остальные — нет.

Ф5 и Ф5.1 — два коммита, но **один релиз**: порознь они дают день без самопосадки (риск 2).

---

### Ф1. Схема и одна миграция

**Тесты вперёд**

- `tests/AFK4.Platform.Api.Tests/Migrations/PlatformAccountPoolMigrationTests.cs` — по образцу
  `SeparatePlatformAndOrganizationIdentityMigrationTests`:
  - `[MigrationPostgresFact]` (атрибут с `public const string EnvironmentVariable`, чтобы
    автоматически попасть под охрану `AFK4_REQUIRE_POSTGRES_TESTS`);
  - две записи в разных клубах с одним телефоном получают один `PlatformPersonId`;
  - запись без телефона остаётся с `PlatformPersonId IS NULL` и работает как раньше;
  - телефон, не подходящий под `^\+[0-9]{11,15}$`, **не роняет перенос**: счёт остаётся
    клубным, в `platform_identity_migration_findings` появляется `unusable_phone`;
  - два активных счёта с одним телефоном **в одном клубе**: к личности подшивается один
    (правило в разделе 4), второй остаётся с `NULL` и попадает в находки как
    `duplicate_in_club` — уникальный индекс не нарушается;
  - расхождение имён между клубами пишет находку `name_mismatch` и **не** мешает склейке;
  - ни одна строка `ledger_entries` не изменена и не добавлена;
  - **ни один `PlayerCredentialEntity.PasswordHash` не переехал** в `platform_persons.PinHash`:
    после переноса `PinHash IS NULL` у всех личностей без исключения;
  - `Down` возвращает схему к исходной.
- `tests/AFK4.Platform.Api.Tests/Data/PlatformPersonSchemaTests.cs` — round-trip сущностей,
  уникальность телефона, уникальность пары `(PlatformPersonId, OrganizationId)`.

**Файлы**

- Новые: `Data/PlatformPersonEntity.cs`, `Data/PlatformPersonAccessTokenEntity.cs`,
  `Data/PlatformPersonRefreshTokenEntity.cs`, `Data/PlatformPhoneOtpEntity.cs`,
  `Data/BranchBookingSettingsEntity.cs`, `Data/PlatformReputationSnapshotEntity.cs`.
- Правки: `Data/PlayerAccountEntity.cs` (+`PlatformPersonId`), `Data/ReservationEntity.cs`
  (+`RespondByUtc`, `ConfirmedAtUtc`), `Data/PlatformDbContext.cs` (DbSet-ы и маппинг),
  миграция + снапшот.

**Проверка:** `dotnet build src/AFK4.Platform.Api`; `dotnet test tests/AFK4.Platform.Api.Tests
--filter FullyQualifiedName~Migrations|FullyQualifiedName~PlatformPersonSchema`. Postgres-тесты
обязаны реально бежать: без строки подключения они молча пропустятся, а пропущенный
Postgres-тест здесь равен непроверенной миграции.

---

### Ф2. Личность в токене и `/api/me` — **сделано**

Самая рискованная фаза волны: ошибка здесь разлогинивает всех игроков.

**Решение по совместимости.** Токен выдаётся личности, но несёт необязательный
`PinnedOrganizationId`. Порядок определения «текущего клуба» в
`PlayerAuthenticationMiddleware`:

1. заголовок `X-AFK4-Organization` (новые клиенты);
2. иначе `PinnedOrganizationId` токена (старые клиенты — вход по-прежнему принимает
   `organizationId` и закрепляет его);
3. иначе, если у человека ровно одна связь с клубом — она;
4. иначе клуба нет, и запрос, которому клуб нужен, получает `409 club_not_selected`.

Так `AFK4.Customer.Web` и Flutter-приложение продолжают работать без единой правки.

**Два контекста вместо nullable — принято при реализации, отменяет прежнее решение.**
План предполагал сделать `PlayerContext.PlayerAccountId` необязательным. При столкновении с кодом
это оказалось худшим из двух вариантов: `PlayerAccountId` читается примерно в сорока местах десяти
файлов эндпоинтов, и каждое — место, где ошибка выдаёт чужой кошелёк. Сорок мест, где можно
ошибиться деньгами, — это не рефакторинг, а мина.

Вместо этого контекстов стало два, и они отвечают на разные вопросы:

| | Отвечает на вопрос | Когда есть |
|---|---|---|
| `PlatformPersonContext` | «кто этот человек» | всегда, пока токен личности жив |
| `PlayerContext` | «чей это счёт и чьи это деньги» | только когда клуб определён |

`PlayerContext.PlayerAccountId` **остаётся обязательным**, `PlayerContext` дополнительно несёт
`PlatformPersonId`. Ни один эндпоинт не изменился. Вопрос «в каком клубе» решается один раз в
`PlayerAuthenticationMiddleware`, он же и отвечает `409 club_not_selected` — одно место вместо
сорока. Единственный маршрут, работающий без клуба, — `GET /api/me`: он клубы и перечисляет.

Названный, но чужой клуб получает тот же `409`; нечитаемый заголовок — `400 invalid_organization`.
Подставить вместо названного клуба другой нельзя: это показало бы человеку чужой кошелёк там, где
он ждал свой.

**Тесты (написаны, зелёные)**

- `Identity/PlatformPersonTokenServiceTests` — выдача, одноразовость и ротация refresh, отзыв при
  `IsActive = false` **на самой личности**; отдельно: закрытая клубом карточка не закрывает
  человеку вход в соседние клубы.
- `Identity/PlayerAuthenticationContextTests` — все четыре ветки выбора клуба, `409` на «клубов
  несколько, ни один не назван», `409` на чужой клуб, `400` на нечитаемый заголовок, `401` без токена.
- `Identity/PlayerClientCompatibilityTests` — ходит ровно теми запросами, что настоящий
  Flutter-клиент (`organizationId` в теле входа, дальше только `Authorization`, продление полем
  `refreshToken`), и проверяет, что вход действительно пошёл по новой дороге. Плюс: счёт, ещё не
  подшитый к личности, входит по-старому.
- `Identity/MeEndpointTests` — `GET /api/me` отдаёт личность и список клубов (название, остаток,
  придержано, долг, стаж по каждому), снятый холд перестаёт держаться, чужих клубов в ответе нет
  ни одним полем, PIN не утекает даже в сыром JSON.
- Совместимость: все существующие тесты прошли **без единой правки**.

**Файлы (как получилось)**

- Новые: `Identity/IPlatformPersonTokenService.cs`, `Identity/OpaquePlatformPersonTokenService.cs`,
  `Identity/PlatformPersonContext.cs` (контекст + аксессор), `Identity/IPlayerClubAccountResolver.cs`,
  `Identity/EfPlayerClubAccountResolver.cs`, `Endpoints/MeEndpoints.cs`,
  `Shared.Contracts/Players/MeDto.cs` (`MeDto(MePersonDto Person, IReadOnlyList<MyClubDto> Clubs)`).
- Правки: `Identity/PlayerContext.cs` (+`PlatformPersonId` необязательным параметром — все прежние
  места конструирования не тронуты), `Identity/PlayerAuthenticationMiddleware.cs`,
  `Identity/EfPlayerPhoneSignInService.cs` (вход выдаёт токен личности, если счёт к ней подшит),
  `Endpoints/AuthEndpoints.cs` (продление обслуживает оба вида токенов одним маршрутом),
  `Program.cs` (DI), `Billing/LedgerBalanceProjector.cs` (см. ниже).
- `Identity/OpaquePlayerTokenService.cs` **править не пришлось**: старые токены продолжают
  валидироваться сами, достаточно порядка проверок в middleware.

> **Досрочная находка для Ф7.** Третье число кошелька понадобилось уже здесь — `MyClubDto` без него
> неполон. В `Billing/LedgerBalanceProjector.cs` появился `GetClubBalancesAsync` (остаток,
> придержано, долг) и запись `ClubBalances`; существующая `GetWalletSummaryAsync` переведена на
> него. **Ф7 обязана расширять этот метод, а не заводить второй источник «придержано»** — два
> определения одной суммы разъедутся на первом же исправлении. Смысл `WalletBalance` при этом не
> тронут: холд как был вычтен из остатка, так и остался (риск 4).

**Проверка:** `dotnet test tests/AFK4.Platform.Api.Tests` целиком — фаза трогает
аутентификацию, то есть блок «cross-module» из AGENTS.md.

---

### Ф3. Первое действие открывает счёт — **сделано**

**Правило:** бронь, пополнение или посадка в клубе, где связи ещё нет, создают связь и счёт с
нулём **внутри той же транзакции**, идемпотентно, и склеиваются с карточкой, которую оператор
завёл руками по тому же телефону.

**Тесты вперёд**

- `PlayerClubMembershipServiceTests`:
  - два параллельных вызова с одним `IdempotencyKey` дают одну связь (Postgres-тест на гонку, по
    образцу `Reservations/PostgresBookingCapacityConcurrencyTests.cs`);
  - если в клубе уже есть `PlayerAccount` с этим телефоном и `PlatformPersonId IS NULL` —
    он **подшивается**, а не дублируется, и его деньги остаются на месте;
  - если счёт есть и уже подшит — возвращается он же;
  - счёт создаётся с нулём: ни одной записи журнала не появляется;
  - у нового счёта проставлен признак происхождения «из приложения» (для стойки).
- `MeReservationFirstActionEndpointTests` — бронь в незнакомом клубе проходит и не отвечает
  «игрок не найден».

**Файлы**

- Новые: `Players/IPlayerClubMembershipService.cs`, `Players/EfPlayerClubMembershipService.cs`.
- Правки: `Endpoints/PlayerSelfServiceEndpoints.cs` (бронь, группа, пополнение, старт сессии —
  вместо `accessor.Current.PlayerAccountId` вызов `membership.EnsureAsync(personId, orgId,
  branchId, idempotencyKey, ct)`). `Data/PlayerAccountEntity.cs` уже расширен в Ф1: пометку
  «из приложения» несёт `CreatedFromApp` — она нужна стойке, но игроку не показывается, это
  кухня клуба.

**Как получилось на самом деле**

- Маршрут, на котором счёт может открыться, **помечается явно**:
  `.OpensClubAccount()` (`Identity/PlayerFirstActionRoutes.cs`). Помечены четыре: бронь, групповая
  бронь, намерение пополнить, посадка за ПК. Middleware пропускает такой запрос без счёта, если
  клуб назван; всё остальное по-прежнему получает `409 club_not_selected`. Вариант «заводить счёт
  в middleware для всех подряд» отвергнут: пролистывание чужой витрины завело бы стойке список
  призраков вместо гостей.
- `PlatformPersonContext` получил `SelectedOrganizationId`, а `IPlayerClubAccountResolver` теперь
  возвращает `PlayerClubSelection(OrganizationId, Account)`: клуб без счёта — это нормальное
  состояние, из которого и вырастает первое действие.
- **`IdempotencyKey` в сигнатуре не нужен и не заведён.** Идентичность создаваемого — это сама
  пара `(PlatformPersonId, OrganizationId)`, и её держит уникальный индекс. Отдельный ключ добавил
  бы второй способ сказать то же самое, а вместе с ним и способ сказать это по-разному.
- **Счёт открывается последним шагом перед делом**, после проверки прав клуба, подтверждения
  номера и валидации запроса. Иначе каждая отклонённая попытка оставляла бы клубу карточку гостя,
  который так и не пришёл.
- **Известное ограничение:** у клуба с несколькими филиалами первое действие отвечает
  `branch_required` — кроме посадки за ПК, где филиал берётся из привязки машины. Приложение пока
  не умеет называть филиал при брони и пополнении; закрывается вместе с клиентской работой (Ф10)
  необязательным полем в запросе. Гадать филиал за человека нельзя: он придёт в один, а счёт
  окажется в другом, и в отчётах клуба это будет выглядеть как два разных гостя.
- **Заметно живому оператору:** в списке клиентов появляются карточки людей, которые в клубе
  физически не были, — с признаком `CreatedFromApp`. Это и есть замысел («клуб не заводит
  человека»), но стойке это видно, и вкладка «Гости из приложения» из волны 2 нужна именно за этим.

> Уникальный индекс `(PlatformPersonId, OrganizationId)` из Ф1 — это то, что делает
> идемпотентность настоящей: при гонке вторая вставка падает на индексе и код перечитывает
> существующую связь, ровно как `EfBillingCommandService.ExecuteInTransactionAsync` уже делает
> для денежных команд.

**Проверка:** focused-тесты + Postgres-тест на гонку.

---

### Ф4. Самостоятельная регистрация

Сегодня записи заводит админ (`POST branches/{id}/players`), а вход по SMS работает только для
номера, который кто-то уже внёс. Человек, скачавший приложение дома, упирается в стену.

**PIN при регистрации не спрашивается** (решение владельца). Регистрация — это ровно два поля:
имя и язык, как на экране 06 концепта. PIN задаётся позже и в тот момент, когда он впервые
нужен: при первой посадке за ПК или из профиля. Лишний шаг на входе стоит дороже, чем экран
«задайте PIN» ровно в нужную секунду.

**Тесты вперёд**

- `PlatformRegistrationEndpointTests`:
  - `POST /api/public/register/start` отвечает **побайтово одинаково** для знакомого и
    незнакомого номера (сравнение полных тел ответа, не только статусов);
  - `POST /api/public/register/confirm` с кодом на незнакомый номер создаёт личность и возвращает
    токен; на знакомый — **входит** в существующую, не создавая второй;
  - `POST /api/public/register/profile` (имя, язык) под токеном;
  - лимит: с одного номера больше `MaxSendsPerHour` кодов не уходит **даже если номер
    незнакомый** — сегодня счётчик привязан к `PlayerAccountId`, которого у незнакомца нет;
  - лимит по IP (`player-public`, 10/мин) остаётся.
- `PhoneEnumerationTests` — переборный сценарий: 50 номеров подряд с одного IP, проверяем, что
  различить «есть аккаунт / нет» по ответу нельзя.

**Файлы**

- Новые: `Identity/IPlatformRegistrationService.cs`, `Identity/EfPlatformRegistrationService.cs`,
  `Identity/PhoneOtp/PhoneKeyedOtpStore.cs`, `Endpoints/PlatformRegistrationEndpoints.cs`,
  `Shared.Contracts/Identity/RegistrationContracts.cs`.
- Правки: `Identity/EfPlayerPhoneSignInService.cs` — вход перестаёт требовать
  `organizationId` (параметр становится необязательным «закрепить клуб»), поиск идёт по
  `platform_persons.PhoneNumber`. Свойство «одинаковый ответ» уже реализовано и **сохраняется
  дословно** — единственное изменение в том, что теперь SMS уходит и незнакомцу, что делает
  ответы неотличимыми ещё и по времени, а не только по содержимому.
- `Program.cs`: политика лимитов `register-public`.

**Проверка:** focused-тесты + `dotnet test tests/AFK4.Shared.Contracts.Tests`.

---

### Ф5. PIN принадлежит игроку и работает во всей сети

**Тесты вперёд**

- `MePinEndpointTests`:
  - `PUT /api/me/pin` задаёт PIN (4–8 цифр, только цифры — сегодня валидация это `Length >= 4`
    без проверки на цифры и без верхней границы);
  - смена требует текущий PIN либо свежий SMS-код;
  - `POST /api/me/pin/reset/start` + `/confirm` — восстановление по SMS;
  - сервер PIN **никогда не отдаёт** — контрактный тест на отсутствие поля.
- `PlatformPinVerificationTests`:
  - `POST /api/public/pin/verify` (для игрового ПК, под device-учёткой): проверяет PIN личности,
    возвращает связь с этим клубом и создаёт её, если её нет (переиспользует Ф3);
  - после 5 ошибок — блокировка на 15 минут (`PinFailedCount`, `PinLockedUntilUtc`), ответ
    одинаковый для «нет такого номера», «PIN не задан» и «неверный PIN»;
  - **клубный `PlayerCredentialEntity.PasswordHash` не проверяется никогда** — ни как основной
    путь, ни как запасной. Решение владельца: старые PIN сброшены всем, значит мёртвы с первой
    минуты, а не «пока живут».
- `PcCodeSignInTests` (вход на игровом ПК по коду из SMS — см. Ф5.1):
  - `POST /api/public/pc-login/code/start` под device-учёткой шлёт код и отвечает одинаково
    для знакомого и незнакомого номера;
  - `/confirm` пускает за ПК, создаёт связь с клубом, если её нет, и возвращает признак
    `pinSetRequired`, если сетевой PIN ещё не задан;
  - лимит на номер общий с `platform_phone_otps`, плюс лимит на устройство.
- `OperatorPinRouteRetirementTests`: `POST branches/{id}/players/{id}/pin` возвращает
  `409 { code: "pin_owned_by_player" }` **всегда**, с заголовком `Deprecation`, и пишет аудит
  на попытку.

**Как именно операторский маршрут выводится из обращения**

1. **Волна 1:** маршрут отвечает `409 pin_owned_by_player` при любом вызове. Не «иногда», не «если
   у человека уже есть PIN» — всегда. Клуб не вправе задавать сетевой пароль: иначе админ одного
   клуба получает вход от чужого имени в чужих клубах. Тело ответа содержит человеческую строку,
   потому что установленные в поле версии Organization Admin покажут её тостом.
2. **Совместимость со старыми клиентами.** Organization Admin обновляется в том же релизе (Ф10):
   кнопка «Задать PIN» заменяется строкой «PIN задаёт игрок сам — на ПК по коду из SMS или в
   приложении». Старые установки в поле кнопку сохранят и получат понятный отказ — это хуже, чем
   отсутствие кнопки, но лучше, чем работающая кнопка, меняющая сетевой пароль.
3. **Удаление** маршрута, экрана и чтения `PlayerCredentialEntity.PasswordHash` — закрывающий
   коммит после окончания перехода (Ф5.1); колонка дропается миграцией волны 2.

**Файлы**

- Новые: `Identity/IPlatformPinService.cs`, `Identity/EfPlatformPinService.cs`,
  `Endpoints/PlatformPinEndpoints.cs`, `Shared.Contracts/Identity/PinContracts.cs`.
- Правки: `Endpoints/PlayerSelfServiceEndpoints.cs` (маршруты `/api/me/pin*`),
  `Endpoints/PlayerManagementEndpoints.cs:144` (отказ + `Deprecation`),
  `Identity/PlayerCredentialService.cs` — **попутно чинится настоящий баг**: `SignInAsync` ищет
  по `p.PhoneNumber == request.PhoneNumber` дословно, без `PhoneNumberNormalizer`, в отличие от
  всех OTP-путей. С нормализованными телефонами в `platform_persons` это разъедется.

**Проверка:** focused-тесты + `dotnet test tests/AFK4.Platform.Api.Tests --filter
FullyQualifiedName~Identity|FullyQualifiedName~Pin`.

---

### Ф5.1 Переход: день выката без неработающего входа

Сброс PIN всем — самое чистое решение с точки зрения безопасности, но у него ровно одна цена:
**в день выката ни у кого нет сетевого PIN, и самопосадка за ПК ломается разом для всех**.
Переход спроектирован так, чтобы этой минуты не случилось, и так, чтобы у него был конец.

**Опора: у входа на ПК появляется второй способ — код из SMS.** Он не костыль на месяц, а
постоянная дорога: это же и восстановление забытого PIN. PIN остаётся быстрым способом, SMS —
способом, который работает всегда. Именно поэтому «день тишины» не наступает: с первой минуты
после выката каждый, у кого есть телефон, принимающий SMS, садится за ПК без единого нового
знания.

**Что показывает Player Shell пришедшему со старым PIN.** Сервер отвечает одинаково на любую
неудачу (иначе по экрану ПК можно проверять, у кого есть аккаунт), поэтому объясняет не сервер,
а сама оболочка — и объясняет **всем одинаково**, пока включён платформенный флаг
`pin_migration_notice`:

> **PIN изменился.** Теперь он один на все клубы сети, и задаёте его вы сами.
> **Войти по коду из SMS** · Позвать администратора

После входа по коду — сразу, на том же экране, поле «Придумайте PIN» (4–8 цифр) с кнопкой
«Потом». Это самый дешёвый момент во всём переходе: человек уже за ПК, уже подтверждён, и ему
нужно ровно десять секунд. Приложение при этом не требуется вовсе — что важно для игроков с
кнопочным телефоном.

**Как игрок узнаёт заранее.**

- Пуш за три дня и в день выката тем, у кого есть зарегистрированное устройство
  (`PlayerDeviceEntity` и рассылка уже существуют).
- Экран-перехватчик при открытии приложения: «Задайте PIN — он теперь один на все клубы».
  Пропускаемый, но возвращается, пока PIN не задан.
- Экран на ПК (выше) — главный канал, потому что застаёт человека ровно в момент нужды. На него
  и рассчитываем: пуши и баннеры доходят до меньшинства, а к ПК приходят все.

**Что делает админ у стойки.**

- **Человек с кнопочным телефоном** — вход по коду из SMS на ПК, админ не нужен.
- **Человек без телефона вовсе или с недоступным номером** — админ **сажает его сам**, как
  сегодня: поток «Посадить гостя» существует и ничего в нём не меняется. Это и есть ответ на
  сценарий «часть игроков живёт без смартфона»: PIN нужен только для самопосадки, а не для игры.
  Ни один человек не остаётся без возможности играть, и ни одному не нужен клубный PIN.
- В карточке клиента вместо кнопки «PIN клиента» — строка «PIN задаёт игрок сам: на ПК по коду
  из SMS или в приложении». Оператор PIN не видит и не назначает больше никогда.

**Что закрывается в конце перехода** (а что не закрывается):

| | |
|---|---|
| Закрывается | флаг `pin_migration_notice` и объясняющий баннер в Shell; экран-перехватчик в приложении; маршрут `POST branches/{id}/players/{id}/pin` и экран «PIN клиента»; чтение `PlayerCredentialEntity.PasswordHash` |
| **Не** закрывается | вход на ПК по коду из SMS — он постоянный, это восстановление PIN; посадка гостя оператором — она постоянная |

**Сколько длится и по какому признаку считается законченным.** Окно — 30 дней. Переход закрыт,
когда выполнены **все три** условия:

1. прошло не менее 30 дней с выката;
2. ≥ 90% игроков, у которых была хотя бы одна сессия за последние 30 дней, имеют `PinSetAtUtc`;
3. доля самопосадок через SMS-код упала ниже 20% от всех самопосадок (то есть PIN реально
   пользуются, а не терпят).

Чтобы критерий не был фиктивным, эти два числа выводятся: один показатель
`pin_adoption` добавляется в существующий `Endpoints/PlatformAnalyticsEndpoints.cs`. Мерить
нечем — значит конца у перехода нет, а вечный режим совместимости мы как раз и не хотим.

**Файлы перехода**

- Новые: `Identity/IPcCodeSignInService.cs`, `Identity/EfPcCodeSignInService.cs`,
  `Endpoints/PcSignInEndpoints.cs`, `Shared.Contracts/Identity/PcSignInContracts.cs`.
- Правки: `Shared.Contracts/Platform/Features/PlatformFeatureNames.cs`
  (+`PinMigrationNotice`), `Endpoints/PlatformAnalyticsEndpoints.cs`,
  `src/AFK4.Player.Shell.Web` (экран входа: второй способ, баннер, поле «Придумайте PIN»),
  `src/AFK4.Player.Shell` (проброс), `Notifications/*` (шаблон пуша о смене правил PIN).

**Проверка:** `dotnet test tests/AFK4.Platform.Api.Tests --filter FullyQualifiedName~PcCodeSignIn`
+ `dotnet test tests/AFK4.Player.Shell.Tests`. Плюс ручная проверка на стенде (раздел 9): тест не
докажет, что SMS дошла и что человек понял экран.

---

### Ф6. Настройки приёма гостей у филиала

**Тесты вперёд**

- `BranchBookingSettingsEndpointTests`: `GET/PUT
  /api/organizations/{orgId}/branches/{branchId}/booking-settings`, право
  `ManageBranchSettings`, аудит на изменение, значения по умолчанию для филиала без строки
  (`auto`, 15 мин, предоплата с новых включена, 1 бронь, 3 визита, 20 мин держим место,
  предоплата при неявке **не** удерживается).
- `PlayerBookingRulesEndpointTests`: `GET /api/me/branches/{branchId}/booking-rules` отдаёт
  игроку режим, срок ответа, нужна ли **ему** предоплата (посчитано сервером по его визитам в
  этом филиале), сколько броней ему доступно, сколько держат место. Ни одного поля про других
  игроков.
- `EfReservationAcceptanceModeTests`: режим `off` → отказ `booking_disabled`; `manual` → бронь
  становится `pending` даже при полном кошельке; `auto` → как сегодня; лимит активных броней
  для новичка; порог визитов снимает лимит и предоплату.

**Файлы**

- Новые: `Branches/IBranchBookingSettingsService.cs`,
  `Branches/EfBranchBookingSettingsService.cs`, `Branches/BranchBookingSettingsDefaults.cs`,
  `Shared.Contracts/Branches/BranchBookingSettingsDto.cs`,
  `Shared.Contracts/Branches/UpdateBranchBookingSettingsRequest.cs`,
  `Shared.Contracts/Reservations/PlayerBookingRulesDto.cs`.
- Правки: `Endpoints/BranchSettingsEndpoints.cs`, `Endpoints/PlayerCatalogEndpoints.cs`,
  `Reservations/EfReservationService.cs` — `ShouldAutoConfirmOnlineAsync` заменяется чтением
  настроек филиала (сегодняшнее «кошелёк > 0 и нет долга» становится **поведением по умолчанию
  режима `auto`**, а не законом природы), `Audit/AuditActionNames.cs` (+`UpdateBookingSettings`).

> Флаг `PlatformFeatureNames.OnlineBooking` остаётся тем, чем был, — рубильником платформы
> для всей организации. Настройка филиала — это решение клуба. Смешивать их нельзя: сегодня
> владелец не может выключить брони в одном филиале, а платформа не должна лезть в решение клуба.

**Проверка:** focused-тесты + `dotnet build`.

---

### Ф7. Заморозка — третья сумма кошелька

**Тесты вперёд**

- `LedgerBalanceProjectorHeldTests`: `HeldMinorUnits` = сумма непогашенных `reservation_hold`;
  после снятия холда становится нулём; **`WalletBalance` не меняет смысла** — он как был, так и
  остаётся доступным остатком (это специально: девять мест в коде читают его как «сколько можно
  потратить», и любое изменение смысла — это класс ошибок «списали дважды»).
- `WalletSummaryContractTests` / `PlayerDashboardContractTests`: третье число появилось,
  первые два не поехали.
- `ReservationNoShowRetentionTests`:
  - `KeepPrepaymentOnNoShow = false` → как сегодня, реверс, деньги вернулись;
  - `KeepPrepaymentOnNoShow = true` → реверс холда **плюс** списание
    `reservation_no_show_fee` на ту же сумму, итог по кошельку тот же, но в журнале видно
    «заморозка снята» и «удержано за неявку»;
  - **удержанное — это выручка клуба** (решение владельца): запись несёт `ShiftId` открытой в
    филиале смены и попадает в отчёт по смене отдельной строкой, а не растворяется в
    «заработано временем»;
  - если в момент разбора неявки открытой смены нет, запись пишется с `ShiftId = null` — она
    видна в дневном отчёте филиала и не видна ни в одном отчёте по смене, потому что ни одна
    смена за неё не отвечала (принятое допущение, раздел 7.2);
  - повторный прогон фоновой задачи ничего не дублирует;
  - `HoldSeatAfterStartMinutes` берётся из настроек филиала, а не из зашитых 15 минут.
- `ShiftRevenueNoShowTests` / `ReportEndpointTests`: удержания видны отдельной величиной в
  `ShiftRevenueDto` и в отчёте о выручке; при `KeepPrepaymentOnNoShow = false` величина нулевая.
- `PlayerLedgerFilterTests`: `reservation_hold` и `reservation_no_show_fee` фильтруются
  (сегодня `PlayerLedgerProjector.KnownEntryTypes` **не содержит даже существующий**
  `reservation_hold` — то есть выписку по холдам отфильтровать нельзя, эндпоинт отвечает 400).
- `MoneyActionHighRiskTests`: реверсы холдов **не** попадают в суточный счётчик
  высокорисковых действий — сегодня `MoneyActionEntryTypes.HighRisk` содержит `reversal`, и
  каждая снятая бронь начнёт съедать лимит кассира.

**Файлы**

- Правки: `Billing/LedgerBalanceProjector.cs`, `Shared.Contracts/Billing/WalletSummaryDto.cs`,
  `Shared.Contracts/Players/PlayerDashboardDto.cs`,
  `Shared.Contracts/Billing/LedgerEntryTypeNames.cs` (+`ReservationHold`,
  `ReservationNoShowFee` — заодно `reservation_hold` переезжает из
  `ReservationHold.EntryType` в общий список, где ему и место),
  `Players/PlayerLedgerProjector.cs`, `Reservations/ReservationHold.cs`,
  `Reservations/ReservationNoShowRunner.cs`, `AntiFraud/EfMoneyActionPolicyResolver.cs`,
  `Reservations/EfReservationService.cs:675` (снять протухший комментарий, утверждающий, что
  «the hold/charge lifecycle is a separate deferred feature»),
  `Reports/EfReportService.cs` — **два места**: `BuildShiftRevenue` (строка ~692, где
  `earnedTime` собирается по белому списку из `GameplayCharge` и `PostpaidDebt`) и расчёт
  выручки на строке ~321. Новый тип не добавляется в `earnedTime`: удержание за неявку — это не
  проданное время, и складывать их значит соврать в отчёте. Появляется отдельная величина в
  `Shared.Contracts/Reports/ShiftRevenueDto.cs`.

**Проверка:** `dotnet test tests/AFK4.Platform.Api.Tests --filter
FullyQualifiedName~Billing|FullyQualifiedName~Reservation|FullyQualifiedName~Ledger` +
`dotnet test tests/AFK4.Shared.Contracts.Tests`.

---

### Ф8. Срок ответа на заявку

**Тесты вперёд**

- `ReservationRespondByTests`: при создании `pending`-заявки
  `RespondByUtc = min(now + branch.RespondWithinMinutes, StartsAtUtc)`; при подтверждении
  ставится `ConfirmedAtUtc`, `RespondByUtc` перестаёт значить что-либо; поле доезжает и в
  `ReservationDto`, и в `PlayerReservationDto` — иначе у двух админов на разных машинах будут
  разные цифры, а у игрока третья.
- `ReservationRequestExpiryRunnerTests`: истёкшая заявка снимается сама, холд возвращается
  **всегда** (истечение — это не неявка, клуб не ответил, удерживать не за что), причина
  `request_expired`, повторный прогон идемпотентен, подтверждённые заявки не трогаются.
- `Architecture/PeriodicJobRegistrationTests` — новая задача зарегистрирована и объявлена в
  каталоге интервалов (тест уже существует и упадёт, если забыть).

**Файлы**

- Новые: `Reservations/ReservationRequestExpiryRunner.cs`,
  `Reservations/ReservationRequestExpiryHostedService.cs`.
- Правки: `Reservations/EfReservationService.cs`,
  `Shared.Contracts/Reservations/ReservationDto.cs`,
  `Shared.Contracts/Reservations/PlayerReservationDto.cs`,
  `Platform/Health/PlatformJobNames.cs`, `Platform/Health/PlatformJobIntervalCatalog.cs`,
  `Program.cs`.

**Проверка:** focused-тесты + `dotnet build`.

---

### Ф9. Репутация и приватность

Единственный пункт, который решение про котёл **создаёт**, а не убирает. Без него платформа
работает каналом утечки клиентской базы между конкурентами, и обнаружится это не в тестах.

**Решение владельца:** точное число из суточного снимка. Оператор видит «в сети 14 визитов,
неявок нет», цифра обновляется раз в сутки, и эта задержка и есть защита — не ведро «10–49»,
не живой счётчик.

**Тесты вперёд** (см. раздел 5 — там же обоснование каждого ограничения)

- `ReputationContractTests`: `PlayerReputationDto` содержит **ровно** `NetworkVisits`,
  `NetworkNoShows`, `NetworkBanned`, `CalculatedAtUtc` — и тест перечисляет поля через
  рефлексию, чтобы завтра никто не добавил `LastClubName` «на минутку».
- `ReputationAccessTests`:
  - оператор получает агрегат по человеку, у которого есть **связь с его клубом** или **живая
    заявка** в его филиал;
  - по произвольному `PlatformPersonId` — `404`, неотличимо от несуществующего;
  - поиск по точному номеру разрешён и ограничен по частоте; поиск по префиксу/части номера
    через платформу запрещён (клубный поиск по своим игрокам не меняется);
  - платформенный админ без активного support-доступа агрегат не получает.
- `ReputationAuditTests`: каждый успешный **и** каждый отказанный запрос пишет
  `players.reputation.view` с целью, филиалом, актором и контекстом обращения.
- `ReputationSnapshotTests`: агрегат читается из суточного снимка; два запроса подряд после
  визита человека в соседний клуб дают **одно и то же** число.

**Файлы**

- Новые: `Players/IPlayerReputationService.cs`, `Players/EfPlayerReputationService.cs`,
  `Players/ReputationSnapshotRunner.cs`, `Players/ReputationSnapshotHostedService.cs`,
  `Shared.Contracts/Players/PlayerReputationDto.cs`.
- Правки: `Endpoints/ReservationEndpoints.cs` (агрегат в карточке заявки),
  `Endpoints/PlayerManagementEndpoints.cs` (агрегат в карточке клиента),
  `Audit/AuditActionNames.cs`, `Program.cs`.

Источник чисел на волне 1: визиты — завершённые сессии, привязанные к счетам одной личности;
неявки — брони в состоянии `cancelled` с `CancelReason = "no-show"`. Отдельного состояния
`no_show` пока нет (это пункт 10 волны 2); когда оно появится, меняется один запрос в
`ReputationSnapshotRunner`, а не контракт.

**Проверка:** focused-тесты + `dotnet test tests/AFK4.Shared.Contracts.Tests`.

---

### Ф10. Клиенты не ломаются

Бэкенд-волна, но три клиента читают эти контракты, и «зелёный бэкенд при красном приложении» —
это не готово.

- `src/afk4_customer_app/lib/api/dto.dart` и `player_api_client.dart` — руками (генератора нет):
  `MeDto`, третье число в кошельке, `RespondByUtc`, правила приёма, PIN-маршруты. Плюс
  **экран-перехватчик «задайте PIN»** из Ф5.1 — это единственная экранная работа, которая в
  волну входит, потому что без неё переход не начинается. Тесты:
  `src/afk4_customer_app/test/player_api_client_test.dart` + виджет-тест перехватчика.
  Проверка: `flutter test`, `flutter analyze`.
  **Плюс одна ветка, найденная в Ф2:** у клиента на `401` стоит автоматическая попытка продления,
  а на `409` — нет, поэтому `club_not_selected` для него сейчас тупик с общей ошибкой. Человеку с
  закреплённым клубом он недостижим, но достижим в одном случае: клуб закрыл карточку, пока человек
  был залогинен. Лечится в `player_api_client.dart` одной веткой — `409 club_not_selected` ведёт на
  экран выбора клуба, а не в «что-то пошло не так». На сервере костыля под это нет намеренно.
- `src/AFK4.Customer.Web/src/api/playerApi.ts` + `types.ts` — то же минимальным объёмом.
  Проверка: `bun test`, production-сборка.
- `src/AFK4.Player.Shell.Web` — экран входа за ПК: второй способ («войти по коду из SMS»),
  баннер перехода по флагу, поле «Придумайте PIN» после входа по коду. Тесты на три состояния
  экрана. **Выкатывается тем же релизом, что и Ф5** — см. риск 2.
- `src/AFK4.OrganizationAdmin.Web/src/api/clients/players.ts`, `.../reservations.ts` — агрегат
  репутации, срок ответа, настройки приёма; кнопка «Задать PIN» заменяется строкой-пояснением;
  новая величина удержаний в экране кассы (`ShiftRevenueDto`). Проверка: `bun test`.

Прочие экранные работы (полоса заявок, вкладка «Гости из приложения», профиль игрока) — **не в
этой волне**; здесь только то, без чего клиент падает, врёт или не даёт человеку задать PIN.

---

### Ф11. Перенос существующих данных

Отдельная фаза, потому что это в основном операция, а не код. См. раздел 4.

Кода здесь ровно столько, сколько нужно, чтобы находки не остались в базе без читателя:

- `Endpoints/PlatformIdentityMigrationEndpoints.cs` — `GET /api/platform/identity-migration/findings`
  (фильтр по виду и по «разобрано»), `POST .../{findingId}/resolve`. Право — платформенное,
  доступ аудируется, потому что находка `name_mismatch` содержит два имени одного номера.
- Тест `PlatformIdentityMigrationEndpointTests`: находки видны, чужая организация их не видит,
  разбор идемпотентен.

---

## 4. Миграция существующих данных

Сегодня карточка игрока живёт внутри организации: `player_accounts` уникальны по
`(OrganizationId, ...)`, телефон не уникален нигде, у половины будущих пользователей счёт в клубе
уже есть, и завёл его админ.

### Правила склейки

**Решение владельца: склеиваем всегда.** Кто владеет номером, тот и владеет личностью. Перенос
ничего не блокирует и ничего не решает за человека — спорные случаи он записывает в
`platform_identity_migration_findings`, а разбирают их руками после.

| Ситуация | Что делаем | Почему |
|---|---|---|
| Один телефон в разных клубах | Одна личность, все счета подшиваются к ней | Это и есть модель |
| Имя расходится между клубами | Склеиваем; берём `DisplayName` самого свежего счёта с непустым именем; пишем находку `name_mismatch` | Свежее — вероятнее актуальное. Находка нужна, чтобы увидеть случаи «один номер — два человека» и разобрать их руками |
| **Один телефон дважды в одном клубе** | К личности подшивается счёт с самой свежей сессией (при равенстве — созданный позже); второй остаётся с `PlatformPersonId IS NULL` и попадает в находки как `duplicate_in_club` | Слить два счёта — это движение денег между кошельками, и молча этого не делает никто. Ничего не теряется: оператор видит обе карточки, как и вчера. Цена честная и её надо назвать: **в приложении человек увидит только один из двух счетов, пока клуб не сольёт их руками**. Сколько таких — показывает предполётная проверка |
| Запись без телефона | `PlatformPersonId` остаётся `NULL` | Гость без приложения — нормальный житель системы, а не мусор. Подошьётся, когда подтвердит номер |
| Телефон в нераспознаваемом формате | Ведём себя как с записью без телефона; находка `unusable_phone` | Нормализовать вслепую — значит склеить не тех. Ронять перенос из-за трёх кривых строк — тоже не дело |
| Язык | `PreferredLocale` из того же счёта, что дал имя; `NULL` допустим | |
| Подтверждение номера | `PhoneVerifiedAtUtc = min(...)` по всем клубам, где `PhoneVerified` | Подтверждён однажды — подтверждён |
| **Клубные PIN** | **Не переносится ни один.** У всех личностей `PinHash IS NULL`, все задают PIN заново | Решение владельца. Клубный PIN назначал админ; промотировать его до сетевого — значит выдать админу вход от чужого имени в чужих клубах. Как при этом не оставить людей без входа в день выката — целиком в Ф5.1 |

### Порядок применения

1. **Предполётная проверка на копии продакшена** (только чтение, ничего не меняет) — она не
   блокирует перенос, она сообщает его цену заранее:
   - сколько телефонов встречается более чем в одном клубе (объём склейки);
   - сколько дублей внутри одного клуба (столько людей увидят в приложении не все свои счета);
   - сколько записей без телефона (столько остаётся чисто клубными);
   - сколько ненормализуемых телефонов;
   - у скольких личностей имена в разных клубах расходятся (кандидаты в «один номер — два
     человека»).
   Запросы живут в самом тесте миграции — не отдельным скриптом, который через месяц разъедется
   с реальностью.
2. Показать эти пять чисел владельцу. Если дублей внутри клубов много, разумно дать клубам
   неделю на слияние **до** переноса — но это ускорение, а не условие: перенос пройдёт в любом
   случае.
3. Снимок базы (`pg_dump`) непосредственно перед применением.
4. Применение на staging: `dotnet run --project src/AFK4.Platform.Api -- --migrate` (флаг
   поддерживается, `Program.cs:461`). Прогон проверочного списка ниже.
5. Применение на проде тем же способом, в окно низкой нагрузки.
6. Разбор находок руками: `GET /api/platform/identity-migration/findings` (Platform Control,
   Ф11). Пока находка не разобрана, ничего не ломается — она просто ждёт.

### Проверка после применения

- число личностей = число различных нормализованных телефонов среди активных счетов;
- каждый счёт с распознанным телефоном либо подшит, либо имеет находку `duplicate_in_club` —
  третьего не дано;
- сумма по `ledger_entries` до и после **побитово одинакова** (миграция денег не касается — это
  главный инвариант);
- уникальный индекс `(PlatformPersonId, OrganizationId)` создан и не нарушен;
- `SELECT count(*) FROM platform_persons WHERE "PinHash" IS NOT NULL` = **0**;
- контрольная выборка: три человека с двумя клубами входят по SMS и видят оба клуба;
- контрольная выборка: один человек садится за ПК по коду из SMS и задаёт PIN на месте
  (проверка Ф5.1 на живом стенде, а не в тесте).

### Откат

Формулировка «неотменяемая операция» относится к последствиям, не к схеме. **`Up` ничего не
удаляет и ничего не переписывает в существующих таблицах** — только добавляет столбец и таблицы.
Поэтому:

- **до выдачи первого токена личности** откат — это `Down` миграции; данные клубов нетронуты;
- **после** — откатывать нужно снимком: выданные токены, заданные сетевые PIN и созданные с нуля
  связи в `Down` не воспроизводятся. Окно принятия решения — сутки после применения, дальше
  снимок устаревает по деньгам.

Настоящая необратимость одна и она не техническая: **если два разных человека делят один номер,
после склейки тот, у кого SIM, видит клубы обоих.** Владелец принял этот риск сознательно — кто
владеет номером, тот и владеет личностью, тем более что вход и так по SMS. Схемой это не
чинится, поэтому единственная защита — находки `name_mismatch` и живой человек, который их
разберёт. Разбор такого случая (расцепить личность и счёт, завести вторую личность на другой
номер) делается руками через Platform Control и в волну 1 автоматизации не получает.

---

## 5. Безопасность и приватность

### Где именно стоит ограничение на репутацию

Не в интерфейсе. Три рубежа:

1. **В контракте.** `PlayerReputationDto` физически не имеет полей под названия клубов, даты и
   суммы, и это закреплено рефлексивным тестом. «Скрыто в UI» — не защита: операторское
   приложение ходит в тот же API, что и curl.
2. **В сервисе.** `EfPlayerReputationService` отдаёт агрегат только тому клубу, у которого есть
   основание: существующая связь с этим человеком либо живая заявка в его филиал. Иначе `404`,
   неотличимый от «нет такого человека».
3. **В снимке.** Агрегат читается из `platform_reputation_snapshots`, пересчитываемого раз в
   сутки. Это и есть защита от вычисления соседа: клуб, опрашивающий счётчик каждую минуту,
   иначе увидит `+1` ровно в тот момент, когда человек сел за ПК напротив, — и узнает, где он
   играет, не получив ни одного названия клуба. Суточная гранулярность делает такую корреляцию
   бессмысленной, а точность цифры («14 визитов») сохраняет.

### Что пишется в аудит

Действие `players.reputation.view` через существующий `IAuditRecordWriter`:
актор (сотрудник), организация, филиал, `TargetType = "platform_person"`, `TargetId`, исход
(`success` / `denied`), и в `DetailsJson` — контекст обращения (идентификатор заявки либо факт
поиска по точному номеру). Отказы пишутся тоже: перебор виден только по отказам.

Аудит пишется на **сам факт запроса**, а не на изменение. Это отличается от остального аудита в
проекте и сделано намеренно: утечка здесь происходит через чтение.

### Какие ответы обязаны быть неразличимы

| Пара | Обязаны совпадать |
|---|---|
| «Код на знакомый номер» / «на незнакомый» | Тело, статус **и** факт отправки SMS. Свойство уже есть в `EfPlayerPhoneSignInService.StartAsync` и обязано пережить переход на регистрацию. Сейчас оно ещё и улучшается: раньше незнакомцу SMS не уходила, и разницу было видно по времени ответа |
| «Кулдаун активен» / «лимит исчерпан» / «SMS не ушла» на публичном входе | Всё то же `Sent`. Настоящие статусы отдаёт только `EfPlayerPhoneVerificationService`, где вызывающий уже аутентифицирован — ему скрывать нечего |
| «Неверный PIN» / «PIN не задан» / «нет такого номера» на `POST /api/public/pin/verify` | Один отказ, один статус. Именно поэтому объяснение про смену правил PIN даёт оболочка, а не сервер, и даёт его **всем одинаково** по платформенному флагу, а не по конкретному человеку (Ф5.1) |
| «Код на ПК знакомому номеру» / «незнакомому» | Как и на публичном входе: тело, статус и факт отправки совпадают |
| «Нет такой личности» / «нет права смотреть репутацию» | `404` в обоих случаях |
| Кривой номер | **Может** отличаться (`400 invalid_phone`): номер, который не может принадлежать никому, ничего не выдаёт |

### Как не дать перебирать номера

- IP-лимит `player-public` 10/мин уже есть; для регистрации добавляется отдельная политика.
- **Лимит по номеру, а не по аккаунту.** Сегодня `MaxSendsPerHour = 5` считается по
  `PlayerAccountId`, которого у незнакомого номера нет, — то есть после включения регистрации
  незнакомый номер получил бы неограниченную рассылку. Счётчик переезжает в
  `platform_phone_otps`, ключ — нормализованный телефон. Это одновременно защита от перебора и
  от SMS-бомбинга за счёт клуба.
- Поиск людей через платформу — **только точное совпадение нормализованного номера**, с лимитом
  и аудитом. Поиск по части номера остаётся тем, чем был: поиском по своим игрокам внутри клуба.
- Публичный каталог клубов (`GET /api/public/organizations`) не получает ни одного поля про
  людей.

### Что ещё нельзя сломать по дороге

- Клубный админ **не может** задать или сменить сетевой PIN (Ф5, маршрут отвечает `409` всегда) и
  не может снимать сетевой запрет. Попытка пишется в аудит.
- Вход на ПК по коду из SMS ходит под device-учёткой и шлёт SMS — значит это ещё и способ
  потратить чужие деньги на рассылку. Лимит двойной: на номер (общий счётчик
  `platform_phone_otps`) и на устройство.
- `/api/me` не суммирует деньги по клубам ни в одном поле: `LedgerBalanceProjector` падает на
  смешанных валютах, и это правильно — общего кошелька нет, суммировать нечего.
- Токен личности отзывается при `IsActive = false` на самой личности, а не только на счёте.

---

## 6. Что можно параллелить после вливания волны 1

**Жёсткое ограничение, не зависящее от логики:** одна миграция — одна ветка. `ModelSnapshot.cs`
конфликтует всегда. Поэтому первым шагом волны 2 делается **одна общая схемная миграция**
(`AddBookingTruthfulness`: состояние `no_show` и удержанная сумма, код причины отказа,
происхождение сессии), и только после её вливания расходятся ветки.

После этого параллельно, разными агентами, без пересечения по файлам:

| Поток | Пункты волны 2 | Основные файлы |
|---|---|---|
| **A. Цена в приложении** | 08 (тариф, цена часа, зона в активной сессии), 17 (докуда действует тариф) | `Players/PlayerDashboardProjector.cs`, `Shared.Contracts/Players/ActiveSessionDto.cs`, `Tariffs/*` |
| **B. Код ПК на мониторе** | 09 | `Devices/*`, `Player.Shell*`, новый эндпоинт обмена кода |
| **C. Выписка игроку** | 14 (`GET /api/me/wallet/ledger`) | `Players/PlayerLedgerProjector.cs`, `Endpoints/PlayerSelfServiceEndpoints.cs` |
| **D. Реалтайм по броням** | 12 | существующий SignalR-хаб, `Reservations/EfReservationService.cs` (только точки публикации событий) |

**Обязаны идти одним потоком, последовательно:** пункты 10 (состояние «не приехал»), 11 (отказ с
причиной) и 13 (происхождение сессии) — все трое переписывают одну и ту же машину состояний в
`EfReservationService.cs` / `EfReservationSessionCoordinator.cs`. Три ветки в этом файле — это
гарантированный тройной конфликт и, что хуже, три разных представления о том, чем отличается
отмена от неявки.

**Обязаны ждать волну 1 целиком:** 16 (сетевой запрет против локального — нужна личность),
20 (свободные места в витрине — нужна публичная витрина с настройками приёма из Ф6),
22 (поиск людей в палитре — нужны правила доступа к репутации из Ф9).

Поток D пересекается с последовательным потоком по одному файлу — планировать его либо до, либо
после, но не одновременно.

---

## 7. Решения и допущения

### 7.1 Решения владельца (согласовано 2026-08-18)

1. **PIN сбрасывается всем.** Ни один клубный PIN не переносится — в том числе в однозначном
   случае «один клуб, один хеш». Все задают PIN заново. Как пережить день выката — Ф5.1.
2. **PIN при регистрации не спрашивается.** Регистрация — имя и язык; PIN задаётся при первой
   посадке за ПК или из профиля.
3. **Склейка по телефону — всегда.** Кто владеет номером, тот владеет личностью. Спорные случаи
   не блокируют перенос, а попадают в находки и разбираются руками после.
4. **Репутация — точное число из суточного снимка.** Задержка и есть защита от вычисления
   соседа по динамике счётчика.
5. **Удержанная при неявке предоплата — выручка клуба**, с проводкой в кассовую смену и
   отдельной строкой в отчётах.

### 7.2 Допущения, которые я принял сам

Владелец их не подтверждал. Каждое — обратимое и дешёвое в отмене; если какое-то не нравится,
менять надо до начала фазы, где оно закреплено.

| # | Допущение | Почему так | Что переделывать при отказе |
|---|---|---|---|
| 1 | Дубли одного телефона **внутри одного клуба** не сливаются автоматически: подшивается счёт с самой свежей сессией, второй остаётся клубным и попадает в находки | Слияние двух счетов — это движение денег между кошельками; автоматика здесь ошибается молча и необратимо | Только SQL внутри миграции (Ф1) |
| 2 | Репутацию вправе спросить клуб, у которого есть **связь с человеком или живая заявка** в его филиал; сверх этого — только точное совпадение номера | Иначе оператор пробивает любой номер и платформа становится справочником «кто где играет» | Одно правило в `EfPlayerReputationService` (Ф9) |
| 3 | «Предоплата только с новых» понимается буквально: завсегдатай бронирует без заморозки, и его неявка не стоит ему денег | Так написано на экране 06 концепта. Но это значит, что неявка своего бесплатна — а именно свои бронируют чаще всего | Настройка филиала (Ф6) + ветка в холде (Ф7) |
| 4 | Имя личности принадлежит человеку: `platform_persons.DisplayName` — единственное имя, которое видит оператор. `player_accounts.DisplayName` остаётся, но становится **клубной пометкой** («Фаррух с PS5»), а не вторым именем | Иначе админ переименовывает человека в чужих клубах, и это ровно та утечка контроля, ради которой всё разводится по слоям | `PATCH players` и проекции оператора (Ф2, Ф10) |
| 5 | Смена номера человеком не двигает клубные счета: связи держатся на `PlatformPersonId`, а не на телефоне | Иначе человек с новой SIM теряет деньги во всех клубах. Сам экран смены номера — вне волны 1 | Ничего: это следствие модели |
| 6 | Удержание за неявку вне открытой смены пишется с `ShiftId = null` — видно в дневном отчёте филиала, не видно ни в одном отчёте по смене | Ни одна смена за него не отвечала. Альтернатива — держать деньги замороженными до открытия клуба — хуже для игрока | Правило в `ReservationNoShowRunner` (Ф7) |
| 7 | Истечение срока ответа на заявку **всегда** возвращает деньги, даже при включённом удержании | Клуб не ответил — удерживать не за что. Удержание платит за неявку игрока, а не за молчание клуба | Ветка в `ReservationRequestExpiryRunner` (Ф8) |
| 8 | PIN — 4–8 цифр, только цифры | Сегодня валидация это `Length >= 4` без верхней границы и без проверки на цифры, то есть «PIN» мог быть паролем | Валидация в `EfPlatformPinService` (Ф5) |
| 9 | Окно перехода по PIN — 30 дней, порог 90% / 20% | Числа выбраны как разумные, а не выведены из данных. Смотреть по факту на показателе `pin_adoption` | Константы + флаг (Ф5.1) |
| 10 | Неявки для агрегата репутации считаются как `cancelled` + `CancelReason = "no-show"`, пока волна 2 не заведёт настоящее состояние `no_show` | Числа честные уже сейчас, а контракт от появления состояния не меняется | Один запрос в `ReputationSnapshotRunner` (Ф9) |


## 8. Самое рискованное

1. **Переход токена на личность (Ф2). — Закрыт.** Ошибка разлогинивала бы всех игроков разом.
   Защита сработала: закреплённый клуб в токене, старые токены валидны до истечения, существующие
   тесты входа прошли без единой правки.
   Отдельно отмечу главное: **вторая половина риска — «править сорок эндпоинтов, читающих
   `PlayerAccountId`» — закрыта не аккуратностью, а архитектурно.** Разделение на два контекста
   (`PlatformPersonContext` — кто человек, `PlayerContext` — чей счёт) убрало саму необходимость
   их трогать: сорока мест, где можно ошибиться деньгами, просто не появилось. Аккуратность
   масштабируется плохо, отсутствие правок — идеально.
2. **День выката PIN (Ф5.1).** Второй по величине риск после токена и единственный, который
   создан принятым решением: сброшены все PIN разом. Если вход на ПК по коду из SMS
   не доедет в том же релизе, самопосадка ломается для всех и клубы вернутся к ручной посадке
   каждого гостя. Правило: **Ф5 и Ф5.1 вливаются одним релизом, порознь не выкатываются
   никогда.**
3. **Склейка по телефону (Ф11).** Необратима не схемой, а последствиями: человек, делящий
   номер с другим, увидит чужие клубы. Владелец риск принял. Защита: находки `name_mismatch`,
   снимок базы, окно отката в сутки. Дубли внутри клуба перенос больше не блокируют — цена в
   том, что до ручного слияния человек видит в приложении один счёт из двух.
4. **Смысл `WalletBalance`.** Соблазн сделать его «валовым» и вычитать холд отдельно
   огромен и ломает девять мест, включая единственную настоящую проверку достаточности средств
   в `EfWalletSettlementService.DebitAsync` (которая считает ещё и застейдженные записи
   `ChangeTracker`). Правило волны: **`WalletBalance` не меняет смысла ни при каких
   обстоятельствах**, третье число только добавляется.
5. **Реверсы холдов в счётчике антифрода.** `MoneyActionEntryTypes.HighRisk` содержит
   `reversal`; каждая снятая бронь начнёт съедать суточный лимит кассира, и в час ночи
   оператор не сможет сделать возврат. Чинится одной строкой, но только если про неё вспомнить.
6. **Молча пропущенный Postgres-тест.** Миграция и гонки проверяются только на настоящем
   Postgres. Локальный прогон без строки подключения зелёный и ничего не доказывает; в CI это
   ловит `AFK4_REQUIRE_POSTGRES_TESTS=1`, но до CI надо дожить с правильным атрибутом
   (`public const string EnvironmentVariable` — иначе тест не попадёт под охрану).
7. **Одна миграция на всю волну означает, что фазу 1 нельзя делать частично.** Забытая колонка
   стоит второй миграции и конфликта снапшота. Список из девяти позиций в разделе 2 —
   исчерпывающий; если по ходу фаз 2–9 понадобилась десятая, это ошибка проектирования схемы, и
   лучше остановиться и дособрать фазу 1, чем завести вторую миграцию.
8. **Отчёт по выручке.** `earnedTime` в `BuildShiftRevenue` собирается по белому списку типов
   записей. Новый `reservation_no_show_fee` туда **не** добавляется, но и не должен потеряться —
   если забыть отдельную величину, удержания станут деньгами, которые есть в журнале и которых
   нет ни в одном отчёте.

---

## 9. Как проверять

- Каждая фаза: `dotnet build src/AFK4.Platform.Api` + focused-тесты фазы.
- Фазы 1, 2, 5, 7 (миграция, аутентификация, PIN, деньги) — вся `tests/AFK4.Platform.Api.Tests`:
  требование AGENTS.md для миграций, auth-границ и денежных границ.
- Фазы, меняющие контракты (2, 4, 5, 5.1, 6, 7, 8, 9): плюс `tests/AFK4.Shared.Contracts.Tests`.
- Фаза 5.1 дополнительно: `dotnet test tests/AFK4.Player.Shell.Tests` — экран входа за ПК живёт
  там, и «баннер показывается всем одинаково» проверяется тестом, а не глазами.
- Фаза 7 дополнительно: `--filter FullyQualifiedName~Report` — удержания обязаны появиться в
  отчёте по смене, иначе они деньги-призраки.
- Фаза 10: `flutter test` + `flutter analyze` в `src/afk4_customer_app`, `bun test` в
  `AFK4.Customer.Web`, `AFK4.Player.Shell.Web` и `AFK4.OrganizationAdmin.Web`.
- Перед вливанием волны: полная сборка решения и полный прогон тестов — затронуты миграции,
  общие контракты и границы аутентификации.
- Postgres-тесты (миграция и склейка, гонка при создании связи, гонка при брони) должны
  **реально выполниться**. Пропуск = провал, не «ну на CI прогонится».

**Проверки, которых тестами не бывает** — их делает человек на стенде перед выкатом на прод:

1. вход за ПК по коду из SMS и установка PIN на месте (Ф5.1) — на настоящей SMS, а не на моке;
2. посадка гостя оператором для человека без телефона — путь не сломан;
3. человек с двумя клубами видит оба после переноса;
4. отказ `409` от старого Organization Admin на кнопке «Задать PIN» выглядит как понятный текст,
   а не как «Ошибка 409».

**Порядок релиза.** Ф5 и Ф5.1 (сервер, Shell, приложение) вливаются и выкатываются одним
релизом. Разделить их — значит устроить день без самопосадки во всех клубах сети.

Ожидаемое суммарное покрытие новыми тестами: порядка 75–85 тестов, из них 3–4 на настоящем
Postgres.
