# Оператор = единая админка — под-проект №2: Org-экраны — Design

**Дата:** 2026-07-23
**Эпик:** «Оператор = единая админка» (тело). Под-проект №1 «Фундамент» — в `main` (merge `93c6f778`).
**Ветка:** `feat/operator-unified-admin-org-screens`

## Цель

Дать владельцу сети в operator-app четыре организационных (не привязанных к филиалу) экрана,
которых сейчас у оператора нет и которые в owner-панели Platform.Web `/club` живут как отдельная
группа `account`: **Branches** (свод по сети), **Billing** (подписка организации), **Install**
(онбординг новой машины через Мастер настройки), **Journal** (org-level аудит). Platform.Web НЕ
переделываем — функции абсорбируем в оператор под owner-права.

## Контекст (инвентаризация 2026-07-23)

Фундамент уже дал: единый HTTP-транспорт staff-auth (`apiTransport` + access-token-provider с
авто-refresh), мульти-филиальность (`useActiveBranch`, `BranchSwitcher`, реактивный `activeBranchId`),
ролевой гейт, браузерную сборку (`VITE_PLATFORM_BASE_URL`), per-branch авторизацию на бэке.

Найденное состояние 4 областей (важно — они разной зрелости, это НЕ 4 одинаковых переноса):

- **Branches.** Platform.Web показывает дашборд карточек филиалов с KPI + rename. Данные собираются
  на клиенте из per-branch вызовов, которые у оператора уже есть. Бэк `GET /api/owner/branches` —
  заглушка: DTO только `{BranchId, Name}`, а право проверяется «с чужого плеча» — `ManageNews`
  (в каталоге нет `ViewBranches`). Настоящая ценность для оператора — новая.
- **Billing.** Read-only обзор. Бэк готов: `GET /api/organizations/{id}/subscription`,
  `GET /api/organizations/{id}/invoices` (право `ViewSubscription`, IDOR-guard уже есть). Owner
  ничего не меняет — апгрейд/отмена плана только у платформы-вендора (`/api/platform/...`).
- **Install.** В Platform.Web это пустышка: статическая ссылка на `.msi` + инструкция, без API.
  Реальный онбординг делает **AFK4.SetupWizard**: пользователь на новой машине запускает
  установщик-бандл (`afk4-client-<version>-<channel>.exe`, WiX Burn, несёт .NET 10), Мастер требует
  вход (телефон/логин) → `POST /api/install/auth/discover` → выбор филиала → роль устройства
  (`gaming_pc` / `manager_workstation`) → имя ПК → `POST /api/install/auth/seats` (для gaming_pc) →
  `POST /api/install/auth/enroll`. Owner НЕ генерирует код руками (owner-code удалён; code-based
  `device-enrollment-codes` — отдельный живой механизм, но Мастер его не использует).
  **Прод-URL дистрибутива пока не существует:** CI `client-packages.yml` — ручной `workflow_dispatch`,
  льёт `.exe` в GitHub Actions артефакты (retention 3 дня) + опционально в настраиваемое update-хранилище
  (file-system / http-put / s3). Точка публичной раздачи конфигурируется при релизе.
- **Journal.** Дыра в бэке: org-level чтения аудита НЕТ. Есть только branch-level
  `GET /api/branches/{id}/audit` (у оператора подключён в `BackendLogsWorkspace`). При этом
  org-level действия owner'а (news / loyalty / dc-config / eskhata-config) ПИШУТСЯ в аудит с
  `BranchId=null`, но прочитать их нельзя ни одним эндпоинтом. Плюс `install.discover` не пишет
  аудит вовсе (константа `InstallDiscoverInvoked` не используется).

## Решения (зафиксировано с пользователем)

1. **Объём:** все 4 экрана в одном под-проекте.
2. **Install — через Мастер настройки**, не через ручную генерацию кода. Экран = гайд «скачай
   установщик, запусти на машине, дальше Мастер сам проведёт привязку».
3. **Install URL — из конфига**, не хардкод. Пусто → честный текст «получить у IT / из релиза»,
   кнопка disabled. Никакой битой/фейковой ссылки.
4. **Journal — чинить корень:** новый org-level эндпоинт чтения аудита.
5. **Размещение — новая owner-only секция «Сеть»** в рейле оператора.
6. **Права — заводить корректные** (не тиражировать техдолг): для Branches — новое `branches.view`;
   для Install — использовать существующее `install.device`; сверить точные строки с backend
   `StaffPermissionNames` в плане.

## Архитектура

Новая секция рейла «Сеть» (7-я, после «Управление»). Внутри — 4 destination-экрана по паттерну
`management/managementNav.ts`: список destinations, гейт `hasAnyPermission(session, permissions)`.
Секция видна, если у сессии есть хоть одно из owner-прав её экранов.

**Org-level, не branch-scoped:** экраны НЕ подписаны на `activeBranchId` и НЕ пересобираются при
смене филиала (в отличие от Карты/Кассы/Клиентов). Работают в контексте `session.organizationId`.

**Feature-shape** (как принято в проекте): каждый экран — каталог `network/<feature>/` с
`*Screen.tsx` + `use*.ts` (загрузка/состояние) + `*Model.ts` (маппинг в view-модель) поверх
доменного api-client. Транспорт — существующий `apiTransport` + access-token-provider из фундамента.

### Раскладка файлов (ориентир, финал — в плане)

```
src/AFK4.Operator.App.Web/src/
  network/
    NetworkSection.tsx            // хост секции: destinations list + рендер выбранного экрана
    networkNav.ts                 // destinations + allowedNetworkDestinations(session)
    branches/
      BranchesScreen.tsx
      useBranchRollup.ts
      branchRollupModel.ts
      RenameBranchDialog.tsx
    billing/
      BillingScreen.tsx
      useBilling.ts
      billingModel.ts
    install/
      InstallScreen.tsx
      installModel.ts             // getInstallerUrl() из конфига оператора
    journal/
      JournalScreen.tsx
      useOrgAudit.ts
      orgAuditModel.ts
  api/clients/
    orgBilling.ts                 // subscription + invoices
    orgAudit.ts                   // GET /api/organizations/{id}/audit
    ownerBranches.ts              // GET /api/owner/branches (+ reuse dashboard summary/branch profile)
```

## Компоненты

### 1. Branches — свод по сети
- **Данные:** `GET /api/owner/branches` (id+имя) + для каждого филиала `GET /api/branches/{id}/dashboard/summary`
  (KPI: устройства online/total, активные сессии, выручка за сегодня, alerts) — оператор эти вызовы
  уже делает. Клиентская агрегация в totals.
- **UI:** верхние KPI-карточки (кол-во филиалов / устройств online·total / активных сессий / выручка
  сегодня / требуют внимания) + грид карточек филиалов (имя, город, те же KPI; ошибка загрузки
  филиала → карточка «ошибка», totals не падают).
- **Действия:** «Открыть» → `useActiveBranch.select(branchId)` + переход на Карту; «Переименовать»
  → `RenameBranchDialog` → `PATCH /api/branches/{id}/profile`.
- **«Добавить филиал»** — disabled с подписью «недоступно» (нет бэка; фейк не рисуем).
- **Гейт:** новое право `branches.view` (заводим в каталоге; бэк `/api/owner/branches` переводим с
  `ManageNews` на `branches.view` через `RequireOrganizationPermission`).

### 2. Billing — подписка (read-only)
- **Данные:** параллельно `getSubscription` + `listInvoices`.
- **UI:** карточка подписки (`planCode`, статус-бейдж по `TenantSubscription.Status`, сумма через
  `@afk4/money` `minorToMajor` + `formatCurrency`, текущий период, дата следующего счёта,
  бейдж `cancelAtPeriodEnd`) + таблица счетов (number / issued / due / amount / статус-бейдж).
  Пусто → empty-state; ошибка → error-state + retry.
- **Действий нет.** Гейт: `viewSubscription` (добавить в operator-каталог).

### 3. Install — установщик-мастер
- **UI:** заголовок + подзаголовок; кнопка «Скачать установщик» (`href = getInstallerUrl()`);
  карточка «Как установить» — шаги реального потока Мастера (запустить установщик → войти
  телефоном/логином → выбрать филиал → выбрать роль ПК → задать имя → готово); карточка «Филиалы»
  (список организации, informational).
- **URL:** `getInstallerUrl()` читает конфиг оператора (env-переменная, по аналогии с
  `VITE_PLATFORM_BASE_URL`; имя — напр. `VITE_SETUP_INSTALLER_URL`). Пусто → кнопка disabled +
  текст «получить у IT / из релиза». Никогда не рисуем битую ссылку `/downloads/AFK4-Agent.msi`.
- **Гейт:** `installDevice` (`install.device`; добавить в operator-каталог).

### 4. Journal — org-level аудит
- **Бэкенд (новое):** `GET /api/organizations/{organizationId}/audit` (query: action, outcome,
  targetType, fromUtc, toUtc, limit — как branch-версия). Авторизация:
  `RequireOrganizationPermission(ViewAudit)` + IDOR-guard `organizationId == staffContext.OrganizationId`.
  Новый метод сервиса поиска: фильтр по `OrganizationId` (все филиалы организации + записи с
  `BranchId == null`), реюз `AuditSearchQuery` / `AuditRecordDto` / `AuditSearchResultDto`.
- **Заодно:** записать аудит `install.discover.invoked` в `POST /api/install/auth/discover`
  (константа есть, вызова нет) — закрыть пробел write-path.
- **UI:** фильтры (диапазон дат + action + targetType + outcome) + таблица (дата / актёр / действие /
  цель / итог-бейдж / источник / детали). Реюз date-range: если у оператора уже есть общий контрол —
  использовать его; иначе перенести минимально нужное.
- **Гейт:** `viewAudit` (`audit.view`; уже в каталоге).

## Кросс-режущее: выравнивание permission-каталога

Operator `src/AFK4.Operator.App.Web/src/permissionNames.ts` отстаёт от backend `StaffPermissionNames`:
добавить `viewSubscription` и `installDevice` (сверить точные строки). Завести `branches.view` в
backend `StaffPermissionNames` + `PermissionCatalog` (роль Owner) и в operator-каталоге; перевести
`GET /api/owner/branches` на него.

## Поток данных

Все запросы идут через существующий `apiTransport` (staff-auth заголовки + авто-refresh) и
access-token-provider из фундамента; base URL — `VITE_PLATFORM_BASE_URL` (браузер) либо host-конфиг
(WPF). Org-эндпоинты используют `session.organizationId`; branch-scoped вызовы Branches-свода
(`dashboard/summary`, `profile`) идут по `session.branchIds` (не по одному `activeBranchId`).

## Обработка ошибок

- Загрузка филиала в своде упала → карточка «ошибка», totals считаются по успешным (частичный сбой
  не рушит экран).
- Billing/Journal ошибка сети → error-state + retry.
- Install без сконфигурированного URL → disabled-состояние с честным текстом (не ошибка).
- Org-audit: 401 → стандартный refresh-flow фундамента; 403 (чужой org / нет права) → отражается как
  «нет доступа», не как generic-ошибка.

## Тестирование

- **Frontend (`bun test`, happy-dom + jest-dom):** каждый `use*`/`*Model`/`*Screen`; секция «Сеть»
  видна/скрыта по правам (паттерн `operatorVisibility.test`); Branches — частичный сбой филиала;
  Billing — money-форматирование; Install — disabled при пустом URL; Journal — фильтры и рендер строк.
  Финал слайса — `bun run build` (`tsc -b && vite`, тайпчекает и тест-файлы).
- **Backend (xUnit, `PlatformApiFactory`):** новый org-audit — org-wide + `BranchId=null` видны;
  IDOR 403 на чужой `organizationId`; permission-гейт (нет `ViewAudit` → 403); branches-permission
  регресс (после фикса `/api/owner/branches` доступен по `branches.view`, а не по `ManageNews`).

## Порядок реализации (фазы)

1. **Бэкенд:** `branches.view` в каталоге + фикс `/api/owner/branches`; org-audit эндпоинт+сервис;
   `install.discover` аудит-запись. + xUnit.
2. **Каркас секции «Сеть»:** `networkNav.ts` + `NetworkSection.tsx` + подключение в рейл/`WorkspaceId`
   + гейт + выравнивание operator permissionNames. + visibility-тест.
3. **Branches** экран.
4. **Billing** экран.
5. **Install** экран.
6. **Journal** экран.

Фронт-экраны 3–6 независимы друг от друга — удобно для параллельных сабагентов.

## Явно вне объёма (YAGNI)

- Создание нового филиала (нет бэка) — «Добавить филиал» остаётся disabled.
- Owner-инициируемое управление подпиской (апгрейд/отмена/оплата) — прерогатива платформы-вендора.
- Публичный прод-CDN для установщика — релизное/инфра-решение, не этот под-проект.
- Пагинация аудита — как в branch-версии остаётся `limit` (без курсора).
- Снос дублей Platform.Web и мобильная обёртка — под-проекты №3 и №4.
