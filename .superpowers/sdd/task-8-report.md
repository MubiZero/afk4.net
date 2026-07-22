# Task 8 report — Operator.App: DC-пополнение в Кассе (QR + подтверждение)

## Что сделано

### Клиент (`src/AFK4.Operator.App.Web/src/api/clients/dcTopUps.ts`, новый)
`createDcTopUpClient(api)` → `{ create, cancel, confirm }`:
- `create(branchId, {playerAccountId, amountMinorUnits, currencyCode})` → `POST /api/branches/{id}/pos/dc-topups` → `DcTopUpDto`.
- `cancel(branchId, intentId)` → `POST /api/branches/{id}/pos/dc-topups/{id}/cancel` (204).
- `confirm(intentId)` → `POST /api/wallet/top-up-intents/{id}/fulfil` (существующий wallet-эндпоинт).
Типы `DcTopUpDto`/`CreateDcTopUpRequest` сверены с реальными C#-record'ами (`AFK4.Shared.Contracts.Payments.DcTopUpDtos`) — поля совпадают 1:1.
Зарегистрирован в `api/clients/index.ts` (`dcTopUps: createDcTopUpClient(api)`) и в барреле `operatorApiClients.ts`.

### Диалог (`src/AFK4.Operator.App.Web/src/players/DcTopUpDialog.tsx`, новый)
Самодостаточный компонент (владеет своим state, как `PhoneVerificationCard`, а не «пресентационный» модалка со state в оркестраторе, как `PayDebtModal`) — оправдано тем, что это многошаговый async-визард (ввод суммы → создание intent → QR → confirm/cancel), а не разовая форма.

Ключевое архитектурное решение: `backend`-проп — НЕ весь `OperatorBackendContext` (config+session), а узкий срез `{ dcTopUps: { create, cancel, confirm } }`. Так тест подставляет голые моки без `createAuthenticatedOperatorClients` и без `mock.module` (по памяти `frontends-on-bun-test`: `mock.module` в bun test течёт process-wide между файлами — это реальный источник флака в этом репо, задокументированный в `src/test/setup.ts` и уже наступавший на PhoneVerificationCard/PosOrdersTicker).

Фазы:
1. **Ввод суммы** — `parseMoneyInputMinorUnits` (существующий helper, major→minor через `@afk4/money`, а не наивный `*100`) гейтит кнопку «Показать QR».
2. **QR** — `QRCode.toDataURL(dto.payUrl)` (паттерн 1:1 с `Player.Shell.Web/TopUpScreen.tsx`), сумма/комментарий/`••••cardLast4`/подсказка, кнопки «Оплата получена» (confirm→onCredited()+onClose()) и «Отмена» (cancel→onClose()).
3. Ошибки — `useFeedbackToasts` + `projectOperatorError` (уже знает код `open_shift_required` → «Смена не открыта» и т.п.); при ошибке confirm диалог **не закрывается и не падает** — intent всё ещё pending, оператор может открыть смену и повторить.
4. `useBusyLabel` — маленький локальный хук отложенного спиннера (кнопка блокируется мгновенно; текст-«…» появляется через 250мс, чтобы быстрые запросы не мигали спиннером).

Решение **не показывать toast на успех** create/confirm/cancel (в отличие от `runClientAction` в оркестраторе, который зовёт `setFeedback({state:'confirmed'})` после сети): там `feedback`-state живёт в персистентном родителе и переживает закрытие модалки; здесь диалог сам себя закрывает — установка `state:'confirmed'` в том же тике, что и `onClose()`, ненадёжно долетит до toast-эффекта до размонтирования. Закрытие диалога — уже достаточный сигнал успеха; ошибки (диалог остаётся открытым) — единственный канал toast.

### Триггер + рефетч баланса
- `WalletZone.tsx`: новая полноширинная кнопка «DushanbeCity (перевод)» (`ui-btn ui-btn--block`) под формой counter-пополнения — НЕ третьей ячейкой в существующем `.topup-row` (grid 1fr/1fr: поле+«Пополнить»), т.к. у DC своя сумма (вводится внутри диалога, не делит поле «своя сумма»). Гейтится тем же `canTopUp` (permission `TopUpWallet`), что и оба backend-эндпоинта DC.
- `ClientDrawer.tsx`: прокидывает новый проп `onOpenDcTopUp` в `WalletZone`.
- `BackendPlayersWorkspace.tsx` (**отклонение от списка файлов брифа** — см. Concerns): добавлено, т.к. это единственное место, где реально доступны `backend.config/session` для сборки `dcTopUps`-клиента и где живут ВСЕ остальные modal-state'ы этого экрана (`payDebtOpen`, `correctionOpen`, ...). `ClientDrawer.tsx` сам никогда не открывал модалки — паттерн проекта: `useState` + условный рендер в оркестраторе, `ClientDrawer` только зовёт callback-пропы. Добавлено:
  - `dcTopUpOpen` state + `<DcTopUpDialog>` рендер рядом с `PayDebtModal` (тот же гард `backend !== null && selectedClient…`).
  - `backend={{ dcTopUps: createAuthenticatedOperatorClients(backend.config, backend.session).dcTopUps }}`.
  - `onCredited={() => { bumpLedger(); bumpWallet(); }}` — новый `walletReloadNonce`/`bumpWallet()`, добавленный в deps эффекта загрузки `walletSummary` (по образцу существующего `ledgerReloadNonce`). Понадобилось, т.к. `fulfil` возвращает `PlayerTopUpIntentDto`, НЕ `WalletSummaryDto` (в отличие от counter top-up, где ответ `topUpWallet` сразу кладётся в `setWalletSummary`) — «тот же рефетч, что после counter-пополнения» реализован как явный ре-триггер эффекта, а не оптимистичная запись ответа.

### i18n (`op.dc.topup.*`, 9 ключей во всех трёх локалях + `bun run gen`)
`open/amount/showQr/received/cancel/hint/comment/cardLast4/feedbackLabel` — реальный перевод во всех локалях, tg — настоящий таджикский (сверен по терминологии с существующими ключами: «Маблағи пурракунӣ» = тот же корень, что и `op.players.actions.topUpAmountLabel`; «Бекор кардан» = тот же, что и `common.cancel`). Guard-тест `tg-i18n-honesty` (`packages/i18n/src/messages.test.ts`) зелёный без новых whitelist-записей — все новые ru/tg значения реально различаются.

`op.payments.zone.income.lead` (протухшая копи из Task 2) — **не тронута**: перепроверил все 3 локали, текст «Eskhata основной, DushanbeCity дополнительный» уже корректен (DC-конфиг вернулся в Task 7), правки не требовались.

### Зависимость
`qrcode@1.5.4` + `@types/qrcode@1.5.6` (та же мажорная версия, что в `Player.Shell.Web/package.json`).

## TDD-evidence

RED (до реализации `DcTopUpDialog.tsx`):
```
error: Cannot find module './DcTopUpDialog' from '.../DcTopUpDialog.test.tsx'
0 pass / 1 fail
```

GREEN (после реализации + i18n gen):
```
$ bun test src/players/DcTopUpDialog.test.tsx
4 pass
0 fail
9 expect() calls
```
(4 теста: happy-path создание+QR+confirm из брифа; передача major→minor `*100` в `create`; «Отмена» на экране QR зовёт `cancel`+`onClose`; ошибка confirm — тост с деталью, диалог не падает и не закрывается, `onCredited` не зовётся.)

## Гейты

- Целевой: `bun test src/players/DcTopUpDialog.test.tsx` → **4 pass / 0 fail**.
- Общий: `bun run test` → **835 pass / 0 fail** (132 файла) + **63 pass / 26 skip / 0 fail** (App.test.tsx, отдельный прогон).
- Сборка: `bun run build` → `tsc -b && vite build` → **✓ built in 321ms** (типы бан-моков в `DcTopUpDialog.test.tsx` явные — `DcTopUpDialogBackend['dcTopUps']`, тайпчек тест-файлов прошёл).
- `grep -rn "DcGate\|BranchPaymentGateway\|payments_cards\|PaymentGatewaysWorkspace" src` — совпадения только в исторических EF-миграциях (`BranchPaymentGatewayEntity`, до этой ветки), новых мест не добавлено.
- `git diff --stat` — `Player.Shell.Web` не затронут (0 файлов).

## Files changed

Новые:
- `src/AFK4.Operator.App.Web/src/api/clients/dcTopUps.ts`
- `src/AFK4.Operator.App.Web/src/players/DcTopUpDialog.tsx`
- `src/AFK4.Operator.App.Web/src/players/DcTopUpDialog.test.tsx`

Изменённые:
- `src/AFK4.Operator.App.Web/src/api/clients/index.ts` — регистрация клиента.
- `src/AFK4.Operator.App.Web/src/operatorApiClients.ts` — ре-экспорт барреля.
- `src/AFK4.Operator.App.Web/src/players/WalletZone.tsx` + `.test.tsx` — кнопка-триггер, новый тест.
- `src/AFK4.Operator.App.Web/src/players/ClientDrawer.tsx` + `.test.tsx` — проброс `onOpenDcTopUp`.
- `src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx` — state/wiring/рендер диалога, `walletReloadNonce`/`bumpWallet`.
- `src/AFK4.Operator.App.Web/src/styles/12-players.css` — минимальная вёрстка QR-экрана диалога (`.dc-topup-*`).
- `src/AFK4.Operator.App.Web/package.json`, `bun.lock` — `qrcode`/`@types/qrcode`.
- `locales/{ru,en,tg}.json`, `packages/i18n/src/messages.ts` (generated) — `op.dc.topup.*`.

## Self-review

- Money-path: `amountMinorUnits` идёт через `parseMoneyInputMinorUnits` (`@afk4/money`, тот же путь, что весь остальной проект) — не наивный `Number(x)*100` (плавающая точка, отсутствие валидации 2 знаков).
- Permission-guard: DC-кнопка и оба POS-эндпоинта гейтятся одним и тем же `TopUpWallet` — не завёл отдельное разрешение без нужды (бэкенд Task 6 тоже требует именно этот permission на create/cancel).
- Идемпотентность: `confirm` безопасен при повторном клике (intentId — ключ идемпотентности на бэке, уже закомментировано в клиенте).
- a11y: QR — `<img alt={t('op.dc.topup.hint')}>` (не пустой alt → доступен по `role=img`), кнопки — обычные `<button>` с текстовым содержимым.

## Concerns / отклонения от брифа

1. **`BackendPlayersWorkspace.tsx` не был в списке «Modify» брифа, но пришлось его тронуть.** Без этого либо (а) `ClientDrawer.tsx` пришлось бы наделять полным auth-контекстом и превращать в единственное исключение из паттерна «модалки открывает только оркестратор», либо (б) DC-пополнение осталось бы нерабочим (нечем собрать `dcTopUps`-клиент, некуда положить `walletReloadNonce`). Выбрал следовать существующему паттерну проекта (модалки — в оркестраторе), а не плодить новый. Кажется правильным, но раз бриф явно this не called out — фиксирую как отклонение.
2. **`op.dc.topup.cancel` текстуально дублирует `common.cancel`** («Отмена» и там, и там) — сохранил как отдельный ключ, т.к. бриф явно перечисляет `.cancel` в списке нужных ключей `op.dc.topup.*`, хотя технически можно было переиспользовать `common.cancel`.
3. **Header-X диалога и footer-кнопка «Отмена» имеют одинаковый accessible name** («Отмена» — X использует `t('common.cancel')` из `PanelModal`) — оба ведут к одному и тому же `cancelIntent()` (согласованное поведение: закрытие с pending-intent должно снимать его на бэке, иначе игрок может оплатить уже «закрытый» pay-link). В своём тесте на «Отмена» пришлось брать `getAllByRole(...)[last]`, а не полагаться на уникальный `name` — не баг, но стоит знать при написании будущих тестов на этот диалог.
4. Success-toast для DC-пополнения нет (только error-toast) — осознанный выбор (см. раздел «Диалог» выше), но это единственное money-действие в разделе Клиентов без success-toast; если сочтёте несогласованным — можно поднять `feedback`/`onCredited` в оркестратор аналогично прочим action, ценой более объёмного рефакторинга.

## FINAL-FIX (money-path review wave, ветка `feat/operator-dc-paylink-manual`)

Финальное ревью нашло два подтверждённых дефекта в переиспользуемом `POST /api/wallet/top-up-intents/{intentId}/fulfil` (общий для counter/eskhata/dc) + одну frontend-несогласованность. TDD: RED → GREEN → гейты.

### BLOCKER A — кредит после отмены (State="cancelled" проваливался сквозь чеки)

До фикса `fulfil` проверял только `State=="fulfilled"` (idempotent no-op) и `State=="pending"&&просрочен` (409). Любое другое состояние, включая `"cancelled"` (ставит `DcTopUpEndpoints.cancel`, `src/AFK4.Platform.Api/Endpoints/DcTopUpEndpoints.cs:96`), проваливалось сквозь оба чека и кредитовало кошелёк.

**Фикс:** `src/AFK4.Platform.Api/Endpoints/WalletEndpoints.cs:155-160` — сразу после expiry-guard добавлена явная проверка:
```csharp
if (intent.State != "pending")
{
    return Results.Conflict(new { Error = "Payment intent is not pending." });
}
```
Отклоняет `cancelled` (и любое иное не-`pending` состояние) 409-м ДО вызова `TopUpWalletAsync`.

### BLOCKER B — кредит неактивному игроку (guard отсутствовал вовсе)

`fulfil` не проверял активность игрока перед кредитом. Найден и применён реальный проектный хелпер `RejectInactivePlayerMoneyAction` (`src/AFK4.Platform.Api/Endpoints/EndpointHelpers.Loaders.cs:191-194`) — тот же, что используют `PlayerManagementEndpoints.cs:500/585/696`, `PackageEndpoints.cs:299`, `MoneyActionEndpoints.cs:434`. Возвращает `400 BadRequest {"Error":"Player account is inactive."}` при `IsActive==false`, иначе `null`.

**Фикс:** `src/AFK4.Platform.Api/Endpoints/WalletEndpoints.cs:162-172` — игрок подгружается через уже имеющийся `LoadPlayerForStaffAsync(dbContext, intent.PlayerAccountId, staffContext.OrganizationId, ct)` (тот же хелпер, что и `LoadPlayerScopedEndpointAsync` использует внутри), затем гард применяется перед сборкой `TopUpWalletRequest`.

Онлайн-топап webhook (Eskhata) через `fulfil` НЕ проходит отдельным путём — комментарий у `RejectInactivePlayerMoneyAction` («online top-up webhook намеренно не роутится сюда») относится к другому эндпоинту, не к этому; `fulfil` — общий кассирский confirm-путь для counter/eskhata/dc, гард корректен для всех трёх методов.

### RED-доказательство (до фикса, тесты добавлены в `tests/AFK4.Platform.Api.Tests/DcTopUpEndpointsTests.cs`)

- `Fulfil_CancelledIntent_DoesNotCreditWallet` — create → cancel (`State="cancelled"`) → fulfil. **До фикса:** `fulfil` вернул `200 OK` (кредит прошёл) — `Assert.NotEqual(HttpStatusCode.OK, ...)` упал: `Expected: Not OK / Actual: OK`.
- `Fulfil_InactivePlayer_DoesNotCreditWallet` — игрок создан с `IsActive=false`, create → fulfil. **До фикса:** тоже `200 OK` — тот же провал ассерта.

Оба теста прогнаны ДО правки бэкенда (`dotnet test --filter FullyQualifiedName~DcTopUpEndpointsTests`): `Failed: 2, Passed: 4` — обе дыры подтверждены прогоном, не домыслены.

### GREEN

После применения обоих гардов те же два теста проходят; полный прогон `dotnet test tests/AFK4.Platform.Api.Tests`: **Failed: 0, Passed: 1411, Skipped: 13** (включая счастливый путь `DcTopUpEndpointsTests.Confirm_ViaFulfil_CreditsWalletOnce`, `EskhataTopUpIntentTests`, `PortalWritesEndpointTests` — pending+активный игрок по-прежнему кредитуется ровно один раз).

### SOFT — success-тост в DcTopUpDialog

`src/AFK4.Operator.App.Web/src/players/DcTopUpDialog.tsx:107-110` — в `confirmReceived` перед `onCredited()+onClose()` добавлено:
```tsx
setFeedback({ label: t('op.dc.topup.feedbackLabel'), state: 'confirmed' });
```
Использован уже существующий ключ `op.dc.topup.feedbackLabel` (без нового i18n-ключа) — консистентно с `DcTransferForm.tsx:71` (`state: 'confirmed'` на той же метке).

RED: тест `показывает тост-подтверждение после успешного «Оплата получена»` (`src/AFK4.Operator.App.Web/src/players/DcTopUpDialog.test.tsx`) до фикса падал таймаутом (5000ms) — `findByText('DC-пополнение: подтверждено')` не находил ничего, диалог закрывался без тоста. GREEN: `bun test DcTopUpDialog.test.tsx` → **5 pass / 0 fail**.

### Гейты (все прогнаны реально, не предположены)

- `dotnet build src/AFK4.Platform.Api` → **0 Warning(s), 0 Error(s)**.
- `dotnet test tests/AFK4.Platform.Api.Tests` → **Failed: 0, Passed: 1411, Skipped: 13, Total: 1424**.
- `cd src/AFK4.Operator.App.Web && bun test DcTopUpDialog.test.tsx` → **5 pass / 0 fail**.
- `bun run build` → `tsc -b && vite build` → **✓ built in 525ms** (пред-существующие `INVALID_ANNOTATION`-warnings из `@microsoft/signalr` — шум rolldown, не ошибки, не связаны с правкой).

### Файлы

- `src/AFK4.Platform.Api/Endpoints/WalletEndpoints.cs` — оба backend-гарда (строки ~155-172).
- `tests/AFK4.Platform.Api.Tests/DcTopUpEndpointsTests.cs` — 2 новых RED→GREEN теста + seed-хелпер `SeedActiveConfigAndInactivePlayerAsync`.
- `src/AFK4.Operator.App.Web/src/players/DcTopUpDialog.tsx` — success-тост.
- `src/AFK4.Operator.App.Web/src/players/DcTopUpDialog.test.tsx` — новый тест на success-тост.

### Concerns

Нет. Обе дыры реальны, доказаны красным прогоном, закрыты правильным переиспользованным паттерном (не выдуманным). Онлайн-топап webhook сознательно не затронут (у него другой эндпоинт/путь, вне скоупа этой правки).
