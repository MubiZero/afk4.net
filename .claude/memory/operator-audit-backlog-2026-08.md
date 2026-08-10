---
name: operator-audit-backlog-2026-08
description: "Аудит Оператора 2026-08-10 по коду: баг авто-товара в POS, пропавшее окно обновления, ~1050 строк мёртвого кода, 26 отключённых тестов, неготовность к мобиле"
metadata: 
  node_type: memory
  type: project
  originSessionId: 0b12af7f-4dcf-4243-beb9-11cda59c4af0
  modified: 2026-08-10T04:30:52.786Z
---

Аудит `src/AFK4.OrganizationAdmin.Web` 2026-08-10 **по коду, не по памяти**. Базовое здоровье
хорошее: `bun run build` зелёный, 1015 + 94 теста проходят, ни одного `TODO`/`FIXME` во фронте.
Найденное ниже — проверено чтением кода, не предположения.

## 1. Баг: POS сам кладёт товар в чек (чинить первым)

`BackendPosWorkspace.tsx:214` — при загрузке каталога, если валидных позиций в корзине нет:
`return backendProducts[0] ? [{ ...backendProducts[0], quantity: 1 }] : []`. Пустая корзина
получает **первый товар каталога**. Остаток демо-фикстуры в боевом пути (рядом строки 163/180 —
честные фикстуры dev-mock). Тестов, закрепляющих поведение, нет → правится безопасно.
Риск: кассир пробивает лишнюю позицию.

## 2. Пропавшая функция: окно обновления клуба

Бэкенд жив: `UpdateEndpoints.cs` отдаёт `GET/PUT organizations/branches/{id}/updates/preferences`
и `/rollouts`; API-клиент `api/clients/updates.ts` на месте; тест клиента в
`operatorApiClients.test.ts` есть. **Ни один живой экран его не зовёт** — UI умер вместе с
«Интеграциями». На платформе (`PlatformControl.Web/src/platform/updates`) только вендорская
сторона: пакеты и раскатки. Клуб не может задать окно обновления и перезапуск. Классическое
полу-наличие: контракт обещает, лица нет.

## 3. Мёртвый код (~1050 строк TSX + 261 ключ × 3 локали)

Никто не импортирует: `BackendSettingsWorkspace.tsx` (437), `DashboardWorkspace.tsx` (444 —
`WorkspaceRouter` рендерит под `dashboard` уже `ReportsWorkspace`), `QuickActionsMenu.tsx` (169),
`settings/SettingsProfileSection`, `settings/SettingsIntegrationsSection`,
`settings/OrganizationAdminUpdateCard`. Из 368 ключей `op.settings.*` живым кодом используются 9.

**ОСТОРОЖНО при уборке:** `SettingsLayoutSection`, `SettingsStaffSection`, `SettingsTariffsSection`,
`SettingsGoodsSection`, `ProductBarcodesSection` — **ЖИВЫЕ**, их переиспользуют
`management/destinations/*`. Папку `settings/` целиком сносить нельзя.

## 4. 26 отключённых интеграционных тестов

`App.test.tsx` — 27 `it.skip` с комментарием (строка ~1083): включить, «once Task 1.6 wires real
content behind 'management'». Контент подключён давно, условие выполнено, тесты не вернули.
Покрывали: приглашение/роль/профиль/деактивацию/сброс пароля сотрудника, залы и места, устройства
и команды им, каталог POS, тарифы и пакеты, публикацию обновлений. Сейчас это проверяется только
точечными тестами экранов, интеграционно — нет.

## 5. К мобильной обёртке код не готов

Медиа-запросы в 11 CSS из 27; 39 жёстких `width: NNNpx`; 17 `grid-template-columns` в пикселях;
8 `min-width` на контейнерах. Мобильного хоста в `src/` нет. Сначала расшивка вёрстки, потом хост
— см. [[operator-as-unified-admin-epic]] (это последний незакрытый кусок эпика).

## 6. Мелкие долги (подтверждены)

«инвойс» вместо «счёта» — 7 мест в `locales/ru.json`; `.clients-primary-action` жив (12
упоминаний) вместо `.ui-btn`; `StateFlag` не сведён к `.ui-chip--count`
(см. [[operator-ui-kit-epic]], там же остальные переносы).

**Рекомендованный порядок:** баг POS → окно обновления → уборка мёртвого кода и ключей →
возврат 26 тестов. Мобилу — отдельным крупным заходом, не смешивая с уборкой.
