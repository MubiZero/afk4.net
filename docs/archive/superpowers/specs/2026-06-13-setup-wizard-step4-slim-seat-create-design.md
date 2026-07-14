# Setup Wizard — шаг 4 «Игровое место»: упрощение до create-only

**Дата:** 2026-06-13
**Статус:** дизайн утверждён, готов к плану
**Затрагивает:** `src/AFK4.SetupWizard.Web` (фронт визарда), i18n-локали, тесты `DeviceScreen`. Бэкенд — без изменений.

## Проблема

Шаг 4 визарда сейчас содержит полноценный механизм работы с местами зала: список мест с группировкой по зонам, поиск, тумблер «только свободные», свёрнутую форму создания нового места и три empty-состояния. Это:

1. **Дублирует Operator App.** Зоны/места/карта уже полностью редактируются в Operator App (`BackendSettingsWorkspace.tsx` + floor-map, эндпоинты `/api/branches/{id}/layout/...`). Визард повторил кусок этой функциональности.
2. **Расходится с индустрией.** Senet, SmartShell, LANGame, ggLeap — все используют двухфазную модель: пер-машинный установщик только привязывает ПК к клубу, а расстановку по карте делают централизованно в админ-панели. Ни одна платформа не кладёт расстановку в установщик.
3. **Громоздко.** Поиск/тумблер/группировка/empty-states — много UI ради задачи, которую делает один человек у одной машины.

## Решение

Свести шаг 4 к одному действию: **задать имя и создать под него место**. Расстановка (зоны, позиции на карте, перенос в VIP, переименование) — полностью в Operator App.

### Целевое поведение

- **Одно поле имени.** Введённое имя становится **и именем устройства (displayName), и именем места (seat name)**. Двух полей нет.
- **На «Зарегистрировать»:** визард создаёт место с этим именем в зоне по умолчанию филиала, затем привязывает (enroll) устройство к этому месту. Создание + enroll в одном submit-потоке.
- **Зона по умолчанию всегда есть.** Каждый новый филиал сидится с зоной «Main Hall» (`EfPlatformTenantService.cs:168`), поэтому create работает и на самом первом ПК клуба — без предварительного захода в Operator App. Берём `branch.zones[0]`.
- **manager_workstation:** без изменений — роль не требует места (`requiresSeat = role === 'gaming_pc'`), поле места не показывается.

### Что убираем из `DeviceScreen.tsx`

- Список мест (`wizard-seats-list`), группировку по зонам (`groupSeatsByZone`, `ZoneGroup`, `wizard-seat-grid`, карточки мест).
- Поиск (`seatFilter`, `wizard-device-seats-toolbar`) и тумблер «только свободные» (`onlyFree`).
- Три empty-состояния (`noSeats`, `noMatch`/filtered/default + reset).
- Свёрнутую форму создания и её toggle (`createOpen`, `openCreateForm`, `closeCreateForm`, `wizard-create-seat`).
- Сопутствующий стейт: `seats`, `freeSeatIds`, `selectedSeatId`, `pendingSeatIds`, optimistic-плитки. Остаётся только `displayName` + `request`.

### Что остаётся

- Поле имени (`field.name` / `field.nameHint`), валидация 3–32 символа.
- Кнопки «Назад» / «Зарегистрировать», состояния `enrolling`, обработка ошибок, `onBusyChange` (защита кнопки закрытия во время установки).
- Заголовки `device.gaming.*` / `device.manager.*`.

### Поток submit (новый `handleSubmit`)

```
1. validate displayName (3–32)
2. requiresSeat?
   - да:  request=creating → createSeat({branchId, zoneId+zoneName: branch.zones[0], name}) →
          request=enrolling → enrollDevice({branchId, seatId: createdSeat.seatId, role, displayName}) →
          onEnrolled(result, createdSeat)
   - нет: request=enrolling → enrollDevice({branchId, seatId: null, role, displayName}) →
          onEnrolled(result, null)
3. error на любом шаге → request=error, message
```

Оптимистичные плитки больше не нужны — нет списка, в который их вставлять. Восприятие скорости обеспечивается спиннером на кнопке (создание+enroll — одно действие пользователя).

## Бэкенд

Без изменений. Используются существующие:
- `POST /api/install/auth/seats` (создание места, `installClient.createSeat`).
- `POST /api/install/auth/enroll` (enroll, `installClient.enrollDevice`).

Эндпоинты слоя layout (`/api/branches/{id}/layout/...`) и floor-map остаются домом Operator App.

## i18n

- **Удалить** ставшие неиспользуемыми ключи `setup.wizard.device.seats.*` и `setup.wizard.device.create.*` из `locales/{ru,en,tg}.json`, затем `bun run gen`.
- **Проверить перед удалением**, что ключ не используется где-то ещё (особенно `setup.wizard.branch.meta.of`/`free` и `devices.status.online`/`offline` — они из других неймспейсов и, скорее всего, используются на других экранах: **не удалять**, если есть ссылки).
- Новых ключей, как правило, не требуется: `field.name` / `field.nameHint` уже описывают «Имя ПК» и подходят (имя видно в админке). При желании можно уточнить формулировку hint, что имя задаёт и место, — опционально, не блокер.

## Трейд-офф (явно)

Визард всегда создаёт **новое** место. Если оператор заранее создал место в Operator App или ПК переустанавливают — появится второе место с тем же именем (дубль). Это сознательный размен на простоту: расстановка и наведение порядка (слияние/удаление/перенос) — в Operator App. Дубли не ломают данные: место — это просто именованный слот в зоне.

## Тестирование (`DeviceScreen.test.tsx`, bun test)

- gaming_pc: ввод имени → submit → вызваны `createSeat` (с `branch.zones[0]`) затем `enrollDevice` (с `seatId` созданного места и `displayName`); `onEnrolled` получил результат и созданное место.
- manager_workstation: submit → `enrollDevice` с `seatId: null`, `createSeat` не вызывается; поле места не рендерится.
- Валидация: имя короче 3 / длиннее 32 → кнопка «Зарегистрировать» disabled, сеть не дёргается.
- Ошибка `createSeat` → состояние error, enroll не вызывается. Ошибка `enrollDevice` → состояние error.
- Bridge unavailable → сообщение `device.error.bridgeMissing`.

## Вне области

- Любые изменения floor-map / layout в Operator App.
- Изменения бэкенда.
- Изменения других шагов визарда (1–3, 5).
