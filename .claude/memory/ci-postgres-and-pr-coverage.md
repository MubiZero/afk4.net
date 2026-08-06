---
name: ci-postgres-and-pr-coverage
description: Как устроены Postgres-тесты в CI afk4 и почему PR-верификация теперь не ограничена веткой main.
metadata: 
  node_type: memory
  type: project
  originSessionId: 5cc09261-f4ea-4bf1-a26f-2ca49d85c51e
  modified: 2026-08-06T04:33:05.341Z
---

PR #134 (ветка `fix/ci-postgres-tests`, от main) закрыл две дыры проверок. Прогон в CI
подтверждён: `Skipped: 0`, 1493 passed, ~2.5 мин.

- **Postgres-тесты жили только на бумаге.** Пропускались всегда: переменные не задавались, а
  service-контейнеры GitHub — Linux-only, тогда как .NET-тесты идут на `windows-latest`.
  Решение: отдельный job `test-postgres` на ubuntu с `postgres:16`, гоняет ТОЛЬКО
  `tests/AFK4.Platform.Api.Tests` (в sln есть WPF — вне Windows не собрать).
- **Одной базы `afk4_ci_test` хватает на все переменные**: каждый набор создаёт свою схему
  (`CREATE SCHEMA` → `DROP … CASCADE`) и ходит через `SearchPath`. Имя БД ОБЯЗАНО кончаться
  на `_test` — каждый атрибут отказывается трогать непомеченную базу. Прав достаточно
  владельца БД; миграции применяются в рантайме (`Database.MigrateAsync()`), `dotnet ef` заранее не нужен.
- **`AFK4_REQUIRE_POSTGRES_TESTS=1` превращает skip в падение.** `PostgresTestAvailabilityTests`
  ищет **рефлексией** все `FactAttribute` с полем `EnvironmentVariable` — не списком: список уже
  разъехался, `AFK4_COMMERCE_TEST_POSTGRES` названа не по шаблону остальных трёх и терялась
  при поиске по имени. Добавляешь новый такой атрибут → сразу под стражем, но и переменную в
  workflow добавь.
- **`on: pull_request` больше без `branches: [main]`.** PR в долгоживущую design-ветку не
  проверялся НИЧЕМ (у PR #133 было «no checks reported»), и работа попадала в main непроверенной.
  Тест-страж теперь запрещает любой `branches:`-фильтр в этом workflow.
- **`pr-verification.yml` охраняется тестом** `ClientReleaseAutomationTests.PrVerificationWorkflow_*`
  — сверяет текст построчно, поэтому ЛЮБАЯ правка workflow роняет его, пока не обновишь тест.
  Флаг `run_windows` переименован в `run_dotnet` (управляет двумя job-ами).

См. [[afk4-env-quirks]]: baseline `AFK4.Agent.Service.Tests` на WSL = 26 падений (Windows-тулинг),
не регрессия.
