# Полное удаление owner-code

**Дата:** 2026-06-13
**Статус:** дизайн утверждён, готов к плану
**Ветка:** `chore/remove-owner-code` (поверх `feature/setup-wizard-design-polish`, т.к. оба меняют `PreviewSetupWizard.cs` + install-контракты; визард-PR #77 мёржится первым, затем эта ветка ребейзится на main).

## Проблема

Owner-code — это короткий код, который владелец генерил в админке, чтобы привязать устройство к клубу без логина сотрудника. Логин по owner-code из визарда уже убран (PR #64/#75): визард теперь enroll-ит через authed-путь (`/api/install/auth/*`, токен сотрудника). Но вся инфраструктура owner-code осталась живой: backend (entity, service, endpoints, unauth install-путь), platform-admin экран, .NET host-bridge (мёртвые хендлеры), i18n, тесты, таблица БД. Это лишний код и второй (ненужный) способ enroll.

Решение: **удалить owner-code целиком.** Единственный способ enroll остаётся authed (логин сотрудника), который визард уже использует — удаление не ломает текущий флоу.

## Объём

### Что остаётся (НЕ трогаем)
- Authed install-путь `/api/install/auth/{discover,seats,enroll}` и `AuthenticatedInstall*Request`/`*Response` контракты.
- Общий каркас `EfInstallService` / `DeviceEndpoints` (вырезаем только owner-code ветки).
- `InstallScreen` в platform-admin как экран (MSI-загрузка, список филиалов) — убираем из него только owner-code-панель и правим тексты инструкций.
- Authed-хендлеры host-bridge (`wizard:discoverAuth`/`createSeatAuth`/`enrollAuth`).
- Существующие enrolled-устройства продолжают работать (их аутентификация — по device credential, не по owner-code).

### Что удаляем (owner-code-специфичное)

**1. Backend core**
- `src/AFK4.Platform.Api/Identity/OwnerCodes/*` целиком: `IOwnerCodeService`, `OwnerCodeService`, `IOwnerCodeGenerator`+`RandomOwnerCodeGenerator`, `IOwnerCodeHasher`+`Sha256OwnerCodeHasher`, `OwnerCodeOptions`, `OwnerCodeOperationResult` (+ `OwnerCodeIssued`/`OwnerCodeSummary`/`OwnerCodeLookupResult`/`OwnerCodeLookupStatus`).
- `src/AFK4.Platform.Api/Data/OwnerCodeEntity.cs`.
- `src/AFK4.Platform.Api/Endpoints/OwnerCodeEndpoints.cs` + вызов `app.MapOwnerCodeEndpoints()` в `Program.cs` + DI-регистрации (`OwnerCodeOptions`, generator, hasher, service) в `Program.cs`.
- Shared.Contracts: `Identity/OwnerCodeIssuedResponse.cs`, `Identity/OwnerCodeSummaryResponse.cs`.

**2. Unauth install-путь**
- Эндпоинты `POST /api/install/discover`, `/api/install/enroll`, `/api/install/seats` в `DeviceEndpoints.cs`.
- Owner-code методы в `EfInstallService.cs`: `DiscoverAsync`, `EnrollAsync`, `CreateSeatAsync`, `RecordResolvedOwnerCodeFailureAsync`, инъекция `IOwnerCodeService`. Authed-методы (`*AuthenticatedAsync`/`EnrollResolvedAsync`) остаются, но из `EnrollResolvedAsync`/`CreateSeatResolvedAsync` убираем параметр/присваивание `enrolledViaOwnerCodeId`.
- Контракты `Install/InstallDiscoverRequest.cs`, `InstallEnrollRequest.cs`, `InstallCreateSeatRequest.cs` (unauth-варианты с `OwnerCode`).
- Интерфейс install-сервиса: убрать методы, которых больше нет (сверить `IInstallService`).

**3. БД — миграция (drop всё)**
- Убрать `DeviceEntity.EnrolledViaOwnerCodeId` (свойство) + конфиг индекса в `PlatformDbContext` (строка ~334).
- Новая EF-миграция: `DROP` колонки `devices.EnrolledViaOwnerCodeId` + её индекса `IX_devices_EnrolledViaOwnerCodeId`, и `DROP TABLE owner_codes`. (Старые миграции `AddOwnerCodes`/`AddInstallDeviceEnrollmentFields`/`Make…Unique`/`Harden…` НЕ трогаем — это история; откат делает новая миграция.)
- Обновить `PlatformDbContextModelSnapshot`.

**4. Permissions / audit**
- `StaffPermissionNames.ManageOwnerCode` (`identity.owner_code.manage`) — удалить из каталога; вычистить из любых seed-наборов ролей и их тестов (паритет permission-каталога).
- Audit-имена `GenerateOwnerCode`/`RotateOwnerCode` (`install.owner_code.generated/rotated`) — удалить.

**5. .NET wizard host**
- В `SetupWizardWebHostBridge.cs`: мёртвые хендлеры `wizard:discover`/`wizard:createSeat`/`wizard:enroll` (owner-code-варианты), приватный `ValidateOwnerCode`, `OwnerCode`-поля в payload-record'ах (`WizardDiscoverPayload`/`WizardCreateSeatPayload`/`WizardEnrollPayload`).
- `ISetupWizardApiClient` (SetupWizard.Core): убрать owner-code методы (`DiscoverAsync(string ownerCode)`, `CreateSeatAsync(string ownerCode,…)`, `EnrollAsync(InstallEnrollRequest)`), оставить authed-варианты. Обновить реальную реализацию клиента и `PreviewSetupWizard.FakeApiClient` соответственно (preview: убрать owner-code методы, authed-методы больше не делегируют через owner-code-перегрузку).

**6. Platform-admin web**
- Удалить: `club/install/OwnerCodePanel.tsx`, `club/install/useOwnerCode.ts`, `api/clients/ownerCode.ts`, типы `OwnerCodeSummary`/`OwnerCodeIssued` в `api/types.ts`, owner-code методы в `clubApi.ts`, `OwnerCodeView`/`toOwnerCodeView` в `installModel.ts`.
- `InstallScreen.tsx`: убрать `<OwnerCodePanel/>` и переписать шаги инструкции под «откройте визард → войдите логином сотрудника» (без упоминания кода). Оставить MSI-загрузку (`getSetupMsiUrl`) и список филиалов.

**7. i18n**
- Удалить ключи `install.ownerCode.*` (~12) из `locales/{ru,en,tg}.json`.
- Переписать `install.subtitle` (сейчас «Use the owner code in the Windows setup wizard.») под новый флоу (вход логином сотрудника) — во всех трёх локалях; tg — реальный перевод, помечен на ревью носителю.
- `bun run gen`.

**8. Тесты**
- Удалить: `tests/AFK4.Platform.Api.Tests/OwnerCodeEndpointTests.cs`; `src/AFK4.Platform.Web/src/club/install/OwnerCodePanel.test.tsx`, `useOwnerCode.test.ts`; owner-code кейс в `App.test.tsx` (+ моки owner-code endpoint'а).
- `InstallEndpointTests.cs` (тесты unauth owner-code install) — удалить; authed-путь уже покрыт `AuthenticatedInstallEndpointTests.cs`. Если там есть проверки не-owner-code поведения — перенести на authed-путь.
- Контракт-тесты удалённых DTO (`InstallContractSerializationTests` и пр.) — убрать соответствующие кейсы.

## Порядок исполнения

Чтобы компилировалось/проходило на каждом шаге, удаляем «листья → корень»:

1. **Frontend (platform-admin):** удалить owner-code UI/клиент/типы/хук, поправить `InstallScreen` + i18n + gen. (Независимо от backend, web-тесты зелёные.)
2. **Wizard host (.NET):** убрать owner-code из `ISetupWizardApiClient` + реализации + bridge + preview. (Зависит от удаления unauth-контрактов — делаем вместе с шагом 3 или сразу после, т.к. `InstallEnrollRequest` уходит.)
3. **Backend endpoints + service:** убрать unauth install-эндпоинты, owner-code методы `EfInstallService`, `OwnerCodeEndpoints` + маппинг + DI.
4. **Backend core + contracts:** удалить `Identity/OwnerCodes/*`, `OwnerCodeEntity`, owner-code DTO, unauth install-контракты, permission/audit-имена.
5. **БД:** убрать свойство+индекс, сгенерить drop-миграцию, обновить snapshot.
6. **Тесты:** удалить/перенести owner-code тесты.
7. **Финал:** `dotnet build` (Api + SetupWizard), `dotnet test` Platform.Api + SetupWizard + Operator.App (если затронут), web `bun test` + build (platform-web), i18n `bun test`.

(Точную атомарную разбивку на коммиты определит план — шаги 2–4 тесно связаны через контракты, их вероятно придётся делать одним заходом, чтобы солюшн компилировался.)

## Риски (явно)

- **Прод-данные:** drop-миграция удаляет таблицу `owner_codes` и колонку `devices.EnrolledViaOwnerCodeId` — необратимо. Подтверждено пользователем. Существующие устройства не ломаются (их auth — device credential).
- **Permission-каталог:** удаление `ManageOwnerCode` должно синхронизироваться с seed-ролями и их тестами (иначе паритет-тест каталога/ролей упадёт). Проверить seed.
- **Единственный enroll-путь:** после удаления unauth `/api/install/*` остаётся только authed. Визард это уже использует; других потребителей unauth-эндпоинтов быть не должно — проверить при реализации (нет ли скриптов/доков, зовущих их).
- **Стек поверх визард-ветки:** PR этой ветки будет показывать визард-коммиты, пока #77 не вмёржен; после мержа — ребейз на main для чистого независимого diff.

## Вне области

- Изменения authed install-пути и общего install-каркаса (кроме вырезания owner-code ветвей).
- Любой рефактор install-сервиса сверх удаления owner-code.
- Изменения визарда сверх host-bridge cleanup.
