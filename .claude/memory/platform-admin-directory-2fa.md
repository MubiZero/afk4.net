---
name: platform-admin-directory-2fa
description: Волна A эпика «пробелы платформенной админки» — каталог сотрудников платформы и обязательный TOTP; что решено и что осталось.
metadata: 
  node_type: memory
  type: project
  originSessionId: 5cc09261-f4ea-4bf1-a26f-2ca49d85c51e
  modified: 2026-08-08T18:57:41.532Z
---

Эпик «чего нет у нас против типовой b2b SaaS-админки» разбит на 4 волны: **A — доступ и поддержка**,
B — деньги (dunning, гибкая цена), C — наблюдение и аналитика, D — продуктовые операции
(feature flags, анонсы, офбординг). Пользователь выбрал делать по одной волне, не проектируя
остальные заранее.

Волна A разбита на два плана. **План 1 (слайсы 1-2) реализован полностью** в ветке
`feat/platform-admin-directory-2fa` (25 коммитов от `7b60aed5`), финальное ревью чистое.
Спека `docs/superpowers/specs/2026-08-04-platform-access-and-support-mode-design.md`,
план `docs/superpowers/plans/2026-08-04-platform-admin-directory-and-2fa.md`.

**План 2 (слайсы 3-4, режим поддержки) реализован** — см. [[platform-support-mode]].
Волна A закрыта целиком. **Волна B закрыта** — см. [[platform-billing-dunning-wave-b]].
**Волна C (наблюдение и аналитика) закрыта целиком** — см. [[platform-observability-wave-c]].
**Волна D (продуктовые операции) спроектирована, план 1 из пяти в main** — см. [[platform-product-operations-wave-d]].

## Durable-инварианты волны A

- Роли платформы **захардкожены в коде** (`PlatformAdminPermissionCatalog`): `platform_admin`
  и `platform_support`. Управление сотрудниками — список + приглашение + одна из двух ролей,
  не конструктор прав.
- 2FA **обязательна для всех**, TOTP свой (RFC 6238 на HMACSHA1, без NuGet — в Platform.Api
  бережно 5 зависимостей). Секрет через существующий `ISecretProtector`.
- Вход двухшаговый: пароль → челлендж (2 мин, отдельная таблица) → код → сессия.
  `PlatformAdminTestHelper.AuthorizeAsAsync` проходит оба шага, сидит фиксированный TOTP-секрет.
- Инварианты каталога защищены **serializable-транзакцией**; serialization failure маппится
  в generic `Conflict` («повторите»), НЕ в `LastFullAdmin` — иначе ложное объяснение отказа.

## Найдено по пути (durable)

- `AesGcmSecretProtector` был синглтоном с непотокобезопасным `AesGcm` — реальная гонка, била
  шифротекст. Чинится созданием `AesGcm` на вызов, НЕ глобальным lock: тот же протектор на
  платёжном хот-пути (`EskhataMerchantClientFactory`).
- **Выкатка неатомарна**: Platform API и PlatformControl.Web — разные приложения Coolify,
  workflow деплоит только API. Порядка без окна рассинхрона не существует → в транспорте
  панели живёт `PlatformStaleClientError` («обновите страницу»).

## Открытые вопросы проекта (вне ветки)

- ~~Postgres-тесты всегда skip в CI~~ — **починено** (PR #134, см. [[ci-postgres-and-pr-coverage]]).
  Ветке остаётся добавить `AFK4_PLATFORM_ADMIN_POSTGRES_TEST_CONNECTION_STRING` в
  `pr-verification.yml`, иначе страж справедливо покраснеет.
- `EfStaffInviteService.AcceptInviteAsync` — та же гонка по логину, что чинили здесь (500 вместо 409).
- Рантбук на полную потерю 2FA единственным админом: `docs/runbooks/`.

Отложенные миноры перечислены в конце ветки; см. [[afk4-env-quirks]] про bun и гейты.
