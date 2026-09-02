# Спеки

Спека объясняет, почему система устроена так, а не иначе. Поэтому спеки, в отличие от
планов, не уезжают в архив после отгрузки: их читают, когда меняют уже сделанное. Но
считать сделанное «одобренным заделом» нельзя — отсюда деление ниже.

Обновлено 03.09.2026.

## Источник правды по архитектуре

- `2026-05-12-afk4-platform-architecture-design.md` — платформа целиком: границы,
  модули, данные, клиенты.
- `2026-07-28-platform-organization-product-boundary-design.md` — граница между Platform
  Control и Organization Admin: личности, роли, права, маршруты, релизы. Действует.

## Реализовано и влито

Читать при изменении соответствующей части, а не как план работ.

- `2026-07-29-platform-control-rebuild-design.md` и
  `2026-08-03-platform-control-ui-redesign-design.md` — панель платформы. Визуальные и
  экранные решения перестройки заменены редизайном; границы маршрутов, прав и контрактов
  из перестройки остались в силе.
- `2026-08-04-platform-access-and-support-mode-design.md` — двухфакторный вход,
  справочник администраторов платформы, режим поддержки.
- `2026-08-07-platform-billing-dunning-and-pricing-design.md`,
  `2026-08-07-platform-observability-and-analytics-design.md`,
  `2026-08-08-platform-product-operations-design.md` — волны биллинга, наблюдаемости и
  продуктовых операций платформы.
- `2026-08-11-customer-app-flutter-design.md` и `2026-08-11-flutter-migration-map.md` —
  приложение игрока. Переезд с веба завершён; веб-версия удалена 02.09.2026, а веб-сборка
  Flutter закрывает вход по ссылке без установки.
- Волна консолидации Organization Admin (июль): `…operator-unified-admin-*`,
  `…operator-management-*`, `…operator-reports-workspace-consolidation-design.md`,
  `…operator-post-auth-shift-gate-design.md`, `…operator-media-upload-subsystem-design.md`.
- Платежи: `…eskhata-merchant-acceptance-design.md`, `…dc-paylink-manual-acceptance-*`,
  `…payments-loyalty-*`, `…operator-club-payments-rethink-decisions.md`.
- Брони: `2026-06-18-online-booking-autoconfirm-hold.md`.

## Реализовано частично

- `2026-07-29-platform-managed-client-updates-design.md` — публикация пакетов и раскатка
  переехали на платформу, восстановление последней рабочей версии у агента есть,
  окна обслуживания у филиала есть. **Открытым остаётся** служебный доступ для
  регистрации релиза из CI: маршрут требует сессии администратора платформы, а она за
  двухфакторкой, поэтому пакет сейчас заводит человек в панели.
- `2026-06-11-productionize-client-installer-design.md` и
  `2026-07-28-organization-admin-latest-installer-design.md` — установщик собирается и
  публикуется; **не решено** production-подписание: сертификат, его хранение и хранение
  ключа подписи метаданных обновлений.

## Связанное

- Планы незакрытой работы: `../plans/README.md`
- Операционная дорожная карта: `../../roadmap/production-readiness.md`
- Снимок текущего состояния: `../../progress/2026-05-12-vertical-slice-progress.md`
