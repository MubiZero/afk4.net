# Оператор = единая админка — Фундамент (под-проект №1): единый HTTP-транспорт + мульти-филиальность — дизайн

**Дата:** 2026-07-22
**Эпик:** «Оператор = единая админка» (тело эпика). Первый кирпич эпика — рефокус Клуб + Платежи/Лояльность — уже сделан.
**Статус:** дизайн принят пользователем → writing-plans.
**Предшествующие решения:** память `operator-as-unified-admin-epic`, `operator-club-payments-rethink`, `afk4-operator-app-webview2`, `operator-wizard-auth-phone-first`.
**Чувствительность:** трогаем фундамент задеплоенного (пока только на demo/staging — prod afk4 НЕ задеплоен) десктоп-оператора: аутентификацию и хостинг. Money-path не трогаем.

---

## Goal

Сделать так, чтобы оператор-веб (`AFK4.Operator.App.Web`) работал **идентично** в двух средах:
десктоп-хост WebView2 (WPF) и **обычный браузер**. Владелец сети (owner) заходит с любого
устройства, видит все свои филиалы и переключается между ними; кассир/локальный админ заперт на
своём филиале. Разделы гейтятся ролью (механизм уже есть).

Это разблокирует три вещи эпика: (1) owner админит со своего ноутбука/телефона, а не только с ПК
клуба; (2) будущая мобильная обёртка (тот же web-app в мобильном хосте); (3) последующий снос
owner-панели Platform.Web/club, функции которой оператор поглощает.

**Этот под-проект строит только фундамент** — транспорт + мульти-филиальный контекст + ролевой
гейт свитчера. Новые org-level экраны (Branches / Billing / Install / Journal) и слияние
дублирующих branch-экранов — отдельные последующие под-проекты.

## Контекст: что уже готово (важно — фундамент ближе, чем кажется)

Из инвентаризации кодовой базы (2026-07-22):

- **Сессия оператора уже мульти-филиальная.** Тот же opaque staff-токен, что у Platform.Web,
  отдаёт оператору `branchIds: string[]`, `permissions: string[]`, `roleNames`, `activeBranchId?`
  (`src/AFK4.Operator.App.Web/src/authClient.ts:3` — `OperatorAuthSession`). Бэкенд:
  `OpaqueStaffTokenService.cs:137` (`BranchIds` агрегируется из ролей `:160`, `Permissions` —
  плоский union по ролям `:161` через `PermissionCatalog.GetPermissions(roleNames)`),
  `StaffContext.cs`.
- **Все data-запросы уже идут напрямую по HTTP** в Platform.Api, минуя WPF-мост.
  `PlatformApiClient` (`src/AFK4.Operator.App.Web/src/platformApi.ts`) — обычный `fetch` +
  `Authorization: Bearer {accessToken}` в `platformBaseUrl`. Все клиенты параметризованы по
  `branchId` (`operatorApiClients.ts`, пути `/api/branches/{branchId}/...`).
- **WPF-мост (`hostBridge.ts` ↔ `OperatorWebHostBridge.cs`) нужен только для двух вещей:**
  (a) **auth** — `authClient.ts` дёргает `postHostRequest('auth:signIn'|'loadToken'|'refresh'|'signOut')`;
  хост делает sign-in server-side и хранит токен через ProtectedData (DPAPI);
  (b) **connection/device-пиннинг** — `useOperatorConnection.ts` (`connection:*`), снапшот
  «эта машина = филиал/сиденье» через `ProtectedDataOperatorConnectionStore`.
- **Ролевой гейт разделов уже работает:** `permissionNames.ts` (~55 прав),
  `operatorPermissions.ts` (`hasPermission`, `workspacePermissionRules`, `canOpenWorkspace`,
  `firstAllowedWorkspace`), используется в `App.tsx` (редирект на первый разрешённый воркспейс,
  фильтр nav-секций).
- **Свитчера филиала в операторе НЕТ.** `resolveActiveBranchId` (`operatorHelpers.ts:744`) один раз
  вычисляет `session.activeBranchId ?? config.branchId ?? session.branchIds[0]` и кладёт в
  `backendContext.branchId` (`App.tsx:105-107`); сменить в рантайме нечем.
- **Готовый эталон свитчера в Platform.Web:** компонент
  `src/AFK4.Platform.Web/src/components/shell/BranchSwitcher.tsx` (props `branches`,
  `activeBranchId`, `onSelect`) + хук `src/AFK4.Platform.Web/src/club/branches/useActiveBranch.ts`
  (хранит активный branch в localStorage `afk4.club.activeBranchId`, валидирует против списка,
  дефолт `branchIds[0]`, `select(branchId)`). Переиспользуемо почти как есть.

## Принятые решения (brainstorming 2026-07-22)

1. **Объём под-проекта №1:** свитчер + браузерный транспорт **вместе** (не по отдельности).
2. **Транспорт — Approach B (унификация, а не дублирование):** убрать **auth** из моста;
   и WebView2, и браузер логинятся одинаково по HTTP в Platform.Api. Мост НЕ консервируется рядом
   со вторым auth-путём (это был бы вечный техдолг — раздвоенная аутентификация). Device-пиннинг
   остаётся **ортогональной опциональной** способностью хоста.
3. **Токен на диске:** переиспользовать staff-auth Platform.Web (`staffTokenStore` + auth-клиент)
   один-в-один для обоих хостов; модель не изобретаем. DPAPI-hardening токена на общем ПК клуба —
   **необязательный follow-up**, не блокирует.
4. **Момент менять фундамент — сейчас:** prod не задеплоен, флота клиентов нет; позже дороже.

---

## Архитектура

### A. Унификация транспорта (Approach B)

**Ключевая идея:** мост делает две несвязанные работы. Auth — НЕ свойство хоста (Platform.Web
логинится по HTTP из браузера тем же способом). Device-identity/пиннинг — законно host-local
(у браузера нет «сиденья 5»). Разделяем их.

**A1. Auth → чистый HTTP в обоих средах.**
- `authClient.ts` переписывается: sign-in / refresh / load-token / sign-out идут напрямую в
  staff-auth эндпоинты Platform.Api (те же, что использует Platform.Web), без `postHostRequest`.
- Переиспользовать браузерный staff-auth Platform.Web: клиент + `staffTokenStore` (хранение
  opaque-токена / refresh в браузере). Реиспользование — предпочтительно через общий пакет/модуль;
  если код Platform.Web не вынесен в разделяемый слой, скопировать минимальный auth-модуль в
  оператор с идентичной семантикой (решение по вынесению — на этапе планирования, см. открытый
  пункт 3).
- `useOperatorAuth.ts` (restore + silent refresh + sign-in/out) остаётся по смыслу, но опирается на
  HTTP-транспорт вместо host-bridge.

**A2. WPF-хост худеет.**
- Auth-хендлеры в `OperatorWebHostBridge.cs` (`auth:signIn`, `auth:loadToken`, `auth:refresh`,
  `auth:signOut`) и связанный серверный `authApiClient.SignInAsync`-путь в хосте становятся
  мёртвыми → удаляются. WPF-хост становится тонкой оболочкой над URL + опциональный device-пиннинг.
- Инъекция `window.__AFK4_OPERATOR_CONFIG__` (`OperatorWebBootstrapScript.cs`) **остаётся** —
  несёт `platformBaseUrl`, `organizationId?`, `branchId?` (пин машины).

**A3. Device-пиннинг (`connection:*`) — опциональная способность хоста.**
- Остаётся как есть для WPF (снапшот машины/сиденья через ProtectedData).
- В браузере отсутствует: детекция среды (`window.chrome.webview` есть → WPF; нет → браузер);
  ветка connection гейтится по среде. Браузер берёт филиал из свитчера, а не из пина.

**A4. Bootstrap конфига по среде.**
- WPF: `platformBaseUrl`, `organizationId`, опциональный `branchId` из инъекции хоста (как сейчас).
- Браузер: `platformBaseUrl` из env сборки; `organizationId` и `branchIds` — из личности при
  логине (сервер резолвит сотрудника, см. B ниже). Ручной ввод organizationId не требуется.

### B. Реактивный контекст филиала + свитчер

**B1. Реактивный `activeBranchId`.**
- Ввести реактивное состояние активного филиала (паттерн `useActiveBranch` из Platform.Web +
  localStorage-ключ, напр. `afk4.operator.activeBranchId`) вместо одноразового вычисления в
  `App.tsx`.
- **Приоритет источников:** выбор пользователя в свитчере (localStorage) → пин машины
  (`config.branchId`, для одно-филиального ПК клуба) → `session.branchIds[0]`. Выбор пользователя
  побеждает пин машины для мульти-филиального сотрудника; одно-филиальный кассир свитчера не видит
  и остаётся на пине.
- Валидация: выбранный branch должен входить в `session.branchIds`; иначе откат к дефолту.

**B2. Пересборка контекста при смене филиала.**
- При смене `activeBranchId`: пересобрать `backendContext` (новый `branchId`) → пересоздать
  API-клиенты; пере-подписать realtime (device-hub на новый филиал — `useOperatorRealtime.ts`,
  `useOperatorConnection`), **с чистым teardown старой подписки**; перезагрузить preload'ы
  (`usePlayersPreload` и пр.).
- Пересчитать `firstAllowedWorkspace` для активного филиала и, если текущий открытый воркспейс
  недоступен, редиректнуть на первый разрешённый.

**B3. `BranchSwitcher` в шапке shell.**
- Переиспользовать компонент Platform.Web (`BranchSwitcher.tsx`) + хук `useActiveBranch`.
- Показывать **только когда `session.branchIds.length > 1`**. Источник списка филиалов с именами —
  директория филиалов (аналог `useBranchDirectory`/`branches.ts`); если имена не в сессии, тянуть
  их с бэка.

### C. Ролевой гейт + починка per-branch авторизации (ПЕРВЫМИ тасками)

- **Найдена cross-branch эскалация (подтверждено кодом):** `StaffAuthorizationService.RequireBranchPermissionAsync`
  проверяет `staffContext.BranchIds.Contains(branchId) && staffContext.Permissions.Contains(permission)`,
  где `Permissions` — плоский **union** прав по всем ролям всех филиалов
  (`OpaqueStaffTokenService.CreateContextAsync` + `PermissionCatalog.GetPermissions`). Роли хранятся
  per-branch (`StaffRoleAssignment.BranchId`), но при сборке контекста branch теряется. Следствие:
  менеджер филиала A, назначенный кассиром в B, получает менеджерские права на B.
- **Решение (принято 2026-07-22): чиним в корне ПЕРВЫМИ тасками фундамента**, до свитчера, потому
  что свитчер как раз включает кросс-филиальную работу в операторе и делает дыру живой. Починка
  **локальная и серверная**: `StaffContext` получает per-branch карту прав + метод
  `HasBranchPermission(branchId, permission)`; `CreateContextAsync` собирает права **по каждому
  филиалу** (группировка role-assignments по `BranchId`); `RequireBranchPermissionAsync` проверяет
  право именно для этого branchId. **Контракт `StaffSignInResponse` не меняется** (плоский
  `Permissions[]` остаётся UI-подсказкой) → **Platform.Web не ломается**. Union-`Permissions` в
  `StaffContext` сохраняется для org/platform-проверок, которые не branch-специфичны.
- Клиентский ролевой гейт (`canOpenWorkspace` / `permissionNames` / `workspacePermissionRules`) —
  оставляем; UI-переизбыток на неактивном филиале безвреден, бэк энфорсит per-branch.
- На смену филиала пересчитывать `firstAllowedWorkspace` (см. B2).

### D. Хостинг браузерной сборки

- Vite-режим «браузер» (сборка без зависимости от WPF-инъекции): `platformBaseUrl` из env.
- Раскатка на staging (как Platform.Web). Prod-хостинг отложен (не задеплоен).

## Поток данных (happy path, браузер)

1. Owner открывает URL оператора в браузере (ноутбук/телефон).
2. Логин phone/email + пароль → HTTP в staff-auth Platform.Api → сессия с
   `organizationId`, `branchIds[]` (несколько), `permissions`, `roleNames`. Токен сохраняется в
   браузерном сторе.
3. `branchIds.length > 1` → в шапке рендерится `BranchSwitcher`. Активный филиал = сохранённый
   выбор или `branchIds[0]`.
4. `backendContext` строится на активном branchId; все клиенты и realtime работают по HTTP/SignalR
   в Platform.Api. Owner видит разделы, разрешённые ролью.
5. Owner переключает филиал в свитчере → контекст пересобирается, данные перезагружаются, гейт
   пересчитывается. Никакого WPF-хоста в цепочке нет.

Поток в WPF идентичен, кроме: конфиг (platformBaseUrl/org/пин) приходит из инъекции хоста, а не из
env/логина; свитчер обычно скрыт (одно-филиальная машина); device-пиннинг активен.

## Обработка ошибок

- Нет токена / протух и refresh не удался → редирект на экран логина (оба хоста).
- Выбранный филиал не в `branchIds` (напр. роль отозвали) → откат к дефолтному, тост.
- Смена филиала во время in-flight запросов → старые ответы игнорируются/отменяются (teardown
  контекста); realtime старого филиала закрывается до открытия нового.
- Браузер без `platformBaseUrl` в env → явная ошибка конфигурации при старте (не молчаливый провал).
- Бэкенд отвергает действие на филиале, где нет прав (per-branch) → штатный 403 → тост (backend
  authoritative, даже если UI показал раздел).

## Тестирование

**Operator.App.Web (`bun test` + `bun run build`):**
- Выбор транспорта: `window.chrome.webview` есть → host-режим; нет → браузер-HTTP-режим.
- `resolveActiveBranchId`/`useActiveBranch`: приоритет (свитчер > пин машины > `branchIds[0]`);
  валидация против списка; персист в localStorage.
- Смена филиала пересобирает `backendContext`, пересоздаёт клиентов, перезагружает данные,
  пере-подписывает realtime с teardown старого.
- `BranchSwitcher` виден только при `branchIds.length > 1`; скрыт при одном филиале.
- Пересчёт `firstAllowedWorkspace` при смене филиала; редирект с недоступного воркспейса.
- Auth по HTTP: sign-in/refresh/sign-out дёргают staff-auth эндпоинты (мок HTTP), не host-bridge.

**Platform.Api (xUnit, `PlatformApiFactory`):**
- Регресс per-branch авторизации: сотрудник с правом на филиале B не может выполнить действие на
  филиале A (нет per-branch роли на A), даже если право в union.

**Оба хоста (ручная/сборочная проверка):**
- WPF по-прежнему логинится (теперь по HTTP через хост-оболочку) и работает; браузерная сборка
  логинится и работает. `bun run build` зелёный (tsc тайпчекает и тесты).

## Открытые пункты — ЗАКРЫТЫ (сверено по коду 2026-07-22)

1. **Форма staff sign-in — двухступенчатая, зеркалим Platform.Web:**
   - Шаг 1: `POST /api/auth/staff/sign-in-by-login` c `{login, password}`. 200 = `StaffSignInResponse`;
     401 = неверно; **409** = `{clubs:[{organizationId,name}]}` → UI показывает выбор клуба.
   - Шаг 2 (при 409 или явном клубе): `POST /api/auth/staff/sign-in` c `{organizationId, userName, password}`.
   - organizationId руками НЕ вводится, из сабдомена НЕ берётся — сервер резолвит по login/email.
   - Refresh: `POST /api/auth/staff/refresh` c `{organizationId, refreshToken}`. **Серверного
     sign-out НЕТ** — sign-out локальный (чистка стора). Токены opaque, access 8ч, refresh 30д (ротируется).
   - Хранение: весь `StaffSignInResponse`-производный сеанс в `sessionStorage['afk4.staff.session']`
     (образец `Platform.Web/auth/staffTokenStore.ts`); активный филиал — `localStorage['afk4.operator.activeBranchId']`.
2. **PC-control работает удалённо из браузера — ДА.** Команды лок/анлок идут по HTTP
   `POST /api/devices/{id}/commands` → SignalR `DeviceHub` → on-site агент; host-мост не участвует.
   **Деградация в браузере не нужна**, PC-control остаётся как есть. Оффлайн-очередь — локальный буфер.
3. **Переиспользование auth-кода — КОПИРУЕМ минимальный модуль в оператор.** Platform.Web и оператор —
   отдельные Vite-приложения без общего пакета; порт `staffTokenStore.ts`-подобного стора +
   staff-auth-клиента (`staffAuthApi.ts`) в `AFK4.Operator.App.Web/src/auth/` с идентичной семантикой.

## Вне scope / отложено

- Новые org-level экраны: **Branches** (свод по сети), **Billing** (подписка орг.), **Install**
  (онбординг агента), **Journal** (аудит) — под-проекты №2+.
- Слияние дублирующих branch-экранов Platform.Web (Venue/Clients/Monetization/Settings/Reports) с
  оператором — последующие под-проекты.
- Снос owner-панели Platform.Web/club — после того как оператор поглотит функции.
- SaaS Control Plane (`/admin`, вендорская консоль тенантов) — **вне периметра** оператора навсегда.
- DPAPI-hardening токена на ПК клуба — необязательный follow-up.
- Мобильный хост/обёртка — отдельный инфра-под-проект (фундамент лишь делает web браузер-совместимым).
- Prod-хостинг браузерной сборки — после общего prod-деплоя.

## Декомпозиция эпика (для контекста — порядок под-проектов)

1. **Фундамент** (этот док): единый HTTP-транспорт + мульти-филиальность + ролевой гейт свитчера.
2. **Org-level экраны:** Branches / Billing / Install / Journal (то, чего у оператора нет; бэк
   частично готов — `/api/organizations/...`, `ViewSubscription`).
3. **Слияние дублей:** Monetization/Settings/Staff → затем Venue/Clients → затем Reports.
4. **Снос Platform.Web/club** + (отдельно) мобильная обёртка.
