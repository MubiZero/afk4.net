# Operator «Касса» S1 — вкладка «Смена» + жизненный цикл смены в шапке-якоре — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Вынести открытие/внесение/изъятие/закрытие смены из «Платежей» в шапку-якорь `CashShiftHeader` (кнопки + `PanelModal`-модалки) и собрать вкладку «Смена» (кокпит кассира: выручка + сверка + движения + история + экспорт) из `shiftRevenue`, схлопнув старые вкладки `payments`+`shifts` в одну `shift`.

**Architecture:** Шапка-якорь раздела «Касса» становится командной строкой смены: статус (S0, read-only) + командная панель `CashShiftCommandBar` (кнопки Открыть/Внести/Изъять/Закрыть → презентационные модалки по образцу `players/CorrectionModal`; оркестрация вызовов `shifts.*` — в командной панели). Новая вкладка «Смена» (`CashShiftWorkspace`) читает `shiftRevenue.current/history` + `getCashOperationReport` и показывает кокпит без форм-действий. Кросс-обновление шапки и вкладки после действия — через счётчик `shiftNonce` в `CashWorkspace`. Старые воркспейсы `BackendPaymentsWorkspace` и `ShiftsWorkspace` удаляются.

**Tech Stack:** React + TypeScript, `@afk4/i18n` (ICU), `@afk4/tokens`, bun test (happy-dom + jest-dom), Vite. Бэкенд не трогаем: используем готовые `shifts.openShift/recordCashMovement/closeShift` и `shiftRevenue.current/history`.

## Global Constraints

- **Деньги:** суммы в DTO — minor units; вывод только через `formatMoney(value, currencyCode)`; ввод парсится `parseMoneyInputMinorUnits` (знаковое) / `parseNonNegativeMoneyInputMinorUnits` (старт смены ≥ 0) из `./operatorHelpers`. Тип `MoneyDto = { currencyCode: string; minorUnits: number }`.
- **Бэкенд money-path не меняем**, новых миграций нет. Используем существующие клиенты `shifts` и `shiftRevenue`.
- **i18n:** каждый новый ключ — реально в ru/en/tg; **tg — настоящий таджикский**, не копия ru (guard `packages/i18n/src/messages.test.ts` падает на `tg===ru` вне whitelist). Термин «смена» в tg — **«навбат»** (как в `op.shifts.*`/`op.payments.*`), НЕ «баст». После правки локалей — регенерация `cd packages/i18n && bun run gen`.
- **Токены:** шкала `--space-1..6`, высоты `--control-sm/md/lg`; контролы ≥36px; цвета через токены (`--accent`, `--border-soft`, `--surface-elevated`, `--text-primary/secondary`). Зеркалим существующие паттерны (`.booking-header`, `.payments-*`, `.panel-modal`).
- **Модалки презентационные** (props: значения + `onChange*` + `onClose` + `onSubmit` + `busy`), реальные вызовы — в родителе-оркестраторе (паттерн `players/CorrectionModal.tsx`). Модалки оборачиваются в `PanelModal` (портал в body, Esc/клик-вне закрывают).
- **Идемпотентность:** каждый POST шлёт `idempotencyKey: createIdempotencyKey('<op>')`; запросы несут `organizationId: backend.session.organizationId`.
- **Права гейтят кнопки:** Открыть — `shifts.open`; Внести/Изъять — `shifts.cash.manage`; Закрыть — `shifts.close` (`permissionNames.openShift/manageShiftCash/closeShift`, проверка `hasPermission`).
- **Гейты (все зелёные перед мержем):**
  - фронт subdir + App.test: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run test`
  - фронт build (tsc+vite): `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run build`
  - i18n: `cd packages/i18n && /home/fedya/.bun/bin/bun run gen && /home/fedya/.bun/bin/bun test`
- **Тестовый бэкенд — фейковый** (`{ config: { platformBaseUrl: 'x' }, session: { accessToken: 't' }, branchId: 'b1' }`): `createAuthenticatedOperatorClients` на нём падает. Поэтому компоненты, строящие боевой клиент, **должны** принимать инъектируемый клиент и строить боевой только при `backend && !injected` (паттерн уже в `CashShiftHeader`/`ShiftsWorkspace`).

## Объём и решение по «остатку Платежей»

`BackendPaymentsWorkspace` сегодня — комбайн: (а) формы открытия/закрытия/внесения-изъятия; (б) сверка (ожидается/посчитано/расхождение); (в) полная лента операций (продажи+касса, с поиском) + панель «детали операции»; (г) сетка методов оплаты; (д) CSV-экспорты.

- **(а) → шапка-якорь** (командная панель + модалки).
- **(б), (д) → вкладка «Смена»** (сверка из `shiftRevenue.cash`; CSV-экспорты — 4 кнопки на готовых методах клиента).
- **(в) полная поисковая лента + (г) сетка методов → отложены в S2 «Журнал кассы»** (spec уже назначает ленту туда), помечены явно. Кокпит «Смены» вместо полной ленты показывает **последние 6 движений наличных** (видимость кассовых операций не теряется). Редизайн не задеплоен в prod (только staging) — реальной регрессии у пользователей нет; вынос ленты в S2 избегает двойной переделки.

`ShiftsWorkspace` (read-only выручка+история) поглощается кокпитом «Смены».

## File Structure

**Создаются:**
- `src/AFK4.Operator.App.Web/src/cash/OpenShiftModal.tsx` — презентационная модалка открытия смены (старт наличных + комментарий).
- `src/AFK4.Operator.App.Web/src/cash/CashMovementModal.tsx` — презентационная модалка внесения/изъятия (тип фиксирован кнопкой; сумма + причина).
- `src/AFK4.Operator.App.Web/src/cash/CloseShiftModal.tsx` — презентационная модалка закрытия (факт в кассе + комментарий + превью сверки ожидается/расхождение; tone="danger").
- `src/AFK4.Operator.App.Web/src/cash/CashShiftCommandBar.tsx` — командная панель: кнопки по статусу+правам, состояние активной модалки и форм, оркестрация `shifts.*`, feedback, `onShiftChanged`.
- `src/AFK4.Operator.App.Web/src/cash/CashShiftWorkspace.tsx` — вкладка «Смена»: кокпит (выручка+сверка из `shiftRevenue`) + последние движения + CSV-экспорты + история.
- Тесты: `cash/OpenShiftModal.test.tsx`, `cash/CashMovementModal.test.tsx`, `cash/CloseShiftModal.test.tsx`, `cash/CashShiftCommandBar.test.tsx`, `cash/CashShiftWorkspace.test.tsx`.

**Модифицируются:**
- `src/AFK4.Operator.App.Web/src/cash/CashShiftHeader.tsx` — рендерит `CashShiftCommandBar`; принимает `session`/`onShiftChanged`/`shiftNonce`; рефетч revenue на `shiftNonce`.
- `src/AFK4.Operator.App.Web/src/cash/CashShiftHeader.test.tsx` — обновить (новые пропсы; тест появления кнопки по праву).
- `src/AFK4.Operator.App.Web/src/cash/CashTabBar.tsx` — `CashTab`: `'payments' | 'shifts'` → `'shift'`.
- `src/AFK4.Operator.App.Web/src/cash/cashModel.ts` — `CASH_TAB_PERMISSIONS`/`CASH_TAB_ORDER`: `payments`+`shifts` → `shift` (права-объединение).
- `src/AFK4.Operator.App.Web/src/cash/cashModel.test.ts` — ожидания `visibleCashTabs`.
- `src/AFK4.Operator.App.Web/src/cash/CashWorkspace.tsx` — список вкладок; рендер `CashShiftWorkspace` для `shift`; `shiftNonce`; проброс `session`/`onShiftChanged` в шапку.
- `src/AFK4.Operator.App.Web/src/styles/21-cash.css` — стили командной панели, кокпита (модалки переиспользуют `.panel-modal`/`.clients-*-form`).
- `locales/{ru,en,tg}.json` + `packages/i18n/src/messages.ts` (регенерация) — новые `op.cash.*`; правка 2 tg-ключей S0 (баст→навбат).
- `src/AFK4.Operator.App.Web/src/App.test.tsx` — миграция сценариев, завязанных на «Платежи».

**Удаляются:**
- `src/AFK4.Operator.App.Web/src/BackendPaymentsWorkspace.tsx`
- `src/AFK4.Operator.App.Web/src/ShiftsWorkspace.tsx`
- `src/AFK4.Operator.App.Web/src/ShiftsWorkspace.test.tsx`

---

### Task 1: i18n — ключи `op.cash.*` для командной панели, модалок и кокпита

**Files:**
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json` (после строк блока `op.cash.metric.*`, ~941 в ru)
- Modify (regen): `packages/i18n/src/messages.ts`
- Test: `packages/i18n/src/messages.test.ts` (guard, существующий)

**Interfaces:**
- Produces: ключи `op.cash.action.*`, `op.cash.open.*`, `op.cash.movement.*`, `op.cash.close.*`, `op.cash.tab.shift`, `op.cash.shift.*` — потребляются Tasks 2–8 через `t('...')`.

- [ ] **Step 1: Добавить ключи в `locales/ru.json`** (вставить сразу после `"op.cash.metric.revenue": "Выручка",`):

```json
  "op.cash.action.open": "Открыть смену",
  "op.cash.action.cashIn": "Внести",
  "op.cash.action.cashOut": "Изъять",
  "op.cash.action.close": "Закрыть смену",
  "op.cash.open.title": "Открытие смены",
  "op.cash.open.subtitle": "старт наличных и комментарий",
  "op.cash.open.startingCashLabel": "Старт наличных",
  "op.cash.open.noteLabel": "Комментарий",
  "op.cash.open.submit": "Открыть смену",
  "op.cash.open.defaultNote": "Утренняя смена",
  "op.cash.movement.titleIn": "Внесение наличных",
  "op.cash.movement.titleOut": "Изъятие наличных",
  "op.cash.movement.subtitle": "сумма и причина",
  "op.cash.movement.amountLabel": "Сумма",
  "op.cash.movement.reasonLabel": "Причина",
  "op.cash.movement.submit": "Подтвердить",
  "op.cash.movement.defaultReason": "Размен кассы",
  "op.cash.close.title": "Закрытие смены",
  "op.cash.close.subtitle": "сверка наличных",
  "op.cash.close.countedLabel": "Факт в кассе",
  "op.cash.close.noteLabel": "Комментарий",
  "op.cash.close.expected": "Ожидается",
  "op.cash.close.difference": "Расхождение",
  "op.cash.close.impact": "После подтверждения смена будет закрыта, новые продажи потребуют открытия следующей смены.",
  "op.cash.close.submit": "Закрыть смену",
  "op.cash.close.defaultNote": "Закрытие смены",
  "op.cash.tab.shift": "Смена",
  "op.cash.shift.revenueTitle": "Выручка смены",
  "op.cash.shift.reconcileTitle": "Сверка кассы",
  "op.cash.shift.starting": "Старт наличных",
  "op.cash.shift.expected": "Ожидается",
  "op.cash.shift.counted": "Посчитано",
  "op.cash.shift.difference": "Расхождение",
  "op.cash.shift.notClosed": "Смена не закрыта",
  "op.cash.shift.movementsTitle": "Движение наличных",
  "op.cash.shift.movementsEmpty": "Движений нет",
  "op.cash.shift.historyTitle": "История смен",
  "op.cash.shift.historyEmpty": "Закрытых смен нет",
  "op.cash.shift.exportTitle": "Экспорт",
  "op.cash.shift.exportShiftSummary": "Сводка смены",
  "op.cash.shift.exportCashMovements": "Движение наличных",
  "op.cash.shift.exportReceipts": "Список чеков",
  "op.cash.shift.exportReconciliation": "Сверка кассы",
  "op.cash.shift.loadError": "Не удалось загрузить смену.",
  "op.cash.shift.empty": "Нет открытой смены",
```

- [ ] **Step 2: Добавить те же ключи в `locales/en.json`** (после `"op.cash.metric.revenue": "Revenue",`):

```json
  "op.cash.action.open": "Open shift",
  "op.cash.action.cashIn": "Cash in",
  "op.cash.action.cashOut": "Cash out",
  "op.cash.action.close": "Close shift",
  "op.cash.open.title": "Open shift",
  "op.cash.open.subtitle": "starting cash and note",
  "op.cash.open.startingCashLabel": "Starting cash",
  "op.cash.open.noteLabel": "Note",
  "op.cash.open.submit": "Open shift",
  "op.cash.open.defaultNote": "Morning shift",
  "op.cash.movement.titleIn": "Cash in",
  "op.cash.movement.titleOut": "Cash out",
  "op.cash.movement.subtitle": "amount and reason",
  "op.cash.movement.amountLabel": "Amount",
  "op.cash.movement.reasonLabel": "Reason",
  "op.cash.movement.submit": "Confirm",
  "op.cash.movement.defaultReason": "Cash exchange",
  "op.cash.close.title": "Close shift",
  "op.cash.close.subtitle": "cash reconciliation",
  "op.cash.close.countedLabel": "Actual cash",
  "op.cash.close.noteLabel": "Note",
  "op.cash.close.expected": "Expected",
  "op.cash.close.difference": "Difference",
  "op.cash.close.impact": "After confirmation the shift is closed; new sales require opening the next shift.",
  "op.cash.close.submit": "Close shift",
  "op.cash.close.defaultNote": "Shift close",
  "op.cash.tab.shift": "Shift",
  "op.cash.shift.revenueTitle": "Shift revenue",
  "op.cash.shift.reconcileTitle": "Cash reconciliation",
  "op.cash.shift.starting": "Starting cash",
  "op.cash.shift.expected": "Expected",
  "op.cash.shift.counted": "Counted",
  "op.cash.shift.difference": "Difference",
  "op.cash.shift.notClosed": "Shift not closed",
  "op.cash.shift.movementsTitle": "Cash movements",
  "op.cash.shift.movementsEmpty": "No movements",
  "op.cash.shift.historyTitle": "Shift history",
  "op.cash.shift.historyEmpty": "No closed shifts",
  "op.cash.shift.exportTitle": "Export",
  "op.cash.shift.exportShiftSummary": "Shift summary",
  "op.cash.shift.exportCashMovements": "Cash movements",
  "op.cash.shift.exportReceipts": "Receipt list",
  "op.cash.shift.exportReconciliation": "Reconciliation",
  "op.cash.shift.loadError": "Failed to load the shift.",
  "op.cash.shift.empty": "No open shift",
```

- [ ] **Step 3: Добавить те же ключи в `locales/tg.json`** (после `"op.cash.metric.revenue": "Даромад",`) — настоящий таджикский, термин «навбат»:

```json
  "op.cash.action.open": "Кушодани навбат",
  "op.cash.action.cashIn": "Ворид кардан",
  "op.cash.action.cashOut": "Хориҷ кардан",
  "op.cash.action.close": "Бастани навбат",
  "op.cash.open.title": "Кушодани навбат",
  "op.cash.open.subtitle": "нақди ибтидоӣ ва шарҳ",
  "op.cash.open.startingCashLabel": "Нақди ибтидоӣ",
  "op.cash.open.noteLabel": "Шарҳ",
  "op.cash.open.submit": "Кушодани навбат",
  "op.cash.open.defaultNote": "Навбати субҳ",
  "op.cash.movement.titleIn": "Ворид кардани нақд",
  "op.cash.movement.titleOut": "Хориҷ кардани нақд",
  "op.cash.movement.subtitle": "маблағ ва сабаб",
  "op.cash.movement.amountLabel": "Маблағ",
  "op.cash.movement.reasonLabel": "Сабаб",
  "op.cash.movement.submit": "Тасдиқ",
  "op.cash.movement.defaultReason": "Ивази хурда",
  "op.cash.close.title": "Бастани навбат",
  "op.cash.close.subtitle": "санҷиши нақд",
  "op.cash.close.countedLabel": "Нақди воқеӣ",
  "op.cash.close.noteLabel": "Шарҳ",
  "op.cash.close.expected": "Интизор",
  "op.cash.close.difference": "Фарқият",
  "op.cash.close.impact": "Пас аз тасдиқ навбат баста мешавад; фурӯши нав кушодани навбати навро талаб мекунад.",
  "op.cash.close.submit": "Бастани навбат",
  "op.cash.close.defaultNote": "Бастани навбат",
  "op.cash.tab.shift": "Навбат",
  "op.cash.shift.revenueTitle": "Даромади навбат",
  "op.cash.shift.reconcileTitle": "Санҷиши касса",
  "op.cash.shift.starting": "Нақди ибтидоӣ",
  "op.cash.shift.expected": "Интизор",
  "op.cash.shift.counted": "Ҳисобшуда",
  "op.cash.shift.difference": "Фарқият",
  "op.cash.shift.notClosed": "Навбат баста нашудааст",
  "op.cash.shift.movementsTitle": "Ҳаракати нақд",
  "op.cash.shift.movementsEmpty": "Ҳаракат нест",
  "op.cash.shift.historyTitle": "Таърихи навбатҳо",
  "op.cash.shift.historyEmpty": "Навбатҳои баста нест",
  "op.cash.shift.exportTitle": "Содирот",
  "op.cash.shift.exportShiftSummary": "Хулосаи навбат",
  "op.cash.shift.exportCashMovements": "Ҳаракати нақд",
  "op.cash.shift.exportReceipts": "Рӯйхати чекҳо",
  "op.cash.shift.exportReconciliation": "Санҷиши касса",
  "op.cash.shift.loadError": "Навбатро бор карда нашуд.",
  "op.cash.shift.empty": "Навбати кушода нест",
```

- [ ] **Step 4: Привести tg-ключи смены S0 к термину «навбат»** (правка консистентности — «баст» выпадает из общего словаря). В `locales/tg.json`:
  - `"op.cash.header.open": "Баст кушода"` → `"op.cash.header.open": "Навбат кушода"`
  - `"op.cash.header.closed": "Басти кушода нест"` → `"op.cash.header.closed": "Навбати кушода нест"`

  **NB исполнителю:** это изменит ожидание в `CashShiftHeader.test.tsx`, который рендерит `initialLocale="ru"` (а не tg), поэтому ru-тест НЕ затронут. Если есть отдельный tg-тест на эти строки — обновить. (Поиск: `rg "Баст кушода" src/`.)

- [ ] **Step 5: Регенерация и проверка guard**

Run: `cd packages/i18n && /home/fedya/.bun/bin/bun run gen && /home/fedya/.bun/bin/bun test`
Expected: PASS. Guard `tg !== ru` зелёный (все новые tg-значения отличаются от ru). `messages.ts` содержит новые ключи.

- [ ] **Step 6: Commit**

```bash
git add locales/ru.json locales/en.json locales/tg.json packages/i18n/src/messages.ts
git commit -m "i18n(cash-s1): ключи op.cash.* для смены (действия, модалки, кокпит) + tg баст→навбат"
```

---

### Task 2: `OpenShiftModal` — модалка открытия смены

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/cash/OpenShiftModal.tsx`
- Test: `src/AFK4.Operator.App.Web/src/cash/OpenShiftModal.test.tsx`

**Interfaces:**
- Consumes: `PanelModal` из `../PanelModal`; ключи `op.cash.open.*` (Task 1).
- Produces: `OpenShiftModal` props `{ startingCash: string; note: string; onChangeStartingCash: (v: string) => void; onChangeNote: (v: string) => void; onClose: () => void; onSubmit: () => void; busy: boolean }`. Потребляется `CashShiftCommandBar` (Task 5).

- [ ] **Step 1: Тест** `cash/OpenShiftModal.test.tsx`:

```tsx
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { OpenShiftModal } from './OpenShiftModal';

afterEach(cleanup);

function renderModal(overrides: Partial<Parameters<typeof OpenShiftModal>[0]> = {}) {
  const onSubmit = mock(() => {});
  const onClose = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <OpenShiftModal
        startingCash="150.00"
        note="Утренняя смена"
        onChangeStartingCash={() => {}}
        onChangeNote={() => {}}
        onClose={onClose}
        onSubmit={onSubmit}
        busy={false}
        {...overrides}
      />
    </I18nProvider>
  );
  return { onSubmit, onClose };
}

describe('OpenShiftModal', () => {
  it('рендерит поля старта наличных и комментария', () => {
    renderModal();
    expect(screen.getByLabelText('Старт наличных')).toHaveValue('150.00');
    expect(screen.getByLabelText('Комментарий')).toHaveValue('Утренняя смена');
  });

  it('submit формы вызывает onSubmit', () => {
    const { onSubmit } = renderModal();
    fireEvent.click(screen.getByRole('button', { name: 'Открыть смену' }));
    expect(onSubmit).toHaveBeenCalledTimes(1);
  });

  it('busy дизейблит сабмит', () => {
    renderModal({ busy: true });
    expect(screen.getByRole('button', { name: 'Открыть смену' })).toBeDisabled();
  });
});
```

- [ ] **Step 2: Прогон — падает** (нет файла).

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/OpenShiftModal.test.tsx`
Expected: FAIL (cannot find `./OpenShiftModal`).

- [ ] **Step 3: Реализация** `cash/OpenShiftModal.tsx`:

```tsx
import { useI18n } from '@afk4/i18n';
import { Unlock } from 'lucide-react';
import { PanelModal } from '../PanelModal';

// Презентационная модалка открытия смены: старт наличных + комментарий. Реальный вызов
// shifts.openShift — в оркестраторе (CashShiftCommandBar).
export function OpenShiftModal({
  startingCash,
  note,
  onChangeStartingCash,
  onChangeNote,
  onClose,
  onSubmit,
  busy
}: {
  startingCash: string;
  note: string;
  onChangeStartingCash: (value: string) => void;
  onChangeNote: (value: string) => void;
  onClose: () => void;
  onSubmit: () => void;
  busy: boolean;
}) {
  const { t } = useI18n();
  return (
    <PanelModal title={t('op.cash.open.title')} subtitle={t('op.cash.open.subtitle')} onClose={onClose}>
      <form
        className="cash-shift-form"
        onSubmit={(event) => {
          event.preventDefault();
          onSubmit();
        }}
      >
        <label htmlFor="open-shift-cash">{t('op.cash.open.startingCashLabel')}</label>
        <input
          id="open-shift-cash"
          inputMode="decimal"
          value={startingCash}
          disabled={busy}
          onChange={(event) => onChangeStartingCash(event.currentTarget.value)}
        />
        <label htmlFor="open-shift-note">{t('op.cash.open.noteLabel')}</label>
        <input
          id="open-shift-note"
          value={note}
          disabled={busy}
          onChange={(event) => onChangeNote(event.currentTarget.value)}
        />
        <button type="submit" className="cash-primary-action" disabled={busy}>
          <Unlock size={15} aria-hidden="true" />
          {t('op.cash.open.submit')}
        </button>
      </form>
    </PanelModal>
  );
}
```

- [ ] **Step 4: Прогон — зелёный.**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/OpenShiftModal.test.tsx`
Expected: PASS (3/3).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/cash/OpenShiftModal.tsx src/AFK4.Operator.App.Web/src/cash/OpenShiftModal.test.tsx
git commit -m "feat(cash-s1): OpenShiftModal — модалка открытия смены"
```

---

### Task 3: `CashMovementModal` — модалка внесения/изъятия наличных

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/cash/CashMovementModal.tsx`
- Test: `src/AFK4.Operator.App.Web/src/cash/CashMovementModal.test.tsx`

**Interfaces:**
- Consumes: `PanelModal`; ключи `op.cash.movement.*` (Task 1).
- Produces: `CashMovementModal` props `{ movementType: 'cash_in' | 'cash_out'; amount: string; reason: string; onChangeAmount: (v: string) => void; onChangeReason: (v: string) => void; onClose: () => void; onSubmit: () => void; busy: boolean }`. Тип фиксирован кнопкой, заголовок зависит от типа. Потребляется `CashShiftCommandBar` (Task 5).

- [ ] **Step 1: Тест** `cash/CashMovementModal.test.tsx`:

```tsx
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { CashMovementModal } from './CashMovementModal';

afterEach(cleanup);

function renderModal(overrides: Partial<Parameters<typeof CashMovementModal>[0]> = {}) {
  const onSubmit = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <CashMovementModal
        movementType="cash_in"
        amount="10.00"
        reason="Размен кассы"
        onChangeAmount={() => {}}
        onChangeReason={() => {}}
        onClose={() => {}}
        onSubmit={onSubmit}
        busy={false}
        {...overrides}
      />
    </I18nProvider>
  );
  return { onSubmit };
}

describe('CashMovementModal', () => {
  it('тип cash_in → заголовок «Внесение наличных»', () => {
    renderModal({ movementType: 'cash_in' });
    expect(screen.getByText('Внесение наличных')).toBeInTheDocument();
  });

  it('тип cash_out → заголовок «Изъятие наличных»', () => {
    renderModal({ movementType: 'cash_out' });
    expect(screen.getByText('Изъятие наличных')).toBeInTheDocument();
  });

  it('submit вызывает onSubmit', () => {
    const { onSubmit } = renderModal();
    fireEvent.click(screen.getByRole('button', { name: 'Подтвердить' }));
    expect(onSubmit).toHaveBeenCalledTimes(1);
  });
});
```

- [ ] **Step 2: Прогон — падает.**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/CashMovementModal.test.tsx`
Expected: FAIL (cannot find `./CashMovementModal`).

- [ ] **Step 3: Реализация** `cash/CashMovementModal.tsx`:

```tsx
import { useI18n } from '@afk4/i18n';
import { ArrowDownToLine, ArrowUpFromLine } from 'lucide-react';
import { PanelModal } from '../PanelModal';

// Презентационная модалка кассового движения. Направление (внесение/изъятие) задаётся снаружи
// кнопкой и неизменно внутри модалки. Реальный вызов shifts.recordCashMovement — в оркестраторе.
export function CashMovementModal({
  movementType,
  amount,
  reason,
  onChangeAmount,
  onChangeReason,
  onClose,
  onSubmit,
  busy
}: {
  movementType: 'cash_in' | 'cash_out';
  amount: string;
  reason: string;
  onChangeAmount: (value: string) => void;
  onChangeReason: (value: string) => void;
  onClose: () => void;
  onSubmit: () => void;
  busy: boolean;
}) {
  const { t } = useI18n();
  const isIn = movementType === 'cash_in';
  const Icon = isIn ? ArrowDownToLine : ArrowUpFromLine;
  return (
    <PanelModal
      title={isIn ? t('op.cash.movement.titleIn') : t('op.cash.movement.titleOut')}
      subtitle={t('op.cash.movement.subtitle')}
      onClose={onClose}
    >
      <form
        className="cash-shift-form"
        onSubmit={(event) => {
          event.preventDefault();
          onSubmit();
        }}
      >
        <label htmlFor="cash-movement-amount">{t('op.cash.movement.amountLabel')}</label>
        <input
          id="cash-movement-amount"
          inputMode="decimal"
          value={amount}
          disabled={busy}
          onChange={(event) => onChangeAmount(event.currentTarget.value)}
        />
        <label htmlFor="cash-movement-reason">{t('op.cash.movement.reasonLabel')}</label>
        <input
          id="cash-movement-reason"
          value={reason}
          disabled={busy}
          onChange={(event) => onChangeReason(event.currentTarget.value)}
        />
        <button type="submit" className="cash-primary-action" disabled={busy}>
          <Icon size={15} aria-hidden="true" />
          {t('op.cash.movement.submit')}
        </button>
      </form>
    </PanelModal>
  );
}
```

- [ ] **Step 4: Прогон — зелёный.**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/CashMovementModal.test.tsx`
Expected: PASS (3/3).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/cash/CashMovementModal.tsx src/AFK4.Operator.App.Web/src/cash/CashMovementModal.test.tsx
git commit -m "feat(cash-s1): CashMovementModal — внесение/изъятие наличных"
```

---

### Task 4: `CloseShiftModal` — модалка закрытия смены со сверкой

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/cash/CloseShiftModal.tsx`
- Test: `src/AFK4.Operator.App.Web/src/cash/CloseShiftModal.test.tsx`

**Interfaces:**
- Consumes: `PanelModal`; `formatMoney`, `parseMoneyInputMinorUnits` из `../operatorHelpers`; ключи `op.cash.close.*` (Task 1). `MoneyDto = { currencyCode: string; minorUnits: number }`.
- Produces: `CloseShiftModal` props `{ expectedCash: { currencyCode: string; minorUnits: number } | null; counted: string; note: string; currencyCode: string; onChangeCounted: (v: string) => void; onChangeNote: (v: string) => void; onClose: () => void; onSubmit: () => void; busy: boolean }`. Превью расхождения = посчитано − ожидается (живо, при валидном вводе). Потребляется `CashShiftCommandBar` (Task 5).

- [ ] **Step 1: Тест** `cash/CloseShiftModal.test.tsx`:

```tsx
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { CloseShiftModal } from './CloseShiftModal';

afterEach(cleanup);

function renderModal(overrides: Partial<Parameters<typeof CloseShiftModal>[0]> = {}) {
  const onSubmit = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <CloseShiftModal
        expectedCash={{ currencyCode: 'TJS', minorUnits: 11500 }}
        counted="120.00"
        note="Закрытие смены"
        currencyCode="TJS"
        onChangeCounted={() => {}}
        onChangeNote={() => {}}
        onClose={() => {}}
        onSubmit={onSubmit}
        busy={false}
        {...overrides}
      />
    </I18nProvider>
  );
  return { onSubmit };
}

describe('CloseShiftModal', () => {
  it('показывает ожидаемую сумму', () => {
    renderModal();
    expect(screen.getByText('Ожидается')).toBeInTheDocument();
    expect(screen.getByText('115,00 TJS')).toBeInTheDocument();
  });

  it('считает расхождение факт − ожидается (120 − 115 = +5,00 TJS)', () => {
    renderModal();
    // 12000 − 11500 = 500 minor = 5,00 TJS
    expect(screen.getByText('5,00 TJS')).toBeInTheDocument();
  });

  it('submit вызывает onSubmit', () => {
    const { onSubmit } = renderModal();
    fireEvent.click(screen.getByRole('button', { name: 'Закрыть смену' }));
    expect(onSubmit).toHaveBeenCalledTimes(1);
  });
});
```

**NB исполнителю:** `formatMoney` форматирует minor units по локали ru как `115,00 TJS` (пробел-разделитель, запятая-десятичная). Если фактический формат отличается (узнать прогоном Step 2 после реализации) — поправить ожидаемые строки теста под реальный вывод `formatMoney`, не наоборот.

- [ ] **Step 2: Прогон — падает.**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/CloseShiftModal.test.tsx`
Expected: FAIL (cannot find `./CloseShiftModal`).

- [ ] **Step 3: Реализация** `cash/CloseShiftModal.tsx`:

```tsx
import { useI18n } from '@afk4/i18n';
import { Lock } from 'lucide-react';
import { PanelModal } from '../PanelModal';
import { formatMoney, parseMoneyInputMinorUnits } from '../operatorHelpers';

// Презентационная модалка закрытия смены со сверкой. Превью расхождения = факт − ожидается,
// считается живо при валидном вводе. Критичное действие (tone="danger"). Реальный вызов — снаружи.
export function CloseShiftModal({
  expectedCash,
  counted,
  note,
  currencyCode,
  onChangeCounted,
  onChangeNote,
  onClose,
  onSubmit,
  busy
}: {
  expectedCash: { currencyCode: string; minorUnits: number } | null;
  counted: string;
  note: string;
  currencyCode: string;
  onChangeCounted: (value: string) => void;
  onChangeNote: (value: string) => void;
  onClose: () => void;
  onSubmit: () => void;
  busy: boolean;
}) {
  const { t } = useI18n();
  const countedMinor = parseMoneyInputMinorUnits(counted);
  const difference =
    countedMinor === null || expectedCash === null
      ? null
      : { currencyCode, minorUnits: countedMinor - expectedCash.minorUnits };

  return (
    <PanelModal title={t('op.cash.close.title')} subtitle={t('op.cash.close.subtitle')} onClose={onClose} tone="danger">
      <form
        className="cash-shift-form"
        onSubmit={(event) => {
          event.preventDefault();
          onSubmit();
        }}
      >
        <div className="cash-close-reconcile">
          <div><span>{t('op.cash.close.expected')}</span><strong>{formatMoney(expectedCash, currencyCode)}</strong></div>
          <div className={difference && difference.minorUnits !== 0 ? 'attention' : undefined}>
            <span>{t('op.cash.close.difference')}</span>
            <strong>{formatMoney(difference, currencyCode)}</strong>
          </div>
        </div>
        <label htmlFor="close-shift-counted">{t('op.cash.close.countedLabel')}</label>
        <input
          id="close-shift-counted"
          inputMode="decimal"
          value={counted}
          disabled={busy}
          onChange={(event) => onChangeCounted(event.currentTarget.value)}
        />
        <label htmlFor="close-shift-note">{t('op.cash.close.noteLabel')}</label>
        <input
          id="close-shift-note"
          value={note}
          disabled={busy}
          onChange={(event) => onChangeNote(event.currentTarget.value)}
        />
        <p className="cash-close-impact">{t('op.cash.close.impact')}</p>
        <button type="submit" className="cash-primary-action danger" disabled={busy}>
          <Lock size={15} aria-hidden="true" />
          {t('op.cash.close.submit')}
        </button>
      </form>
    </PanelModal>
  );
}
```

- [ ] **Step 4: Прогон — зелёный.** При расхождении формата денег — поправить тест под реальный вывод `formatMoney`.

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/CloseShiftModal.test.tsx`
Expected: PASS (3/3).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/cash/CloseShiftModal.tsx src/AFK4.Operator.App.Web/src/cash/CloseShiftModal.test.tsx
git commit -m "feat(cash-s1): CloseShiftModal — закрытие смены со сверкой"
```

---

### Task 5: `CashShiftCommandBar` — командная панель смены (оркестрация)

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/cash/CashShiftCommandBar.tsx`
- Test: `src/AFK4.Operator.App.Web/src/cash/CashShiftCommandBar.test.tsx`

**Interfaces:**
- Consumes: `OpenShiftModal`/`CashMovementModal`/`CloseShiftModal` (Tasks 2–4); `createAuthenticatedOperatorClients`, `createIdempotencyKey`, `parseMoneyInputMinorUnits`, `parseNonNegativeMoneyInputMinorUnits` из `../operatorHelpers`; `FeedbackNotice` из `../operatorPrimitives`; `hasPermission`, `permissionNames` из `../operatorPermissions`; типы `OperatorBackendContext` (`../operatorTypes`), `OperatorAuthSession` (`../authClient`), `Feedback`/`emptyFeedback`; `OpenShiftRequest`/`RecordCashMovementRequest`/`CloseShiftRequest` из `../api/clients/shifts`.
- Produces: `CashShiftCommandBar` props `{ backend: OperatorBackendContext | null; session: OperatorAuthSession | null; shiftId: string | null; isOpen: boolean; expectedCash: { currencyCode: string; minorUnits: number } | null; currencyCode: string; onShiftChanged: () => void; actions?: CashShiftActionsClient }`. Экспортирует `interface CashShiftActionsClient { openShift; recordCashMovement; closeShift }`. Потребляется `CashShiftHeader` (Task 6).

**Контракт правок:** оркестрация по образцу `BackendPaymentsWorkspace.runReportAction` (try/catch, `projectOperatorError`, idempotency, `organizationId`). Боевой клиент строим только при `backend && !actions` (фейк-backend в тестах ломает `createAuthenticatedOperatorClients`).

- [ ] **Step 1: Тест** `cash/CashShiftCommandBar.test.tsx`:

```tsx
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { CashShiftCommandBar, type CashShiftActionsClient } from './CashShiftCommandBar';
import type { OperatorAuthSession } from '../authClient';

afterEach(cleanup);

const backend = { config: { platformBaseUrl: 'x' }, session: { accessToken: 't', organizationId: 'org1' }, branchId: 'b1' } as never;
const allPerms = ['shifts.open', 'shifts.close', 'shifts.cash.manage'];
const session = (perms: string[]) => ({ permissions: perms, organizationId: 'org1' } as unknown as OperatorAuthSession);

function fakeActions(): CashShiftActionsClient & { calls: Record<string, unknown[]> } {
  const calls: Record<string, unknown[]> = { open: [], movement: [], close: [] };
  return {
    calls,
    openShift: mock(async (branchId: string, request: unknown) => { calls.open.push({ branchId, request }); return {}; }),
    recordCashMovement: mock(async (shiftId: string, request: unknown) => { calls.movement.push({ shiftId, request }); return {}; }),
    closeShift: mock(async (shiftId: string, request: unknown) => { calls.close.push({ shiftId, request }); return {}; })
  };
}

function renderBar(opts: { isOpen: boolean; perms?: string[]; actions?: CashShiftActionsClient; onShiftChanged?: () => void }) {
  render(
    <I18nProvider initialLocale="ru">
      <CashShiftCommandBar
        backend={backend}
        session={session(opts.perms ?? allPerms)}
        shiftId={opts.isOpen ? 's1' : null}
        isOpen={opts.isOpen}
        expectedCash={{ currencyCode: 'TJS', minorUnits: 11500 }}
        currencyCode="TJS"
        onShiftChanged={opts.onShiftChanged ?? (() => {})}
        actions={opts.actions}
      />
    </I18nProvider>
  );
}

describe('CashShiftCommandBar', () => {
  it('закрытая смена → только кнопка «Открыть смену»', () => {
    renderBar({ isOpen: false });
    expect(screen.getByRole('button', { name: 'Открыть смену' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Внести' })).not.toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Закрыть смену' })).not.toBeInTheDocument();
  });

  it('открытая смена → Внести/Изъять/Закрыть, без «Открыть»', () => {
    renderBar({ isOpen: true });
    expect(screen.getByRole('button', { name: 'Внести' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Изъять' })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: 'Закрыть смену' })).toBeInTheDocument();
    expect(screen.queryByRole('button', { name: 'Открыть смену' })).not.toBeInTheDocument();
  });

  it('права гейтят кнопки: без shifts.open закрытая смена не даёт «Открыть»', () => {
    renderBar({ isOpen: false, perms: [] });
    expect(screen.queryByRole('button', { name: 'Открыть смену' })).not.toBeInTheDocument();
  });

  it('открытие смены: модалка → submit → openShift с payload + onShiftChanged', async () => {
    const actions = fakeActions();
    const onShiftChanged = mock(() => {});
    renderBar({ isOpen: false, actions, onShiftChanged });
    fireEvent.click(screen.getByRole('button', { name: 'Открыть смену' }));
    // в модалке поля предзаполнены; меняем старт наличных
    fireEvent.change(screen.getByLabelText('Старт наличных'), { target: { value: '150.00' } });
    fireEvent.click(screen.getByRole('button', { name: 'Открыть смену' }));
    await waitFor(() => expect(actions.calls.open.length).toBe(1));
    const { branchId, request } = actions.calls.open[0] as { branchId: string; request: Record<string, unknown> };
    expect(branchId).toBe('b1');
    expect(request).toMatchObject({ organizationId: 'org1', startingCash: { currencyCode: 'TJS', minorUnits: 15000 } });
    expect(String(request.idempotencyKey)).toMatch(/^shift-open-/);
    await waitFor(() => expect(onShiftChanged).toHaveBeenCalledTimes(1));
  });

  it('закрытие смены: модалка → submit → closeShift с countedCash', async () => {
    const actions = fakeActions();
    renderBar({ isOpen: true, actions });
    fireEvent.click(screen.getByRole('button', { name: 'Закрыть смену' }));
    fireEvent.change(screen.getByLabelText('Факт в кассе'), { target: { value: '115.00' } });
    fireEvent.click(screen.getByRole('button', { name: 'Закрыть смену' }));
    await waitFor(() => expect(actions.calls.close.length).toBe(1));
    const { shiftId, request } = actions.calls.close[0] as { shiftId: string; request: Record<string, unknown> };
    expect(shiftId).toBe('s1');
    expect(request).toMatchObject({ countedCash: { currencyCode: 'TJS', minorUnits: 11500 } });
  });
});
```

- [ ] **Step 2: Прогон — падает.**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/CashShiftCommandBar.test.tsx`
Expected: FAIL (cannot find `./CashShiftCommandBar`).

- [ ] **Step 3: Реализация** `cash/CashShiftCommandBar.tsx`:

```tsx
import { useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { Lock, ArrowDownToLine, ArrowUpFromLine, Unlock } from 'lucide-react';
import {
  createAuthenticatedOperatorClients,
  createIdempotencyKey,
  parseMoneyInputMinorUnits,
  parseNonNegativeMoneyInputMinorUnits
} from '../operatorHelpers';
import { projectOperatorError } from '../apiErrors';
import { FeedbackNotice } from '../operatorPrimitives';
import { hasPermission, permissionNames } from '../operatorPermissions';
import type { OperatorBackendContext, Feedback } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import type { OpenShiftRequest, RecordCashMovementRequest, CloseShiftRequest } from '../api/clients/shifts';
import { OpenShiftModal } from './OpenShiftModal';
import { CashMovementModal } from './CashMovementModal';
import { CloseShiftModal } from './CloseShiftModal';

export interface CashShiftActionsClient {
  openShift(branchId: string, request: OpenShiftRequest): Promise<unknown>;
  recordCashMovement(shiftId: string, request: RecordCashMovementRequest): Promise<unknown>;
  closeShift(shiftId: string, request: CloseShiftRequest): Promise<unknown>;
}

type ActiveModal = 'open' | 'cash_in' | 'cash_out' | 'close' | null;

// Командная панель смены в шапке-якоре: кнопки по статусу+правам, модалки и оркестрация
// shifts.* (idempotency, feedback). После успеха зовёт onShiftChanged → раздел перечитывает смену.
export function CashShiftCommandBar({
  backend,
  session,
  shiftId,
  isOpen,
  expectedCash,
  currencyCode,
  onShiftChanged,
  actions: injectedActions
}: {
  backend: OperatorBackendContext | null;
  session: OperatorAuthSession | null;
  shiftId: string | null;
  isOpen: boolean;
  expectedCash: { currencyCode: string; minorUnits: number } | null;
  currencyCode: string;
  onShiftChanged: () => void;
  actions?: CashShiftActionsClient;
}) {
  const { t } = useI18n();
  const realActions = useMemo(
    () => (backend && !injectedActions ? createAuthenticatedOperatorClients(backend.config, backend.session).shifts : null),
    [backend?.config, backend?.session, injectedActions]
  );
  const actions = injectedActions ?? realActions;

  const [activeModal, setActiveModal] = useState<ActiveModal>(null);
  const [busy, setBusy] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>({ label: '', state: 'idle' });
  const [startingCash, setStartingCash] = useState('0.00');
  const [openingNote, setOpeningNote] = useState(t('op.cash.open.defaultNote'));
  const [movementAmount, setMovementAmount] = useState('10.00');
  const [movementReason, setMovementReason] = useState(t('op.cash.movement.defaultReason'));
  const [countedCash, setCountedCash] = useState('');
  const [closingNote, setClosingNote] = useState(t('op.cash.close.defaultNote'));

  const canOpen = !isOpen && hasPermission(session, permissionNames.openShift);
  const canCash = isOpen && hasPermission(session, permissionNames.manageShiftCash);
  const canClose = isOpen && hasPermission(session, permissionNames.closeShift);

  const run = async (label: string, fn: () => Promise<void>) => {
    if (actions === null || backend === null) return;
    setBusy(true);
    setFeedback({ label, state: 'pending' });
    try {
      await fn();
      setActiveModal(null);
      setFeedback({ label, state: 'confirmed' });
      onShiftChanged();
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setBusy(false);
    }
  };

  const submitOpen = () =>
    run(t('op.cash.action.open'), async () => {
      const minor = parseNonNegativeMoneyInputMinorUnits(startingCash);
      if (minor === null) throw new Error(t('op.cash.open.startingCashLabel'));
      await actions!.openShift(backend!.branchId, {
        organizationId: backend!.session.organizationId,
        startingCash: { currencyCode, minorUnits: minor },
        openingNote: openingNote.trim(),
        idempotencyKey: createIdempotencyKey('shift-open')
      });
    });

  const submitMovement = (movementType: 'cash_in' | 'cash_out') => () =>
    run(movementType === 'cash_in' ? t('op.cash.movement.titleIn') : t('op.cash.movement.titleOut'), async () => {
      const minor = parseMoneyInputMinorUnits(movementAmount);
      const reason = movementReason.trim();
      if (minor === null || !reason || shiftId === null) throw new Error(t('op.cash.movement.amountLabel'));
      await actions!.recordCashMovement(shiftId, {
        organizationId: backend!.session.organizationId,
        movementType,
        amount: { currencyCode, minorUnits: minor },
        reason,
        idempotencyKey: createIdempotencyKey('shift-cash-movement')
      });
      setMovementAmount('10.00');
      setMovementReason(t('op.cash.movement.defaultReason'));
    });

  const submitClose = () =>
    run(t('op.cash.action.close'), async () => {
      const minor = parseMoneyInputMinorUnits(countedCash);
      if (minor === null || shiftId === null) throw new Error(t('op.cash.close.countedLabel'));
      await actions!.closeShift(shiftId, {
        organizationId: backend!.session.organizationId,
        countedCash: { currencyCode, minorUnits: minor },
        closingNote: closingNote.trim(),
        idempotencyKey: createIdempotencyKey('shift-close')
      });
    });

  return (
    <div className="cash-head-commands">
      {canOpen && (
        <button type="button" className="cash-command-btn" onClick={() => setActiveModal('open')}>
          <Unlock size={14} aria-hidden="true" />{t('op.cash.action.open')}
        </button>
      )}
      {canCash && (
        <>
          <button type="button" className="cash-command-btn" onClick={() => setActiveModal('cash_in')}>
            <ArrowDownToLine size={14} aria-hidden="true" />{t('op.cash.action.cashIn')}
          </button>
          <button type="button" className="cash-command-btn" onClick={() => setActiveModal('cash_out')}>
            <ArrowUpFromLine size={14} aria-hidden="true" />{t('op.cash.action.cashOut')}
          </button>
        </>
      )}
      {canClose && (
        <button type="button" className="cash-command-btn danger" onClick={() => setActiveModal('close')}>
          <Lock size={14} aria-hidden="true" />{t('op.cash.action.close')}
        </button>
      )}
      {feedback.state !== 'idle' && <FeedbackNotice feedback={feedback} />}

      {activeModal === 'open' && (
        <OpenShiftModal
          startingCash={startingCash}
          note={openingNote}
          onChangeStartingCash={setStartingCash}
          onChangeNote={setOpeningNote}
          onClose={() => setActiveModal(null)}
          onSubmit={submitOpen}
          busy={busy}
        />
      )}
      {(activeModal === 'cash_in' || activeModal === 'cash_out') && (
        <CashMovementModal
          movementType={activeModal}
          amount={movementAmount}
          reason={movementReason}
          onChangeAmount={setMovementAmount}
          onChangeReason={setMovementReason}
          onClose={() => setActiveModal(null)}
          onSubmit={submitMovement(activeModal)}
          busy={busy}
        />
      )}
      {activeModal === 'close' && (
        <CloseShiftModal
          expectedCash={expectedCash}
          counted={countedCash}
          note={closingNote}
          currencyCode={currencyCode}
          onChangeCounted={setCountedCash}
          onChangeNote={setClosingNote}
          onClose={() => setActiveModal(null)}
          onSubmit={submitClose}
          busy={busy}
        />
      )}
    </div>
  );
}
```

**NB исполнителю:** при сборке проверь, что `OperatorBackendContext` экспортирует `Feedback` — если `Feedback` живёт в `../operatorTypes` (так и есть: `export type Feedback`), импортируй оттуда. `emptyFeedback` начального состояния не используем — стартуем с `{ label: '', state: 'idle' }`. Сигнатуры `OpenShiftRequest`/`RecordCashMovementRequest`/`CloseShiftRequest` — из `../api/clients/shifts` (поля: `organizationId`, `startingCash`/`amount`+`movementType`/`countedCash`, нота, `idempotencyKey`).

- [ ] **Step 4: Прогон — зелёный.**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/CashShiftCommandBar.test.tsx`
Expected: PASS (5/5).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/cash/CashShiftCommandBar.tsx src/AFK4.Operator.App.Web/src/cash/CashShiftCommandBar.test.tsx
git commit -m "feat(cash-s1): CashShiftCommandBar — командная панель смены (открыть/внести/изъять/закрыть)"
```

---

### Task 6: `CashShiftHeader` — встроить командную панель + рефетч на `shiftNonce`

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashShiftHeader.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashShiftHeader.test.tsx`
- Modify (CSS): `src/AFK4.Operator.App.Web/src/styles/21-cash.css`

**Interfaces:**
- Consumes: `CashShiftCommandBar` (Task 5); `buildCashHeader` (есть); `OperatorAuthSession`.
- Produces: `CashShiftHeader` props расширяются: `{ backend; currencyCode; session?: OperatorAuthSession | null; shiftNonce?: number; onShiftChanged?: () => void; client?: ShiftRevenueReader; actions?: CashShiftActionsClient }`. Потребляется `CashWorkspace` (Task 8).

- [ ] **Step 1: Тест** — добавить в `cash/CashShiftHeader.test.tsx` кейс появления кнопки по праву. Дописать импорт `fireEvent` при необходимости и новый кейс в `describe`:

```tsx
  it('открытая смена + право shifts.close → кнопка «Закрыть смену» в шапке', async () => {
    const session = { permissions: ['shifts.close'], organizationId: 'o' } as never;
    render(
      <I18nProvider initialLocale="ru">
        <CashShiftHeader
          backend={backend}
          currencyCode="TJS"
          session={session}
          client={{ current: async () => openShift() }}
          actions={{ openShift: async () => ({}), recordCashMovement: async () => ({}), closeShift: async () => ({}) }}
        />
      </I18nProvider>
    );
    await waitFor(() => expect(screen.getByText('Смена открыта')).toBeInTheDocument());
    expect(screen.getByRole('button', { name: 'Закрыть смену' })).toBeInTheDocument();
  });
```

(Существующие 2 кейса не меняем: они рендерят без `session` → команд-панель не показывает кнопок, статус-часть прежняя.)

- [ ] **Step 2: Прогон — падает** (новый кейс: нет пропсов `session`/`actions` в типе).

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/CashShiftHeader.test.tsx`
Expected: FAIL (новый кейс: кнопка не найдена / тип-проп отсутствует).

- [ ] **Step 3: Реализация** — обновить `cash/CashShiftHeader.tsx` (полный файл):

```tsx
import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { createAuthenticatedOperatorClients, formatMoney } from '../operatorHelpers';
import { StateFlag } from '../operatorPrimitives';
import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';
import type { ShiftRevenueDto } from '../operatorApiClients';
import { buildCashHeader } from './cashModel';
import { CashShiftCommandBar, type CashShiftActionsClient } from './CashShiftCommandBar';

interface ShiftRevenueReader {
  current(branchId: string): Promise<ShiftRevenueDto | null>;
}

// Якорь раздела «Касса»: статус текущей смены (виден из любой вкладки) + командная панель смены
// (открыть/внести/изъять/закрыть). Действие → onShiftChanged → раздел бампает shiftNonce →
// шапка и вкладка «Смена» перечитывают смену.
export function CashShiftHeader({
  backend,
  currencyCode,
  session = null,
  shiftNonce = 0,
  onShiftChanged = () => {},
  client: injectedClient,
  actions
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session?: OperatorAuthSession | null;
  shiftNonce?: number;
  onShiftChanged?: () => void;
  client?: ShiftRevenueReader;
  actions?: CashShiftActionsClient;
}) {
  const { t } = useI18n();
  const memoizedClient = useMemo(
    () => (backend && !injectedClient ? createAuthenticatedOperatorClients(backend.config, backend.session).shiftRevenue : null),
    [backend?.config, backend?.session, injectedClient]
  );
  const client = injectedClient ?? memoizedClient;
  const [revenue, setRevenue] = useState<ShiftRevenueDto | null>(null);

  useEffect(() => {
    if (client === null || backend === null) return undefined;
    let active = true;
    setRevenue(null);
    client.current(backend.branchId)
      .then((cur) => { if (active) setRevenue(cur); })
      .catch(() => { if (active) setRevenue(null); });
    return () => { active = false; };
  }, [client, backend?.branchId, shiftNonce]);

  const header = buildCashHeader(revenue);

  return (
    <section className="cash-head">
      <h1>
        <strong className="cash-head-name">{t('op.cash.title')}</strong>
        {' · '}
        <span className="cash-head-tagline">
          {header.isOpen ? t('op.cash.header.open') : t('op.cash.header.closed')}
        </span>
      </h1>
      {header.isOpen && (
        <div className="cash-head-metrics">
          <StateFlag label={t('op.cash.metric.inHand')} value={formatMoney(header.cashInHand, currencyCode)} />
          <StateFlag label={t('op.cash.metric.revenue')} value={formatMoney(header.revenueTotal, currencyCode)} />
        </div>
      )}
      <CashShiftCommandBar
        backend={backend}
        session={session}
        shiftId={revenue?.shiftId ?? null}
        isOpen={header.isOpen}
        expectedCash={header.cashInHand}
        currencyCode={currencyCode}
        onShiftChanged={onShiftChanged}
        actions={actions}
      />
    </section>
  );
}
```

- [ ] **Step 4: CSS** — добавить в конец `styles/21-cash.css` (командная панель + формы модалок):

```css
/* S1: командная панель смены в шапке-якоре + формы модалок смены. */
.cash-head-commands { display: flex; align-items: center; flex: none; gap: var(--space-2); }
.cash-command-btn {
  appearance: none;
  display: inline-flex;
  align-items: center;
  gap: var(--space-1);
  min-height: var(--control-sm);
  padding: 0 var(--space-2);
  border: 1px solid var(--border-soft);
  border-radius: 6px;
  background: var(--surface-elevated);
  color: var(--text-primary);
  font-size: 12px;
  cursor: pointer;
}
.cash-command-btn:hover { border-color: var(--accent); color: var(--accent-bright); }
.cash-command-btn:focus-visible { outline: 2px solid var(--accent); outline-offset: 2px; }
.cash-command-btn.danger { color: var(--danger, #d3403a); }
.cash-command-btn.danger:hover { border-color: var(--danger, #d3403a); }

.cash-shift-form { display: flex; flex-direction: column; gap: var(--space-2); }
.cash-shift-form label { font-size: 12px; color: var(--text-secondary); }
.cash-shift-form input {
  min-height: var(--control-md);
  padding: 0 var(--space-2);
  border: 1px solid var(--border-soft);
  border-radius: 6px;
  background: var(--surface-base, var(--surface-elevated));
  color: var(--text-primary);
}
.cash-close-reconcile { display: flex; flex-direction: column; gap: var(--space-1); }
.cash-close-reconcile > div { display: flex; justify-content: space-between; font-size: 13px; }
.cash-close-reconcile .attention strong { color: var(--danger, #d3403a); }
.cash-close-impact { margin: 0; font-size: 12px; color: var(--text-secondary); }
.cash-primary-action {
  display: inline-flex;
  align-items: center;
  justify-content: center;
  gap: var(--space-2);
  min-height: var(--control-lg);
  border: none;
  border-radius: 7px;
  background: var(--accent);
  color: #fff;
  font-weight: 600;
  cursor: pointer;
}
.cash-primary-action:disabled { opacity: 0.6; cursor: default; }
.cash-primary-action.danger { background: var(--danger, #d3403a); }
```

**NB исполнителю:** если токенов `--danger`/`--surface-base`/`--accent-bright` нет — проверь `@afk4/tokens`/`operatorTheme` (`rg "\-\-accent-bright|\-\-danger" src/styles packages`) и используй фактически существующие (в `.cash-tab.active` уже используется `--accent-bright`, значит он есть). Фолбэки в `var(--x, …)` оставлены на случай отсутствия.

- [ ] **Step 5: Прогон — зелёный** (header subdir).

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/CashShiftHeader.test.tsx`
Expected: PASS (3/3).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/cash/CashShiftHeader.tsx src/AFK4.Operator.App.Web/src/cash/CashShiftHeader.test.tsx src/AFK4.Operator.App.Web/src/styles/21-cash.css
git commit -m "feat(cash-s1): шапка-якорь рендерит командную панель смены + рефетч на shiftNonce"
```

---

### Task 7: `CashShiftWorkspace` — вкладка «Смена» (кокпит кассира)

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/cash/CashShiftWorkspace.tsx`
- Test: `src/AFK4.Operator.App.Web/src/cash/CashShiftWorkspace.test.tsx`
- Modify (CSS): `src/AFK4.Operator.App.Web/src/styles/21-cash.css`

**Interfaces:**
- Consumes: `createAuthenticatedOperatorClients`, `formatMoney`, `formatTime`, `cashOperationTypeLabel`, `readArray`, `readString`, `readMoney`, `downloadTextFile` из `../operatorHelpers`; `projectOperatorError` из `../apiErrors`; `ShiftRevenueDto` из `../operatorApiClients`. Читает `shiftRevenue.current/history`, `shifts.getCashOperationReport`, CSV через `shifts.exportShiftReportCsv/exportCashOperationReportCsv/exportSalesReportCsv`.
- Produces: `CashShiftWorkspace` props `{ backend: OperatorBackendContext | null; branchId: string; currencyCode: string; shiftNonce?: number; revenueClient?: ShiftCockpitClient; reports?: ShiftCockpitReports }`. Инъекции для тестов. Потребляется `CashWorkspace` (Task 8).

Определить инъект-типы (для тестируемости без боевого клиента):

```ts
interface ShiftCockpitClient {
  current(branchId: string): Promise<ShiftRevenueDto | null>;
  history(branchId: string, limit?: number): Promise<{ shifts: ShiftRevenueDto[]; limit: number }>;
}
interface ShiftCockpitReports {
  getCashOperationReport(branchId: string, query?: { limit?: number }): Promise<Record<string, unknown>>;
}
```

- [ ] **Step 1: Тест** `cash/CashShiftWorkspace.test.tsx`:

```tsx
import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { CashShiftWorkspace } from './CashShiftWorkspace';
import type { ShiftRevenueDto } from '../operatorApiClients';

afterEach(cleanup);
const m = (minorUnits: number) => ({ currencyCode: 'TJS', minorUnits });

function openShift(): ShiftRevenueDto {
  return {
    shiftId: 's1', organizationId: 'o', branchId: 'b1',
    openedByStaffUserId: 'u1', closedByStaffUserId: null, state: 'open',
    earned: { time: m(310000), goods: m(115000), total: m(425000) },
    inflow: { cash: m(200000), nonCash: m(180000), walletTopUps: m(90000), directTotal: m(380000) },
    cash: { starting: m(1000000), expected: m(1380000), counted: null, difference: null },
    openedAtUtc: '2026-06-24T09:00:00Z', closedAtUtc: null
  };
}

const backend = { config: { platformBaseUrl: 'x' }, session: { accessToken: 't' }, branchId: 'b1' } as never;

function renderWs(current: ShiftRevenueDto | null, cashRows: Record<string, unknown>[] = []) {
  render(
    <I18nProvider initialLocale="ru">
      <CashShiftWorkspace
        backend={backend}
        branchId="b1"
        currencyCode="TJS"
        revenueClient={{ current: async () => current, history: async () => ({ shifts: [], limit: 20 }) }}
        reports={{ getCashOperationReport: async () => ({ rows: cashRows }) }}
      />
    </I18nProvider>
  );
}

describe('CashShiftWorkspace', () => {
  it('открытая смена → выручка и сверка', async () => {
    renderWs(openShift());
    await waitFor(() => expect(screen.getByText('Выручка смены')).toBeInTheDocument());
    expect(screen.getByText('Сверка кассы')).toBeInTheDocument();
    expect(screen.getByText('Ожидается')).toBeInTheDocument();
  });

  it('нет смены → пустое состояние', async () => {
    renderWs(null);
    await waitFor(() => expect(screen.getByText('Нет открытой смены')).toBeInTheDocument());
  });

  it('последние движения наличных в списке', async () => {
    renderWs(openShift(), [
      { operationId: 'c1', createdAtUtc: '2026-06-24T10:00:00Z', operationType: 'cash_in', cashImpact: m(5000), reason: 'Размен' }
    ]);
    await waitFor(() => expect(screen.getByText('Движение наличных')).toBeInTheDocument());
  });
});
```

- [ ] **Step 2: Прогон — падает.**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/CashShiftWorkspace.test.tsx`
Expected: FAIL (cannot find `./CashShiftWorkspace`).

- [ ] **Step 3: Реализация** `cash/CashShiftWorkspace.tsx`:

```tsx
import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { ReceiptText, Banknote, ArrowRightLeft } from 'lucide-react';
import {
  cashOperationTypeLabel,
  createAuthenticatedOperatorClients,
  downloadTextFile,
  formatMoney,
  formatTime,
  readArray,
  readMoney,
  readString
} from '../operatorHelpers';
import { projectOperatorError } from '../apiErrors';
import type { OperatorBackendContext } from '../operatorTypes';
import type { ShiftRevenueDto } from '../operatorApiClients';

interface ShiftCockpitClient {
  current(branchId: string): Promise<ShiftRevenueDto | null>;
  history(branchId: string, limit?: number): Promise<{ shifts: ShiftRevenueDto[]; limit: number }>;
}
interface ShiftCockpitReports {
  getCashOperationReport(branchId: string, query?: { limit?: number }): Promise<Record<string, unknown>>;
}

// Вкладка «Смена»: кокпит кассира — выручка + сверка из shiftRevenue, последние движения наличных,
// CSV-экспорты, история. Действия (открыть/закрыть/внести/изъять) — в шапке-якоре, не здесь.
// Полная поисковая лента операций + сетка методов отложены в S2 «Журнал кассы».
export function CashShiftWorkspace({
  backend,
  branchId,
  currencyCode,
  shiftNonce = 0,
  revenueClient: injectedRevenue,
  reports: injectedReports
}: {
  backend: OperatorBackendContext | null;
  branchId: string;
  currencyCode: string;
  shiftNonce?: number;
  revenueClient?: ShiftCockpitClient;
  reports?: ShiftCockpitReports;
}) {
  const { t } = useI18n();
  const built = useMemo(
    () => (backend && (!injectedRevenue || !injectedReports) ? createAuthenticatedOperatorClients(backend.config, backend.session) : null),
    [backend?.config, backend?.session, injectedRevenue, injectedReports]
  );
  const revenueClient = injectedRevenue ?? built?.shiftRevenue ?? null;
  const reports = injectedReports ?? (built?.shifts ?? null);

  const [current, setCurrent] = useState<ShiftRevenueDto | null>(null);
  const [history, setHistory] = useState<ShiftRevenueDto[]>([]);
  const [cashRows, setCashRows] = useState<Record<string, unknown>[]>([]);
  const [loading, setLoading] = useState(true);
  const [loadError, setLoadError] = useState<string | null>(null);

  useEffect(() => {
    if (revenueClient === null || reports === null) return undefined;
    let active = true;
    setLoading(true);
    setLoadError(null);
    Promise.all([
      revenueClient.current(branchId),
      revenueClient.history(branchId, 20),
      reports.getCashOperationReport(branchId, { limit: 6 })
    ])
      .then(([cur, hist, cash]) => {
        if (!active) return;
        setCurrent(cur);
        setHistory(hist.shifts.filter((s) => s.state === 'closed'));
        setCashRows(readArray<Record<string, unknown>>(cash, 'rows'));
      })
      .catch((error) => { if (active) setLoadError(projectOperatorError(error, t).detail); })
      .finally(() => { if (active) setLoading(false); });
    return () => { active = false; };
  }, [revenueClient, reports, branchId, shiftNonce]);

  const exportCsv = async (kind: 'shifts' | 'cash' | 'sales') => {
    if (backend === null) return;
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    const stamp = new Date().toISOString().replace(/[:.]/g, '-');
    if (kind === 'shifts') {
      downloadTextFile(`afk4-shift-summary-${stamp}.csv`, await clients.shifts.exportShiftReportCsv(branchId, { limit: 50 }), 'text/csv;charset=utf-8');
    } else if (kind === 'cash') {
      downloadTextFile(`afk4-cash-movements-${stamp}.csv`, await clients.shifts.exportCashOperationReportCsv(branchId, { limit: 50 }), 'text/csv;charset=utf-8');
    } else {
      downloadTextFile(`afk4-check-list-${stamp}.csv`, await clients.shifts.exportSalesReportCsv(branchId, { limit: 50 }), 'text/csv;charset=utf-8');
    }
  };

  if (loading) {
    return <main className="workspace-screen cash-shift-screen"><p className="workspace-loading">{t('op.shifts.loading')}</p></main>;
  }
  if (loadError) {
    return (
      <main className="workspace-screen cash-shift-screen">
        <p className="workspace-error" role="alert">{loadError}</p>
      </main>
    );
  }

  return (
    <main className="workspace-screen cash-shift-screen">
      {current ? (
        <div className="cash-shift-grid">
          <section className="cash-shift-card">
            <h2>{t('op.cash.shift.revenueTitle')}</h2>
            <div className="cash-shift-row"><span>{t('op.shifts.earned')}</span><strong>{formatMoney(current.earned.total, currencyCode)}</strong></div>
            <div className="cash-shift-row"><span>{t('op.shifts.time')}</span><strong>{formatMoney(current.earned.time, currencyCode)}</strong></div>
            <div className="cash-shift-row"><span>{t('op.shifts.goods')}</span><strong>{formatMoney(current.earned.goods, currencyCode)}</strong></div>
            <div className="cash-shift-row"><span>{t('op.shifts.cash')}</span><strong>{formatMoney(current.inflow.cash, currencyCode)}</strong></div>
            <div className="cash-shift-row"><span>{t('op.shifts.nonCash')}</span><strong>{formatMoney(current.inflow.nonCash, currencyCode)}</strong></div>
            <div className="cash-shift-row"><span>{t('op.shifts.walletTopUps')}</span><strong>{formatMoney(current.inflow.walletTopUps, currencyCode)}</strong></div>
          </section>

          <section className="cash-shift-card">
            <h2>{t('op.cash.shift.reconcileTitle')}</h2>
            <div className="cash-shift-row"><span>{t('op.cash.shift.starting')}</span><strong>{formatMoney(current.cash.starting, currencyCode)}</strong></div>
            <div className="cash-shift-row"><span>{t('op.cash.shift.expected')}</span><strong>{formatMoney(current.cash.expected, currencyCode)}</strong></div>
            <div className="cash-shift-row"><span>{t('op.cash.shift.counted')}</span><strong>{current.cash.counted ? formatMoney(current.cash.counted, currencyCode) : t('op.cash.shift.notClosed')}</strong></div>
            <div className={`cash-shift-row${current.cash.difference && current.cash.difference.minorUnits !== 0 ? ' attention' : ''}`}>
              <span>{t('op.cash.shift.difference')}</span><strong>{formatMoney(current.cash.difference, currencyCode)}</strong>
            </div>
          </section>

          <section className="cash-shift-card">
            <h2>{t('op.cash.shift.movementsTitle')}</h2>
            {cashRows.length === 0 ? (
              <p className="cash-shift-empty-note">{t('op.cash.shift.movementsEmpty')}</p>
            ) : (
              <ul className="cash-shift-movements">
                {cashRows.slice(0, 6).map((row) => (
                  <li key={readString(row, 'operationId')}>
                    <span>{formatTime(readString(row, 'createdAtUtc'))}</span>
                    <strong>{cashOperationTypeLabel(readString(row, 'operationType', 'cash'), t)}</strong>
                    <b>{formatMoney(readMoney(row, 'cashImpact'), currencyCode)}</b>
                  </li>
                ))}
              </ul>
            )}
          </section>

          <section className="cash-shift-card">
            <h2>{t('op.cash.shift.exportTitle')}</h2>
            <div className="cash-shift-exports">
              <button type="button" onClick={() => void exportCsv('shifts')}><ReceiptText size={15} aria-hidden="true" />{t('op.cash.shift.exportShiftSummary')}</button>
              <button type="button" onClick={() => void exportCsv('cash')}><Banknote size={15} aria-hidden="true" />{t('op.cash.shift.exportCashMovements')}</button>
              <button type="button" onClick={() => void exportCsv('sales')}><ArrowRightLeft size={15} aria-hidden="true" />{t('op.cash.shift.exportReceipts')}</button>
            </div>
          </section>
        </div>
      ) : (
        <section className="cash-shift-empty">{t('op.shifts.noOpenShift')}</section>
      )}

      <section className="cash-shift-history">
        <h2>{t('op.cash.shift.historyTitle')}</h2>
        {history.length === 0 ? (
          <p className="cash-shift-empty-note">{t('op.cash.shift.historyEmpty')}</p>
        ) : (
          <ul>
            {history.map((s) => (
              <li key={s.shiftId}>
                {new Date(s.openedAtUtc).toLocaleDateString('ru-RU')} · {t('op.shifts.earned')} {formatMoney(s.earned.total, currencyCode)}
                {s.cash.difference ? ` · ${t('op.shifts.cashDiff')} ${formatMoney(s.cash.difference, currencyCode)}` : ''}
              </li>
            ))}
          </ul>
        )}
      </section>
    </main>
  );
}
```

**NB исполнителю:** проверь, что `cashOperationTypeLabel` и `formatTime` экспортируются из `../operatorHelpers` (да — используются в `BackendPaymentsWorkspace`). `new Date(...).toISOString()`/`toLocaleDateString` допустимы (как в существующем `ShiftsWorkspace`/`BackendPaymentsWorkspace`).

- [ ] **Step 4: CSS** — добавить в конец `styles/21-cash.css`:

```css
/* S1: вкладка «Смена» — кокпит кассира. */
.cash-shift-grid { display: grid; grid-template-columns: repeat(auto-fit, minmax(220px, 1fr)); gap: var(--space-3); }
.cash-shift-card {
  display: flex;
  flex-direction: column;
  gap: var(--space-1);
  padding: var(--space-3);
  border: 1px solid var(--border-soft);
  border-radius: 8px;
  background: var(--surface-elevated);
}
.cash-shift-card h2 { margin: 0 0 var(--space-1); font-size: 13px; color: var(--text-secondary); font-weight: 600; }
.cash-shift-row { display: flex; justify-content: space-between; font-size: 13px; color: var(--text-primary); }
.cash-shift-row.attention strong { color: var(--danger, #d3403a); }
.cash-shift-movements { list-style: none; margin: 0; padding: 0; display: flex; flex-direction: column; gap: var(--space-1); }
.cash-shift-movements li { display: grid; grid-template-columns: auto 1fr auto; gap: var(--space-2); font-size: 12px; }
.cash-shift-exports { display: flex; flex-direction: column; gap: var(--space-1); }
.cash-shift-exports button {
  display: inline-flex; align-items: center; gap: var(--space-2);
  min-height: var(--control-md); padding: 0 var(--space-2);
  border: 1px solid var(--border-soft); border-radius: 6px;
  background: var(--surface-elevated); color: var(--text-primary); cursor: pointer;
}
.cash-shift-exports button:hover { border-color: var(--accent); }
.cash-shift-history { margin-top: var(--space-3); }
.cash-shift-history h2 { font-size: 13px; color: var(--text-secondary); }
.cash-shift-history ul { margin: 0; padding-left: var(--space-3); }
.cash-shift-empty, .cash-shift-empty-note { color: var(--text-secondary); font-size: 13px; }
```

- [ ] **Step 5: Прогон — зелёный.**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash/CashShiftWorkspace.test.tsx`
Expected: PASS (3/3).

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/cash/CashShiftWorkspace.tsx src/AFK4.Operator.App.Web/src/cash/CashShiftWorkspace.test.tsx src/AFK4.Operator.App.Web/src/styles/21-cash.css
git commit -m "feat(cash-s1): вкладка «Смена» — кокпит кассира (выручка/сверка/движения/экспорт/история)"
```

---

### Task 8: Схлопнуть вкладки `payments`+`shifts` → `shift`; удалить старые воркспейсы

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashTabBar.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/cash/cashModel.ts`
- Modify: `src/AFK4.Operator.App.Web/src/cash/cashModel.test.ts`
- Modify: `src/AFK4.Operator.App.Web/src/cash/CashWorkspace.tsx`
- Delete: `src/AFK4.Operator.App.Web/src/BackendPaymentsWorkspace.tsx`, `src/AFK4.Operator.App.Web/src/ShiftsWorkspace.tsx`, `src/AFK4.Operator.App.Web/src/ShiftsWorkspace.test.tsx`

**Interfaces:**
- Consumes: `CashShiftHeader` (Task 6), `CashShiftWorkspace` (Task 7).
- Produces: `CashTab = 'sales' | 'orders' | 'shift' | 'review'`. `CASH_TAB_PERMISSIONS.shift` = объединение прав старых payments+shifts.

- [ ] **Step 1: `CashTabBar.tsx`** — обновить union (строка 1):

```tsx
export type CashTab = 'sales' | 'orders' | 'shift' | 'review';
```

- [ ] **Step 2: `cashModel.ts`** — обновить `CASH_TAB_PERMISSIONS` и `CASH_TAB_ORDER` (заменить две записи `payments`/`shifts` одной `shift`):

```ts
const CASH_TAB_PERMISSIONS: Record<CashTab, readonly string[]> = {
  sales: [permissionNames.createPosSale, permissionNames.payPosSale, permissionNames.refundPosSale, permissionNames.voidPosSale],
  orders: [permissionNames.createPosSale],
  shift: [permissionNames.viewShift, permissionNames.openShift, permissionNames.closeShift, permissionNames.manageShiftCash, permissionNames.viewReports],
  review: [permissionNames.approveMoneyAction]
};

const CASH_TAB_ORDER: CashTab[] = ['sales', 'orders', 'shift', 'review'];
```

- [ ] **Step 3: `cashModel.test.ts`** — обновить ожидания `visibleCashTabs`: где тест ожидал `'payments'`/`'shifts'`, теперь `'shift'`. Найти и заменить (`rg "'payments'|'shifts'" src/cash/cashModel.test.ts`). Пример замены кейса прав «только смотрит смену»:

```ts
  it('право shifts.view → видна вкладка shift', () => {
    expect(visibleCashTabs(session(['shifts.view']))).toEqual(['shift']);
  });
```

(Сохрани структуру существующих кейсов; только переименуй ожидаемые id payments/shifts → shift и слей дубль, если оба ожидались.)

- [ ] **Step 4: `CashWorkspace.tsx`** — полный файл:

```tsx
import { useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { OperatorAuthSession } from '../authClient';
import type { OperatorBackendContext } from '../operatorTypes';
import { visibleCashTabs } from './cashModel';
import { CashShiftHeader } from './CashShiftHeader';
import { CashTabBar, type CashTab } from './CashTabBar';
import { BackendPosWorkspace } from '../BackendPosWorkspace';
import { ShopOrdersWorkspace } from '../ShopOrdersWorkspace';
import { CashShiftWorkspace } from './CashShiftWorkspace';
import { ReviewWorkspace } from '../ReviewWorkspace';

// Единый раздел «Касса» = шапка-якорь смены (статус + командная панель) + под-вкладки.
// S1: payments+shifts слиты во вкладку «Смена» (shift); действия смены живут в шапке.
export function CashWorkspace({
  backend,
  currencyCode,
  session
}: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session: OperatorAuthSession | null;
}) {
  const { t } = useI18n();
  const [activeTab, setActiveTab] = useState<CashTab>(() => visibleCashTabs(session)[0] ?? 'sales');
  const [shiftNonce, setShiftNonce] = useState(0);

  const visible = new Set(visibleCashTabs(session));
  const allTabs: { id: CashTab; label: string }[] = [
    { id: 'sales', label: t('op.shell.nav.pos') },
    { id: 'orders', label: t('op.shell.nav.shop_orders') },
    { id: 'shift', label: t('op.cash.tab.shift') },
    { id: 'review', label: t('op.shell.nav.review') }
  ];
  const tabs = allTabs.filter((tab) => visible.has(tab.id));

  return (
    <main className="workspace-screen cash-screen">
      <CashShiftHeader
        backend={backend}
        currencyCode={currencyCode}
        session={session}
        shiftNonce={shiftNonce}
        onShiftChanged={() => setShiftNonce((n) => n + 1)}
      />
      <CashTabBar tabs={tabs} activeTab={activeTab} onSelect={setActiveTab} label={t('op.shell.navGroup.cashier')} />
      <div className="cash-tab-content">
        {activeTab === 'sales' && <BackendPosWorkspace currencyCode={currencyCode} backend={backend} />}
        {activeTab === 'orders' && <ShopOrdersWorkspace backend={backend} />}
        {activeTab === 'shift' && backend !== null && (
          <CashShiftWorkspace backend={backend} branchId={backend.branchId} currencyCode={currencyCode} shiftNonce={shiftNonce} />
        )}
        {activeTab === 'review' && <ReviewWorkspace currencyCode={currencyCode} backend={backend} />}
      </div>
    </main>
  );
}
```

**NB исполнителю:** `CashShiftWorkspace` рендерим только при `backend !== null` (как раньше `ShiftsWorkspace`). Если `activeTab==='shift'` и `backend===null` (dev-mock без backend), вкладка пуста — это допустимо (как было).

- [ ] **Step 5: Удалить старые воркспейсы**

```bash
git rm src/AFK4.Operator.App.Web/src/BackendPaymentsWorkspace.tsx \
       src/AFK4.Operator.App.Web/src/ShiftsWorkspace.tsx \
       src/AFK4.Operator.App.Web/src/ShiftsWorkspace.test.tsx
```

- [ ] **Step 6: Найти и устранить повисшие импорты** старых воркспейсов (кроме App.test — это Task 9):

Run: `cd src/AFK4.Operator.App.Web && rg -l "BackendPaymentsWorkspace|ShiftsWorkspace" src --glob '!App.test.tsx'`
Expected: пусто после правок (если что-то всплыло вне App.test — поправить импорт здесь же; ожидаемо чисто, т.к. оба рендерились только в CashWorkspace).

- [ ] **Step 7: Прогон subdir-тестов (без App.test) + build**

Run: `cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun test src/cash && /home/fedya/.bun/bin/bun run build`
Expected: cash-тесты PASS; build PASS (tsc не находит повисших импортов/типов). Если build падает на App.test-типах — это нормально, App.test чинит Task 9 (но `bun run build` тайпчекает весь src, включая App.test → если build красный из-за App.test, отметь и переходи к Task 9, затем повторно собери). 

**NB:** `bun run build` = `tsc -b && vite build` тайпчекает и App.test. Поэтому полностью зелёный build будет только после Task 9. На этом шаге достаточно: cash-тесты зелёные, а build-ошибки — только из App.test (никаких других). Зафиксируй вывод.

- [ ] **Step 8: Commit**

```bash
git add -A src/AFK4.Operator.App.Web/src/cash src/AFK4.Operator.App.Web/src/BackendPaymentsWorkspace.tsx src/AFK4.Operator.App.Web/src/ShiftsWorkspace.tsx src/AFK4.Operator.App.Web/src/ShiftsWorkspace.test.tsx
git commit -m "refactor(cash-s1): payments+shifts → одна вкладка «Смена»; удалить старые воркспейсы"
```

---

### Task 9: Миграция `App.test.tsx` под новую IA «Кассы»

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/App.test.tsx`

**Interfaces:**
- Consumes: новый shell «Кассы» (Tasks 6–8). Действие открытия смены теперь — модалка из шапки, а не форма «Платежей».

Затронутые места (найти точно: `rg -n "Платежи|payments|Операции смены|Сверка кассы|Подготовить закрытие|shifts/open" src/App.test.tsx`):

- [ ] **Step 1: `TAB_SECTION`** (строка ~9–13) — убрать `'Платежи'`, добавить `'Смена'`:

```ts
const TAB_SECTION: Record<string, string> = {
  'Продажи': 'Касса', 'Смена': 'Касса', 'Проверка': 'Касса',
  'Дашборд': 'Отчёты',
  'Настройки': 'Управление', 'Приём платежей': 'Управление', 'Лояльность': 'Управление', 'Новости': 'Управление', 'Логи': 'Управление'
};
```

- [ ] **Step 2: Walkthrough-блок «Платежи»** (строки ~876–887) — заменить навигацию и ассерты на вкладку «Смена» (кокпит). Было — структура «Платежей»; стало:

```tsx
    gotoWorkspace('Смена');
    expect(await screen.findByText('Выручка смены')).toBeInTheDocument();
    expect(screen.getByText('Сверка кассы')).toBeInTheDocument();
    expect(screen.getByText('Ожидается')).toBeInTheDocument();
    expect(screen.getByText('История смен')).toBeInTheDocument();
```

(Шапка-якорь «Касса» в этом сценарии: смена открыта в моке → в шапке кнопки «Внести/Изъять/Закрыть». Если у тестовой сессии есть `shifts.*` права — кнопки появятся; ассертить их не обязательно, проверяем кокпит вкладки.)

- [ ] **Step 3: Тест «opens a shift from Payments»** (строки ~1487–1524) — переписать на открытие через модалку шапки. Переименовать и заменить тело UI-части (контракт POST `/shifts/open` тот же):

```tsx
  it('opens a shift from the cash header modal when no current shift exists', async () => {
    installSessionBridge();
    let shiftOpened = false;
    fetchMock.mockImplementation((input, init) => {
      const url = new URL(String(input));
      if (url.pathname.endsWith('/shifts/current') && !shiftOpened) {
        return Promise.resolve(new Response('', { status: 404, statusText: 'Not Found' }));
      }
      if (url.pathname.endsWith('/shifts/revenue/current') && !shiftOpened) {
        return Promise.resolve(new Response('', { status: 404, statusText: 'Not Found' }));
      }
      if (url.pathname.endsWith('/shifts/open') && init?.method === 'POST') {
        shiftOpened = true;
      }
      return mockPlatformFetch(input, init);
    });

    render(<App />);
    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    gotoWorkspace('Смена');

    // Смена закрыта → в шапке кнопка «Открыть смену» открывает модалку.
    fireEvent.click(await screen.findByRole('button', { name: 'Открыть смену' }));
    fireEvent.change(screen.getByLabelText('Старт наличных'), { target: { value: '150.00' } });
    fireEvent.change(screen.getByLabelText('Комментарий'), { target: { value: 'Утренняя смена' } });
    // submit модалки (вторая кнопка «Открыть смену» — внутри формы модалки)
    const dialog = screen.getByRole('dialog');
    fireEvent.click(within(dialog).getByRole('button', { name: 'Открыть смену' }));

    const openCall = await waitFor(() => {
      const call = fetchMock.mock.calls.find(([input, init]) =>
        String(input).includes('/api/branches/acfc0212-967f-4d84-94be-9003387b09c2/shifts/open') && init?.method === 'POST');
      expect(call).toBeDefined();
      return call!;
    });
    const body = JSON.parse(String(openCall[1]?.body));
    expect(body).toMatchObject({
      organizationId: '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08',
      startingCash: { currencyCode: 'TJS', minorUnits: 15000 },
      openingNote: 'Утренняя смена'
    });
    expect(body.idempotencyKey).toMatch(/^shift-open-/);
  });
```

**NB исполнителю:** есть две кнопки «Открыть смену» (в шапке и в форме модалки) — submit скоупь внутри `getByRole('dialog')` через `within`. Убедись, что мок отдаёт 404 и на `/shifts/revenue/current` (его читает шапка), иначе шапка может считать смену открытой и не покажет «Открыть смену». Проверь, какой мок-роут `/shifts/revenue/current` есть в `mockPlatformFetch` по умолчанию — если он отдаёт открытую смену, явно верни 404 до `shiftOpened` (как выше).

- [ ] **Step 4: Тест «shows successful empty Payments reports»** (строки ~1526–1569) — кокпит «Смены» больше не показывает полную ленту операций (она в S2). Этот тест проверял пустые состояния ленты Платежей. Заменить на проверку, что вкладка «Смена» грузится без ошибки на пустых отчётах:

```tsx
  it('renders the shift tab without errors on empty reports', async () => {
    installSessionBridge();
    const zero = { currencyCode: 'TJS', minorUnits: 0 };
    fetchMock.mockImplementation((input, init) => {
      const pathname = new URL(String(input)).pathname;
      if (pathname.endsWith('/reports/cash-operations')) {
        return Promise.resolve(jsonResponse({ ...createCashReport(), rows: [], cashInTotal: zero, cashOutTotal: zero, netCashTotal: zero }));
      }
      return mockPlatformFetch(input, init);
    });

    render(<App />);
    expect(await screen.findByRole('heading', { name: /AFK4 Dushanbe/ })).toBeInTheDocument();
    gotoWorkspace('Смена');
    expect(await screen.findByText('Выручка смены')).toBeInTheDocument();
    expect(screen.getByText('Движений нет')).toBeInTheDocument();
  });
```

**NB исполнителю:** если дефолтный мок `/shifts/revenue/current` отдаёт открытую смену с непустыми движениями — для этого теста переопредели `/reports/cash-operations` на пустые rows (как выше). Если мок выдаёт «нет смены», ассерты замени на пустое состояние кокпита (`'Нет открытой смены'`). Сначала прогони, посмотри что реально рендерит дефолтный мок, подгони ассерты под факт (а не наоборот).

- [ ] **Step 5: Прочие повисшие ссылки** — прогнать поиск и поправить остаточные упоминания старой IA:

Run: `cd src/AFK4.Operator.App.Web && rg -n "'Платежи'|gotoWorkspace\('Платежи'\)|Операции смены|Подготовить закрытие|Методы оплаты" src/App.test.tsx`
Expected: пусто (все мигрированы). Что осталось — поправить под вкладку «Смена»/шапку.

- [ ] **Step 6: Полный прогон фронта (subdir + App.test) + build + i18n**

Run:
```bash
cd src/AFK4.Operator.App.Web && /home/fedya/.bun/bin/bun run test && /home/fedya/.bun/bin/bun run build
cd ../../packages/i18n && /home/fedya/.bun/bin/bun test
```
Expected: всё PASS. (App.test зелёный; build tsc+vite зелёный; i18n guard зелёный.)

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/App.test.tsx
git commit -m "test(cash-s1): App.test под новую IA «Кассы» (вкладка «Смена», открытие смены из шапки)"
```

---

## Self-Review

**1. Spec coverage** (против `2026-06-24-operator-cash-design.md`, секция S1):
- «открытие/закрытие/внести-изъять через шапку-якорь + модалки (вынуть из Платежей)» → Tasks 2–6 (3 модалки + командная панель + шапка). ✅
- «выручка + сверка наличных в одной вкладке» → Task 7 (кокпит). ✅
- «единый дизайн» → токены/зеркала паттернов в Tasks 6–7 CSS. ✅
- «payments+shifts → одна shift, старые удаляются» → Task 8. ✅
- Тесты-перенос (`ShiftsWorkspace.test` удалён, его покрытие — в `CashShiftWorkspace.test`; `App.test` мигрирован) → Tasks 7–9. ✅
- X/Z — вне S1 (S2). ✅
- «Остаток Платежей» (лента/методы) — отложен в S2 явно, движения видны в кокпите. ✅ (документировано в разделе «Объём»)

**2. Placeholder scan:** заглушек нет; код полный в каждом Step. Денежные/формат-ассерты помечены «подогнать под фактический `formatMoney`» — это не заглушка, а защита от догадки о локали.

**3. Type consistency:**
- `CashTab` = `'sales' | 'orders' | 'shift' | 'review'` — согласован между CashTabBar (Task 8.1), cashModel (8.2), CashWorkspace (8.4).
- `CashShiftActionsClient` определён в Task 5, импортируется в Task 6 (header) и используется в тестах.
- Пропсы `CashShiftHeader` (session/shiftNonce/onShiftChanged/actions) — заданы в Task 6, потребляются в Task 8.4.
- `shiftNonce` — единый счётчик: CashWorkspace (8.4) → header (6) + CashShiftWorkspace (7); оба кладут его в deps load-эффекта.
- `ShiftRevenueDto` — единый источник статуса (shiftId/state/cash/earned), `buildCashHeader` уже мапит.

## Execution Handoff

После сохранения плана — выбор исполнения (Subagent-Driven рекомендуется, по образцу S0).
