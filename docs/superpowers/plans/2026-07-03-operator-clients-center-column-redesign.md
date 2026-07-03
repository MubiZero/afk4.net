# Operator «Клиенты» — редизайн центральной колонки — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Превратить центральную карточку раздела «Клиенты» в tabless-воркспейс на всю ширину (список 300px | карточка), где История переезжает внутрь карточки правой колонкой, а верхняя зона даёт крупные Баланс/Долг + быстрое inline-пополнение; лишняя ширина рейла истории устраняется.

**Architecture:** Чисто фронтовая переработка раскладки/презентации раздела `AFK4.Operator.App.Web`. Master-detail сохраняется; меняется внутренность detail-панели (`ClientDetail`): вкладки и правый рейл `ClientLedgerRail` удаляются, `WalletSection` расщепляется на новый `WalletZone` (плитки + inline-пополнение + вторичные кнопки) и новый `PayDebtModal` (диалог погашения долга). Оркестратор `BackendPlayersWorkspace` упрощается (нет `showLedgerRail`/`activeTab`/`useMediaQuery`), журнал грузится всегда при выбранном клиенте.

**Tech Stack:** React 18 + TypeScript, `@afk4/i18n`, атомы `.ui-*` (CSS в `styles/`), `<Money>`/`<LedgerRow>`/`PanelModal`/`EmptyState`/`Skeleton` из `operatorPrimitives`. Тесты: `bun test` (happy-dom + @testing-library/react + jest-dom). Сборка/тайпчек: `tsc -b && vite build`.

## Global Constraints

- **Бэкенд/деньги/права не трогаем.** Ноль изменений в эндпоинтах, money-path, `IsActive`-guard, правах. Только фронтовая раскладка/презентация.
- **Атомы, не новые компоненты кнопок/чипов.** Весь UI на существующих `.ui-*` + `<Money>`/`<LedgerRow>`/`PanelModal`. Новые CSS-правила — только раскладочные, в `styles/12-players.css`.
- **i18n — реюз существующих ключей `op.players.*`; новых ключей не добавляем** (все нужные строки уже есть: `chip.balance`, `chip.debt`, `actions.topUpAmountLabel`, `actions.topUpBtn`, `actions.writeOffDebtBtn`, `actions.debtAmountLabel`, `actions.debtReasonLabel`, `actions.writeOffDebtDefault`, `correction.openLink`, `wallet.payDebtTitle`, `tabs.packages`, `ledgerRail.title`). Если строка реально отсутствует — добавить в ru/tg/en настоящим переводом (не копией; см. tg-i18n-honesty), не хардкодить.
- **Анти-мигание не ломать.** `playersSnapshotCache`, `usePlayersPreload`, snapshot-логика, скелетоны, `isLoading`-гейты empty-state — сохраняются как есть.
- **Каскад-ловушка (durable-урок S2/S3):** контейнер с descendant-селектором `.X button` (специфичность 0,1,1) бьёт атом `.ui-btn` (0,1,0). Не оборачивать `.ui-btn` в контейнеры, у которых есть правило `.X button {…}`. Новые контейнеры (`.clients-wallet-zone`, `.clients-detail-split`, `.clients-paydebt-form`) НЕ должны иметь тег-селектор `button`.
- **Money-span ловушка (durable S3):** правило метки `.X span` ловит вложенный `<span class="ui-money">`. Метки стат-плиток скоупить на прямых детей (`.ui-card--stat > span`), что уже сделано в атоме — не регрессировать.
- **Формат денег:** только `<Money minorUnits … currencyCode … />`; в ленте операций `<Money signed>`.
- **Гейт слайса:** финал обязан включать И `bun test` (полный), И `bun run build` (tsc тайпчекает тест-файлы и сужения — зелёный `bun test` ≠ зелёная сборка).

---

## File Structure

**Создаются:**
- `src/players/WalletZone.tsx` — верхняя зона денег: плитки Баланс/Долг + inline быстрое пополнение + вторичные кнопки (Погасить долг, Ручная корректировка). Презентационный.
- `src/players/WalletZone.test.tsx` — юнит-тесты WalletZone.
- `src/players/PayDebtModal.tsx` — диалог погашения долга (`PanelModal`, tone='danger', поля сумма+причина). Презентационный.
- `src/players/PayDebtModal.test.tsx` — юнит-тесты PayDebtModal.

**Меняются:**
- `src/players/ClientDetail.tsx` — переписать раскладку: убрать вкладки/`showLedgerRail`/мини-ленту; рендерить header → context → `WalletZone` → `.clients-detail-split` (PackagesSection | HistorySection).
- `src/players/ClientDetail.test.tsx` — переписать под tabless.
- `src/BackendPlayersWorkspace.tsx` — убрать `ClientLedgerRail`/`useMediaQuery`/`activeTab`/`showLedgerRail`/`ClientDetailTab`; добавить `payDebtOpen` + рендер `PayDebtModal`; упростить `ledgerPaneVisible`; закрывать модалку долга на успехе; обновить пропсы `ClientDetail`.
- `src/App.test.tsx` — обновить ассерты, завязанные на вкладки Клиентов.
- `src/styles/12-players.css` — новая раскладка (2 колонки, `.clients-wallet-zone`, `.clients-detail-split`, `.clients-paydebt-form`); удалить CSS вкладок/рейла/старого кошелька.

**Удаляются:**
- `src/players/ClientLedgerRail.tsx` — рейл больше не нужен.
- `src/players/WalletSection.tsx` — расщеплён на `WalletZone` + `PayDebtModal`.
- `src/players/WalletSection.test.tsx` — вместе с компонентом.

---

## Task 1: Компонент `WalletZone`

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/players/WalletZone.tsx`
- Test: `src/AFK4.Operator.App.Web/src/players/WalletZone.test.tsx`

**Interfaces:**
- Produces: `WalletZone(props)` со свойствами:
  `balanceMinorUnits: number`, `debtMinorUnits: number`, `currencyCode: string`,
  `topUpAmount: string`, `canTopUp: boolean`, `onChangeTopUpAmount: (v: string) => void`, `onTopUp: () => void`,
  `canPayDebt: boolean`, `onOpenPayDebt: () => void`, `canCorrect: boolean`, `onCorrect: () => void`.
  Кнопка «Погасить долг» рендерится только при `debtMinorUnits > 0` (disabled при `!canPayDebt`).
  Кнопка «Ручная корректировка» рендерится только при `canCorrect`.
- Consumes: `Money` из `../operatorPrimitives`, `useI18n` из `@afk4/i18n`.

- [ ] **Step 1: Написать падающий тест**

Create `src/AFK4.Operator.App.Web/src/players/WalletZone.test.tsx`:

```tsx
import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { WalletZone } from './WalletZone';

afterEach(cleanup);

const base = {
  balanceMinorUnits: 45000,
  debtMinorUnits: 0,
  currencyCode: 'TJS',
  topUpAmount: '',
  canTopUp: true,
  onChangeTopUpAmount: () => {},
  onTopUp: () => {},
  canPayDebt: true,
  onOpenPayDebt: () => {},
  canCorrect: false,
  onCorrect: () => {},
};

const renderZone = (over: Partial<typeof base> = {}) =>
  render(<I18nProvider initialLocale="ru"><WalletZone {...base} {...over} /></I18nProvider>);

describe('WalletZone', () => {
  it('renders two money stat cards (balance + debt)', () => {
    renderZone({ balanceMinorUnits: 45000, debtMinorUnits: 3500 });
    expect(document.querySelectorAll('.ui-card--stat')).toHaveLength(2);
    expect(screen.getByText('450 с.')).toHaveClass('ui-money');
  });

  it('marks the debt card danger only when debt is present', () => {
    const { rerender } = renderZone({ debtMinorUnits: 0 });
    expect(document.querySelector('.ui-card--stat.is-danger')).toBeNull();
    rerender(<I18nProvider initialLocale="ru"><WalletZone {...base} debtMinorUnits={3500} /></I18nProvider>);
    expect(document.querySelector('.ui-card--stat.is-danger')).not.toBeNull();
  });

  it('fires onTopUp when the inline top-up form is submitted', () => {
    const onTopUp = mock(() => {});
    renderZone({ onTopUp });
    fireEvent.click(screen.getByRole('button', { name: /Пополнить/ }));
    expect(onTopUp).toHaveBeenCalled();
  });

  it('exposes the amount field labelled "Сумма пополнения"', () => {
    renderZone();
    expect(screen.getByLabelText('Сумма пополнения')).toBeInTheDocument();
  });

  it('hides the pay-debt button when there is no debt', () => {
    renderZone({ debtMinorUnits: 0 });
    expect(screen.queryByRole('button', { name: /Погасить долг|Списать долг/ })).toBeNull();
  });

  it('shows the pay-debt button and fires onOpenPayDebt when debt is present', () => {
    const onOpenPayDebt = mock(() => {});
    renderZone({ debtMinorUnits: 3500, onOpenPayDebt });
    fireEvent.click(screen.getByRole('button', { name: /Погасить долг|Списать долг/ }));
    expect(onOpenPayDebt).toHaveBeenCalled();
  });

  it('hides the correction button without permission and fires onCorrect with it', () => {
    const onCorrect = mock(() => {});
    const { rerender } = renderZone({ canCorrect: false });
    expect(screen.queryByRole('button', { name: /корректировк/i })).toBeNull();
    rerender(<I18nProvider initialLocale="ru"><WalletZone {...base} canCorrect onCorrect={onCorrect} /></I18nProvider>);
    fireEvent.click(screen.getByRole('button', { name: /корректировк/i }));
    expect(onCorrect).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Прогнать тест — убедиться, что падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/WalletZone.test.tsx`
Expected: FAIL — `Cannot find module './WalletZone'`.

- [ ] **Step 3: Реализовать `WalletZone`**

Create `src/AFK4.Operator.App.Web/src/players/WalletZone.tsx`:

```tsx
import { useI18n } from '@afk4/i18n';
import { CircleDollarSign, ReceiptText, SlidersHorizontal } from 'lucide-react';
import { Money } from '../operatorPrimitives';

// Верхняя зона карточки клиента: крупные плитки Баланс/Долг + быстрое inline-пополнение
// (одно поле суммы + кнопка; причина необязательна — дефолт подставляется в оркестраторе при
// сабмите). Вторичные действия — кнопки: «Погасить долг» (только при долге, открывает диалог) и
// «Ручная корректировка» (только при праве). Возврат живёт построчно в Истории, не здесь.
export function WalletZone({
  balanceMinorUnits,
  debtMinorUnits,
  currencyCode,
  topUpAmount,
  canTopUp,
  onChangeTopUpAmount,
  onTopUp,
  canPayDebt,
  onOpenPayDebt,
  canCorrect,
  onCorrect,
}: {
  balanceMinorUnits: number;
  debtMinorUnits: number;
  currencyCode: string;
  topUpAmount: string;
  canTopUp: boolean;
  onChangeTopUpAmount: (value: string) => void;
  onTopUp: () => void;
  canPayDebt: boolean;
  onOpenPayDebt: () => void;
  canCorrect: boolean;
  onCorrect: () => void;
}) {
  const { t } = useI18n();
  const hasDebt = debtMinorUnits > 0;

  return (
    <div className="clients-wallet-zone">
      <div className="ui-card ui-card--stat clients-wallet-balance">
        <span>{t('op.players.chip.balance')}</span>
        <strong><Money minorUnits={balanceMinorUnits} currencyCode={currencyCode} /></strong>
      </div>
      <div className={`ui-card ui-card--stat clients-wallet-debt${hasDebt ? ' is-danger' : ''}`}>
        <span>{t('op.players.chip.debt')}</span>
        <strong><Money minorUnits={debtMinorUnits} currencyCode={currencyCode} /></strong>
      </div>

      <form
        className="clients-wallet-quickpay"
        onSubmit={(event) => {
          event.preventDefault();
          onTopUp();
        }}
      >
        <div className="ui-field">
          <label htmlFor="wallet-topup-amount">{t('op.players.actions.topUpAmountLabel')}</label>
          <input
            id="wallet-topup-amount"
            inputMode="decimal"
            placeholder="0.00"
            value={topUpAmount}
            disabled={!canTopUp}
            onChange={(event) => onChangeTopUpAmount(event.currentTarget.value)}
          />
        </div>
        <button type="submit" className="ui-btn ui-btn--primary" disabled={!canTopUp}>
          <CircleDollarSign size={15} aria-hidden="true" />
          {t('op.players.actions.topUpBtn')}
        </button>
      </form>

      {(hasDebt || canCorrect) && (
        <div className="clients-wallet-secondary">
          {hasDebt && (
            <button type="button" className="ui-btn ui-btn--danger" disabled={!canPayDebt} onClick={onOpenPayDebt}>
              <ReceiptText size={14} aria-hidden="true" />
              {t('op.players.actions.writeOffDebtBtn')}
            </button>
          )}
          {canCorrect && (
            <button type="button" className="ui-btn ui-btn--ghost" onClick={onCorrect}>
              <SlidersHorizontal size={14} aria-hidden="true" />
              {t('op.players.correction.openLink')}
            </button>
          )}
        </div>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Прогнать тест — убедиться, что проходит**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/WalletZone.test.tsx`
Expected: PASS (7 tests). Примечание: метка кнопки долга берётся из `op.players.actions.writeOffDebtBtn` — тест матчит regex `Погасить долг|Списать долг`, чтобы не зависеть от точной формулировки ключа.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/players/WalletZone.tsx src/AFK4.Operator.App.Web/src/players/WalletZone.test.tsx
git commit -m "feat(operator-clients): WalletZone — плитки Баланс/Долг + inline-пополнение + вторичные кнопки"
```

---

## Task 2: Компонент `PayDebtModal`

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/players/PayDebtModal.tsx`
- Test: `src/AFK4.Operator.App.Web/src/players/PayDebtModal.test.tsx`

**Interfaces:**
- Produces: `PayDebtModal(props)` со свойствами:
  `amount: string`, `reason: string`, `onChangeAmount: (v: string) => void`, `onChangeReason: (v: string) => void`,
  `onClose: () => void`, `onSubmit: () => void`, `busy: boolean`.
- Consumes: `PanelModal` из `../PanelModal`.

- [ ] **Step 1: Написать падающий тест**

Create `src/AFK4.Operator.App.Web/src/players/PayDebtModal.test.tsx`:

```tsx
import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { PayDebtModal } from './PayDebtModal';

afterEach(cleanup);

const base = {
  amount: '',
  reason: '',
  onChangeAmount: () => {},
  onChangeReason: () => {},
  onClose: () => {},
  onSubmit: () => {},
  busy: false,
};

const renderModal = (over: Partial<typeof base> = {}) =>
  render(<I18nProvider initialLocale="ru"><PayDebtModal {...base} {...over} /></I18nProvider>);

describe('PayDebtModal', () => {
  it('renders the pay-debt title and amount/reason fields', () => {
    renderModal();
    expect(screen.getByText('Погасить долг')).toBeInTheDocument();
    expect(screen.getByLabelText('Сумма долга')).toBeInTheDocument();
    expect(screen.getByLabelText('Причина долга')).toBeInTheDocument();
  });

  it('fires onSubmit when the form is submitted', () => {
    const onSubmit = mock(() => {});
    renderModal({ onSubmit });
    fireEvent.click(screen.getByRole('button', { name: /Погасить долг|Списать долг/ }));
    expect(onSubmit).toHaveBeenCalled();
  });

  it('disables the submit button while busy', () => {
    renderModal({ busy: true });
    expect(screen.getByRole('button', { name: /Погасить долг|Списать долг/ })).toBeDisabled();
  });
});
```

Примечание: метки полей — `op.players.actions.debtAmountLabel` («Сумма долга») и `op.players.actions.debtReasonLabel` («Причина долга»); заголовок — `op.players.wallet.payDebtTitle` («Погасить долг»). Если фактические строки в словаре отличаются — подставить фактические в ассерты (проверить `packages/i18n/src/messages.ts`), не менять ключи.

- [ ] **Step 2: Прогнать тест — убедиться, что падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/PayDebtModal.test.tsx`
Expected: FAIL — `Cannot find module './PayDebtModal'`.

- [ ] **Step 3: Реализовать `PayDebtModal`**

Create `src/AFK4.Operator.App.Web/src/players/PayDebtModal.tsx`:

```tsx
import { useI18n } from '@afk4/i18n';
import { ReceiptText } from 'lucide-react';
import { PanelModal } from '../PanelModal';

// Диалог погашения долга. Презентационный: реальный вызов — в оркестраторе (writeOffDebt).
// Раньше был всегда развёрнутой inline-формой в WalletSection; теперь открывается кнопкой из
// WalletZone. Та же бизнес-логика, только спрятана до нажатия.
export function PayDebtModal({
  amount,
  reason,
  onChangeAmount,
  onChangeReason,
  onClose,
  onSubmit,
  busy,
}: {
  amount: string;
  reason: string;
  onChangeAmount: (value: string) => void;
  onChangeReason: (value: string) => void;
  onClose: () => void;
  onSubmit: () => void;
  busy: boolean;
}) {
  const { t } = useI18n();

  return (
    <PanelModal title={t('op.players.wallet.payDebtTitle')} tone="danger" onClose={onClose}>
      <form
        className="clients-paydebt-form"
        onSubmit={(event) => {
          event.preventDefault();
          onSubmit();
        }}
      >
        <div className="ui-field">
          <label htmlFor="paydebt-amount">{t('op.players.actions.debtAmountLabel')}</label>
          <input
            id="paydebt-amount"
            inputMode="decimal"
            placeholder="0.00"
            value={amount}
            disabled={busy}
            onChange={(event) => onChangeAmount(event.currentTarget.value)}
          />
        </div>
        <div className="ui-field">
          <label htmlFor="paydebt-reason">{t('op.players.actions.debtReasonLabel')}</label>
          <input
            id="paydebt-reason"
            placeholder={t('op.players.actions.writeOffDebtDefault')}
            value={reason}
            disabled={busy}
            onChange={(event) => onChangeReason(event.currentTarget.value)}
          />
        </div>
        <button type="submit" className="ui-btn ui-btn--danger ui-btn--block" disabled={busy}>
          <ReceiptText size={15} aria-hidden="true" />
          {t('op.players.actions.writeOffDebtBtn')}
        </button>
      </form>
    </PanelModal>
  );
}
```

- [ ] **Step 4: Прогнать тест — убедиться, что проходит**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/PayDebtModal.test.tsx`
Expected: PASS (3 tests).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/players/PayDebtModal.tsx src/AFK4.Operator.App.Web/src/players/PayDebtModal.test.tsx
git commit -m "feat(operator-clients): PayDebtModal — диалог погашения долга (PanelModal, danger)"
```

---

## Task 3: Переписать `ClientDetail` под tabless-воркспейс

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/players/ClientDetail.tsx` (полная замена тела рендера)
- Test: `src/AFK4.Operator.App.Web/src/players/ClientDetail.test.tsx` (полная замена)

**Interfaces:**
- Consumes: `WalletZone` (Task 1), `PackagesSection`, `HistorySection`, `ClientActionsMenu`, `ClientContextStrip`, `EmptyState`, `Money`.
- Produces: `ClientDetail(props)` с НОВЫМ набором пропсов (удалены `activeTab`, `showLedgerRail`, `recentEntries`, `onSelectTab`, `topUpReason`, `onChangeTopUpReason`, `debtAmount`, `debtReason`, `onChangeDebtAmount`, `onChangeDebtReason`, `onPayDebt`; добавлен `onOpenPayDebt: () => void`). Тип `ClientDetailTab` больше НЕ экспортируется. Полный список пропсов — в Step 3.

- [ ] **Step 1: Переписать тест под tabless**

Заменить весь файл `src/AFK4.Operator.App.Web/src/players/ClientDetail.test.tsx` на:

```tsx
import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ClientDetail } from './ClientDetail';
import type { ClientLiveContext } from './playersModel';
import type { PlayerClientItem } from '../operatorHelpers';
import type { LedgerEntryDto, PackageOptionDto, PlayerPackageDto } from '../operatorApiClients';

afterEach(cleanup);

type DetailProps = Parameters<typeof ClientDetail>[0];

const client: PlayerClientItem = {
  playerAccountId: 'p1', name: 'Madina S.', status: 'active', balanceMinorUnits: 46000,
  debtMinorUnits: 0, last: '', tone: 'active', detail: '', phoneNumber: '+992 90 555 22 11', source: 'backend'
};

const baseProps: DetailProps = {
  client,
  isLoading: false,
  liveContext: { session: null, nextBooking: null },
  balanceMinorUnits: 46000,
  debtMinorUnits: 0,
  packageCount: 1,
  currencyCode: 'TJS',
  packages: [] as PlayerPackageDto[],
  options: [] as PackageOptionDto[],
  ledgerEntries: [] as LedgerEntryDto[],
  ledgerFilter: null,
  ledgerHasMore: false,
  ledgerLoading: false,
  onLedgerFilterChange: () => {},
  onLedgerLoadMore: () => {},
  selectedPackageDefinitionId: '',
  packageBusy: false,
  packagesLoading: false,
  topUpAmount: '',
  canTopUp: true,
  canPayDebt: true,
  canPurchase: true,
  canCreateReservation: true,
  canManageClient: false,
  onSetPin: () => {},
  onEditProfile: () => {},
  onToggleActive: () => {},
  canCorrect: false,
  onCorrect: () => {},
  canRefund: false,
  onRefund: () => {},
  onChangeTopUpAmount: () => {},
  onTopUp: () => {},
  onOpenPayDebt: () => {},
  onSelectOption: () => {},
  onBuy: () => {},
  onCreateReservation: () => {},
};

const renderDetail = (over: Partial<DetailProps> = {}) =>
  render(<I18nProvider initialLocale="ru"><ClientDetail {...baseProps} {...over} /></I18nProvider>);

describe('ClientDetail', () => {
  it('shows the empty state when no client is selected', () => {
    renderDetail({ client: null });
    expect(screen.getByText('Нет выбранного клиента')).toBeInTheDocument();
  });

  it('does NOT flash the empty state while the list is still loading', () => {
    renderDetail({ client: null, isLoading: true });
    expect(screen.queryByText('Нет выбранного клиента')).toBeNull();
  });

  it('renders the header, phone and reservation button for a selected client', () => {
    renderDetail();
    expect(screen.getByText('Madina S.')).toBeInTheDocument();
    expect(screen.getByText('+992 90 555 22 11', { exact: false })).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Бронь/ })).toBeInTheDocument();
  });

  it('renders wallet zone, packages and history together — no tabs', () => {
    renderDetail();
    // нет табов вообще
    expect(screen.queryByRole('tab')).toBeNull();
    // все зоны видны одновременно
    expect(screen.getByLabelText('Сумма пополнения')).toBeInTheDocument();     // WalletZone
    expect(screen.getByText('История операций')).toBeInTheDocument();          // History panel heading
  });

  it('shows two money stat cards and the package count', () => {
    renderDetail({ balanceMinorUnits: 45000, debtMinorUnits: 3500, packageCount: 2 });
    expect(screen.getByText('450 с.')).toHaveClass('ui-money');
    expect(document.querySelectorAll('.clients-wallet-zone .ui-card--stat')).toHaveLength(2);
    expect(screen.getByText('Пакеты', { exact: false })).toHaveTextContent('2');
  });

  it('marks the debt stat card as danger only when the client has debt', () => {
    const { rerender } = renderDetail({ debtMinorUnits: 0 });
    expect(document.querySelector('.ui-card--stat.is-danger')).toBeNull();
    rerender(<I18nProvider initialLocale="ru"><ClientDetail {...baseProps} debtMinorUnits={3500} /></I18nProvider>);
    expect(document.querySelector('.ui-card--stat.is-danger')).not.toBeNull();
  });

  it('fires onOpenPayDebt from the pay-debt button when the client has debt', () => {
    const onOpenPayDebt = mock(() => {});
    renderDetail({ debtMinorUnits: 3500, onOpenPayDebt });
    fireEvent.click(screen.getByRole('button', { name: /Погасить долг|Списать долг/ }));
    expect(onOpenPayDebt).toHaveBeenCalled();
  });

  it('fires onCreateReservation when the reservation button is clicked', () => {
    const onCreateReservation = mock(() => {});
    renderDetail({ onCreateReservation });
    fireEvent.click(screen.getByRole('button', { name: /Бронь/ }));
    expect(onCreateReservation).toHaveBeenCalled();
  });

  it('renders the actions menu only with manage permission', () => {
    const { rerender } = renderDetail({ canManageClient: false });
    expect(screen.queryByRole('button', { name: 'Действия с клиентом' })).toBeNull();
    rerender(<I18nProvider initialLocale="ru"><ClientDetail {...baseProps} canManageClient /></I18nProvider>);
    expect(screen.getByRole('button', { name: 'Действия с клиентом' })).toBeInTheDocument();
  });

  it('shows the deactivated banner for an inactive client', () => {
    renderDetail({ client: { ...client, status: 'inactive' } });
    expect(screen.getByText(/Клиент деактивирован/)).toBeInTheDocument();
  });

  it('shows the live-context strip: playing now + next booking', () => {
    renderDetail({
      liveContext: {
        session: { seatName: 'PC-03', untilLabel: '14:30' },
        nextBooking: { timeLabel: '18:00', seatName: null }
      }
    });
    expect(screen.getByText(/PC-03/)).toBeInTheDocument();
    expect(screen.getByText(/18:00/)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Прогнать тест — убедиться, что падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/ClientDetail.test.tsx`
Expected: FAIL (старый `ClientDetail` ещё вкладочный; и/или несоответствие пропсов).

- [ ] **Step 3: Переписать `ClientDetail.tsx`**

Заменить весь файл `src/AFK4.Operator.App.Web/src/players/ClientDetail.tsx` на:

```tsx
import { useI18n } from '@afk4/i18n';
import { CalendarClock } from 'lucide-react';
import type { PlayerClientItem } from '../operatorHelpers';
import type { LedgerEntryDto, PackageOptionDto, PlayerPackageDto } from '../operatorApiClients';
import { EmptyState } from '../operatorPrimitives';
import { playerStatusLabel, type ClientLiveContext } from './playersModel';
import { ClientContextStrip } from './ClientContextStrip';
import { WalletZone } from './WalletZone';
import { PackagesSection } from './PackagesSection';
import { HistorySection } from './HistorySection';
import { ClientActionsMenu } from './ClientActionsMenu';

// Первые две буквы имени как аватар-заглушка.
function initials(name: string): string {
  return name
    .split(' ')
    .map((part) => part[0])
    .join('')
    .slice(0, 2)
    .toUpperCase() || '—';
}

// Центральная карточка-воркспейс (tabless): личность → зона денег → низ в две колонки
// Пакеты | История. Отдельного правого рейла истории больше нет — журнал живёт правой колонкой
// карточки. Данные/фильтр/пагинация держит оркестратор.
export function ClientDetail(props: {
  client: PlayerClientItem | null;
  // Список клиентов ещё грузится: не показываем «нет выбранного клиента», иначе пустая карточка
  // мигает до прихода данных (см. isLoading в ClientList).
  isLoading: boolean;
  liveContext: ClientLiveContext;
  balanceMinorUnits: number;
  debtMinorUnits: number;
  packageCount: number;
  currencyCode: string;
  packages: PlayerPackageDto[];
  options: PackageOptionDto[];
  ledgerEntries: LedgerEntryDto[];
  ledgerFilter: string | null;
  ledgerHasMore: boolean;
  ledgerLoading: boolean;
  onLedgerFilterChange: (entryType: string | null) => void;
  onLedgerLoadMore: () => void;
  selectedPackageDefinitionId: string;
  packageBusy: boolean;
  packagesLoading: boolean;
  topUpAmount: string;
  canTopUp: boolean;
  canPayDebt: boolean;
  canPurchase: boolean;
  canCreateReservation: boolean;
  canManageClient: boolean;
  onSetPin: () => void;
  onEditProfile: () => void;
  onToggleActive: () => void;
  canCorrect: boolean;
  onCorrect: () => void;
  canRefund: boolean;
  onRefund: (entry: LedgerEntryDto) => void;
  onChangeTopUpAmount: (value: string) => void;
  onTopUp: () => void;
  onOpenPayDebt: () => void;
  onSelectOption: (packageDefinitionId: string) => void;
  onBuy: () => void;
  onCreateReservation: () => void;
}) {
  const { t } = useI18n();
  const { client } = props;

  if (client === null) {
    // Пока грузимся — держим панель пустой (без layout-jump), но без «нет выбранного клиента»,
    // чтобы не мигала на входе. Empty-state показываем только когда загрузка устаканилась.
    return (
      <section className="clients-panel clients-detail-panel" aria-hidden={props.isLoading || undefined}>
        {!props.isLoading && (
          <EmptyState
            title={t('op.players.profile.empty')}
            description={t('op.players.profile.emptyNote')}
          />
        )}
      </section>
    );
  }

  return (
    <section className="clients-panel clients-detail-panel">
      <div className="clients-detail-scroll">
        <header className="client-detail-head">
          <div className="client-avatar">{initials(client.name)}</div>
          <div className="client-detail-ident">
            {client.status !== 'active' && (
              <span className={`client-detail-status is-${client.status}`}>{playerStatusLabel(client.status, t)}</span>
            )}
            <strong>{client.name}</strong>
            <em>{client.phoneNumber || t('op.pos.cart.clientNoPhone')}</em>
          </div>
          <div className="client-detail-actions">
            <button
              type="button"
              className="ui-btn"
              disabled={!props.canCreateReservation}
              onClick={props.onCreateReservation}
            >
              <CalendarClock size={15} aria-hidden="true" />
              {t('op.players.detail.reservationBtn')}
            </button>
            {props.canManageClient && (
              <ClientActionsMenu
                isActive={client.status !== 'inactive'}
                onEditProfile={props.onEditProfile}
                onSetPin={props.onSetPin}
                onToggleActive={props.onToggleActive}
              />
            )}
          </div>
        </header>

        {client.status === 'inactive' && (
          <div className="client-detail-banner" role="status">
            {t('op.players.detail.deactivatedBanner')}
          </div>
        )}

        <ClientContextStrip context={props.liveContext} />

        <WalletZone
          balanceMinorUnits={props.balanceMinorUnits}
          debtMinorUnits={props.debtMinorUnits}
          currencyCode={props.currencyCode}
          topUpAmount={props.topUpAmount}
          canTopUp={props.canTopUp}
          onChangeTopUpAmount={props.onChangeTopUpAmount}
          onTopUp={props.onTopUp}
          canPayDebt={props.canPayDebt}
          onOpenPayDebt={props.onOpenPayDebt}
          canCorrect={props.canCorrect}
          onCorrect={props.onCorrect}
        />

        <div className="clients-detail-split">
          <section className="clients-subpanel">
            <header className="clients-subpanel-head">
              <span>{t('op.players.tabs.packages')}</span>
              {props.packageCount > 0 && (
                <span className="ui-chip ui-chip--status ui-chip--xs is-neutral" aria-hidden="true">
                  {props.packageCount}
                </span>
              )}
            </header>
            <PackagesSection
              packages={props.packages}
              options={props.options}
              selectedPackageDefinitionId={props.selectedPackageDefinitionId}
              balanceMinorUnits={props.balanceMinorUnits}
              currencyCode={props.currencyCode}
              canPurchase={props.canPurchase}
              busy={props.packageBusy}
              loading={props.packagesLoading}
              onSelectOption={props.onSelectOption}
              onBuy={props.onBuy}
            />
          </section>

          <section className="clients-subpanel">
            <header className="clients-subpanel-head">
              <span>{t('op.players.ledgerRail.title')}</span>
            </header>
            <HistorySection
              entries={props.ledgerEntries}
              currencyCode={props.currencyCode}
              activeFilter={props.ledgerFilter}
              onFilterChange={props.onLedgerFilterChange}
              hasMore={props.ledgerHasMore}
              onLoadMore={props.onLedgerLoadMore}
              loading={props.ledgerLoading}
              canRefund={props.canRefund}
              onRefund={props.onRefund}
            />
          </section>
        </div>
      </div>
    </section>
  );
}
```

- [ ] **Step 4: Прогнать тест — убедиться, что проходит**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/ClientDetail.test.tsx`
Expected: PASS (все кейсы). Если «Пакеты» heading не находится через `getByText('Пакеты', { exact:false })` из-за счётчика — heading содержит текст «Пакеты» и `2` внутри `.clients-subpanel-head`; матчер `toHaveTextContent('2')` смотрит весь узел. Ок.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/players/ClientDetail.tsx src/AFK4.Operator.App.Web/src/players/ClientDetail.test.tsx
git commit -m "feat(operator-clients): ClientDetail — tabless-воркспейс (WalletZone + Пакеты|История), убраны вкладки"
```

---

## Task 4: Оркестратор `BackendPlayersWorkspace` — убрать рейл/вкладки, подключить PayDebtModal

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx`
- Test: `src/AFK4.Operator.App.Web/src/App.test.tsx` (обновить ассерты вкладок Клиентов)

**Interfaces:**
- Consumes: `ClientDetail` (Task 3, новые пропсы), `PayDebtModal` (Task 2).

- [ ] **Step 1: Обновить импорты и стейт**

В `src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx`:

Удалить импорты (строки ~24, ~27):
```tsx
import { useMediaQuery } from './useMediaQuery';
```
```tsx
import { ClientLedgerRail } from './players/ClientLedgerRail';
```

Заменить импорт ClientDetail (строка ~26) — убрать тип `ClientDetailTab`:
```tsx
import { ClientDetail } from './players/ClientDetail';
```

Добавить импорт PayDebtModal рядом с другими модалками игроков:
```tsx
import { PayDebtModal } from './players/PayDebtModal';
```

Удалить строку `const wideLayout = useMediaQuery('(min-width: 1280px)');` (~41).
Удалить строку `const [activeTab, setActiveTab] = useState<ClientDetailTab>('wallet');` (~47).
Добавить рядом с прочими `*Open` стейтами (после ~48):
```tsx
  const [payDebtOpen, setPayDebtOpen] = useState(false);
```

- [ ] **Step 2: Упростить гейт журнала и удалить `showLedgerRail`**

Удалить строку `const showLedgerRail = wideLayout && canViewLedger;` (~149).

Заменить (~151):
```tsx
  const ledgerPaneVisible = canViewLedger && (showLedgerRail || activeTab === 'history');
```
на:
```tsx
  const ledgerPaneVisible = canViewLedger;
```
(Эффект загрузки журнала уже сам гейтит `selectedClient === null || !selectedClient.playerAccountId` — строка ~198 не трогаем.)

- [ ] **Step 3: Закрывать модалку долга на успехе**

В `runClientAction`, ветка `writeOffDebt`, сразу после `bumpLedger();` (~492) добавить строку:
```tsx
        setPayDebtOpen(false);
```
(Зеркало того, как ветка `correction` делает `setCorrectionOpen(false)` на успехе.)

- [ ] **Step 4: Обновить рендер — раскладка, пропсы ClientDetail, убрать рейл, добавить модалку**

Заменить открывающий тег секции раскладки (~757):
```tsx
      <section className={`clients-layout${showLedgerRail ? ' has-ledger-rail' : ''}`}>
```
на:
```tsx
      <section className="clients-layout">
```

Внутри `<ClientDetail … />` (строки ~775–823) заменить блок пропсов на новый набор (удалить `activeTab`, `showLedgerRail`, `recentEntries`, `onSelectTab`, `topUpReason`, `onChangeTopUpReason`, `debtAmount`, `debtReason`, `onChangeDebtAmount`, `onChangeDebtReason`, `onPayDebt`; добавить `onOpenPayDebt`). Итоговый вызов:

```tsx
        <ClientDetail
          client={selectedClient}
          isLoading={loadStatus === 'loading'}
          liveContext={liveContext}
          balanceMinorUnits={balance}
          debtMinorUnits={debt}
          packageCount={selectedClientPackageCount}
          currencyCode={currencyCode}
          packages={selectedClientPackages}
          options={packageOptions}
          ledgerEntries={ledgerEntries}
          ledgerFilter={ledgerFilter}
          ledgerHasMore={ledgerCursor !== null}
          ledgerLoading={ledgerLoading}
          onLedgerFilterChange={changeLedgerFilter}
          onLedgerLoadMore={() => void loadMoreLedger()}
          selectedPackageDefinitionId={selectedPackageDefinitionId}
          packageBusy={feedback.state === 'pending'}
          packagesLoading={packagesLoading}
          topUpAmount={walletTopUpAmount}
          canTopUp={canTopUpWallet}
          canPayDebt={canPayDebt}
          canPurchase={canPurchasePackage}
          canCreateReservation={canCreateClientReservation}
          onChangeTopUpAmount={setWalletTopUpAmount}
          onTopUp={() => runClientAction('topUp', t('op.players.actions.topUpBtn'))}
          onOpenPayDebt={() => setPayDebtOpen(true)}
          onSelectOption={setSelectedPackageDefinitionId}
          onBuy={() => runClientAction('buyPackage', t('op.players.actions.buyPackageBtn'))}
          onCreateReservation={() => runClientAction('booking', t('op.players.actions.bookingBtn'))}
          canManageClient={canManageClient}
          onSetPin={() => setPinOpen(true)}
          onEditProfile={openEditProfile}
          onToggleActive={() => setActiveStateOpen(true)}
          canCorrect={canManualCorrect}
          onCorrect={() => setCorrectionOpen(true)}
          canRefund={canRefundLedger}
          onRefund={(entry) => setRefundTarget(entry)}
        />
```

Удалить весь блок рендера рейла (строки ~825–837):
```tsx
        {showLedgerRail && (
          <ClientLedgerRail
            entries={ledgerEntries}
            currencyCode={currencyCode}
            activeFilter={ledgerFilter}
            onFilterChange={changeLedgerFilter}
            hasMore={ledgerCursor !== null}
            onLoadMore={() => void loadMoreLedger()}
            loading={ledgerLoading}
            canRefund={canRefundLedger}
            onRefund={(entry) => setRefundTarget(entry)}
          />
        )}
```

Добавить рендер `PayDebtModal` рядом с `CorrectionModal` (после блока `{correctionOpen && (…)}`, ~865):
```tsx
      {payDebtOpen && (
        <PayDebtModal
          amount={debtPaymentAmount}
          reason={debtPaymentReason}
          onChangeAmount={setDebtPaymentAmount}
          onChangeReason={setDebtPaymentReason}
          onClose={() => setPayDebtOpen(false)}
          onSubmit={() => void runClientAction('writeOffDebt', t('op.players.actions.writeOffDebtBtn'))}
          busy={feedback.state === 'pending'}
        />
      )}
```

- [ ] **Step 5: Тайпчек воркспейса — убедиться, что нет висячих ссылок**

Run: `cd src/AFK4.Operator.App.Web && bunx tsc -b`
Expected: успешная компиляция. Если `tsc` ругается на неиспользуемый импорт/переменную (`useMediaQuery`, `ClientDetailTab`, `ClientLedgerRail`, оставшиеся `walletTopUpReason`/`onChangeTopUpReason` использования) — удалить висячие ссылки. Примечание: `walletTopUpReason`/`debtPaymentReason`/`setDebtPaymentReason` ОСТАЮТСЯ (используются в `runClientAction` и `PayDebtModal`); удалять только то, на что больше нет ссылок.

- [ ] **Step 6: Обновить `App.test.tsx` под tabless**

В `src/AFK4.Operator.App.Web/src/App.test.tsx`:

(a) Тест существования вкладок Клиентов (~867–871) — заменить три `getByRole('tab', …)` ассерта на tabless-проверку. Найти блок:
```tsx
    expect(await screen.findByRole('tab', { name: 'Кошелёк' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'Пакеты' })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: 'История' })).toBeInTheDocument();
    // кнопка действия на активном табе (Кошелёк)
    expect(screen.getByRole('button', { name: /Пополнить депозит/ })).toBeInTheDocument();
```
заменить на:
```tsx
    // tabless: зона денег, пакеты и история видны одновременно
    expect(await screen.findByLabelText('Сумма пополнения')).toBeInTheDocument();
    expect(screen.getByText('История операций')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /Пополнить депозит/ })).toBeInTheDocument();
```

(b) Удалить клики по вкладке «Пакеты» перед покупкой пакета/пополнением. Найти обе строки:
```tsx
    fireEvent.click(await screen.findByRole('tab', { name: 'Пакеты' }));
```
(около ~1626 и ~1654) и удалить их — селект «Пакет для покупки» и поле «Сумма пополнения» теперь всегда в DOM (ждать их через существующие `findBy*`).

(c) Прогнать App.test и починить любые остаточные ассерты, завязанные на вкладки/рейл Клиентов (напр. `getByRole('tab', { name: 'Кошелёк'|'История' })` в других кейсах), тем же принципом: вкладок нет, зоны видны сразу. НЕ трогать вкладки Кассы (`Заказы`/`Смена`/`Карта` в PaymentDialog) — это другой раздел.

Run: `cd src/AFK4.Operator.App.Web && bun test src/App.test.tsx`
Expected: PASS (после правок (a)–(c)).

- [ ] **Step 7: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx src/AFK4.Operator.App.Web/src/App.test.tsx
git commit -m "feat(operator-clients): воркспейс — убраны рейл/вкладки, PayDebtModal подключён, журнал грузится всегда"
```

---

## Task 5: CSS-раскладка + удаление мёртвого кода + финальный гейт

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/styles/12-players.css`
- Delete: `src/AFK4.Operator.App.Web/src/players/ClientLedgerRail.tsx`
- Delete: `src/AFK4.Operator.App.Web/src/players/WalletSection.tsx`
- Delete: `src/AFK4.Operator.App.Web/src/players/WalletSection.test.tsx`

- [ ] **Step 1: Удалить мёртвые файлы**

```bash
cd src/AFK4.Operator.App.Web
git rm src/players/ClientLedgerRail.tsx src/players/WalletSection.tsx src/players/WalletSection.test.tsx
```

- [ ] **Step 2: Новая раскладка раздела (2 колонки)**

В `src/AFK4.Operator.App.Web/src/styles/12-players.css` заменить правило `.clients-layout` (~94–101):
```css
.clients-layout {
  display: grid;
  grid-template-columns: minmax(0, 360px) minmax(0, 1fr);
  gap: 10px;
  flex: 1;
  min-height: 0;
  overflow: hidden;
}
```
на:
```css
.clients-layout {
  display: grid;
  grid-template-columns: minmax(0, 300px) minmax(0, 1fr);
  gap: 10px;
  flex: 1;
  min-height: 0;
  overflow: hidden;
}
```

Удалить весь `@media (min-width: 1280px)` блок с `.clients-layout.has-ledger-rail` (~103–110) — раскладка истории через рейл больше не существует.

- [ ] **Step 3: Прокрутка карточки + общая ширина контента**

Карточка теперь скроллит своё содержимое целиком (обёртка `.clients-detail-scroll`). Добавить в `12-players.css` (рядом с `.clients-detail-panel`, ~266):
```css
/* Карточка-воркспейс: единый скролл содержимого; общая макс-ширина контента, чтобы карточка
   реально использовала освободившуюся ширину (история переехала внутрь), но строки не растягивались
   бесконечно. Значение подобрать на превью. */
.clients-detail-scroll {
  --clients-card-max: 1320px;
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
  overflow-y: auto;
  min-height: 0;
  padding: var(--space-3);
}
```

Существующие правила `.client-detail-head`, `.client-detail-tabs`, `.client-detail-chips`, `.client-detail-content` имеют `max-width: 1040px` и собственные `margin/padding`. Т.к. отступы теперь даёт `.clients-detail-scroll`, обновить:
- `.client-detail-head` (~270): убрать `margin: var(--space-3) var(--space-3) 0;`, заменить `max-width: 1040px;` → `max-width: var(--clients-card-max);`.
- `.client-detail-chips` (~339): удалить целиком (стат-плитки теперь внутри `.clients-wallet-zone`, стилизуются ниже) — ЛИБО оставить, если класс ещё где-то используется (проверить `grep client-detail-chips src`; если только в удалённом коде — удалить правило).
- `.client-detail-content` (~387): удалить целиком (контейнера вкладок больше нет).
- `.client-detail-tabs`, `.client-detail-tab`, `.client-detail-tab:hover`, `.client-detail-tab:focus-visible`, `.client-detail-tab.active`, `.client-tab-count` (~347–385): удалить все — вкладок нет.

- [ ] **Step 4: Стили зоны денег `.clients-wallet-zone`**

Добавить в `12-players.css`:
```css
/* Зона денег: широко — Баланс | Долг | быстрое-пополнение в ряд, вторичные кнопки под ними;
   узко — всё в стопку. Тег-селектор `button` тут НЕ использовать (каскад-ловушка с .ui-btn). */
.clients-wallet-zone {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr) minmax(300px, 1.3fr);
  grid-template-areas:
    "balance debt quickpay"
    "secondary secondary secondary";
  gap: var(--space-3);
  max-width: var(--clients-card-max);
}
.clients-wallet-balance { grid-area: balance; }
.clients-wallet-debt { grid-area: debt; }
.clients-wallet-quickpay {
  grid-area: quickpay;
  display: flex;
  align-items: flex-end;
  gap: var(--space-2);
}
.clients-wallet-quickpay .ui-field { flex: 1; }
.clients-wallet-secondary {
  grid-area: secondary;
  display: flex;
  flex-wrap: wrap;
  gap: var(--space-2);
}

@media (max-width: 1100px) {
  .clients-wallet-zone {
    grid-template-columns: minmax(0, 1fr);
    grid-template-areas: "balance" "debt" "quickpay" "secondary";
  }
}
```

- [ ] **Step 5: Стили низа `.clients-detail-split` + подпанели**

Добавить в `12-players.css`:
```css
/* Низ карточки: Пакеты | История рядом; узко — в стопку (Пакеты, затем История). */
.clients-detail-split {
  display: grid;
  grid-template-columns: minmax(0, 1fr) minmax(0, 1fr);
  gap: var(--space-3);
  align-items: start;
  max-width: var(--clients-card-max);
}
@media (max-width: 1100px) {
  .clients-detail-split { grid-template-columns: minmax(0, 1fr); }
}

.clients-subpanel {
  display: flex;
  flex-direction: column;
  min-width: 0;
  border: 1px solid var(--border-soft);
  border-radius: var(--radius-md);
  background: var(--surface-card);
}
.clients-subpanel-head {
  display: flex;
  align-items: center;
  gap: var(--space-2);
  padding: var(--space-3) var(--space-3) var(--space-2);
  color: var(--text-primary);
  font-size: 13px;
  font-weight: 700;
}
.clients-subpanel > .clients-packages-section,
.clients-subpanel > .clients-history-section {
  padding: 0 var(--space-3) var(--space-3);
}

/* Форма диалога погашения долга (PayDebtModal). Без тег-селектора button (каскад-ловушка). */
.clients-paydebt-form {
  display: flex;
  flex-direction: column;
  gap: var(--space-3);
}
```

Примечание: если `.clients-packages-section` / `.clients-history-section` уже имеют собственный внешний padding из старых правил — свести к одному источнику отступа (не дублировать). Проверить визуально на превью (Step 8).

- [ ] **Step 6: Удалить CSS старого кошелька и рейла**

Удалить из `12-players.css` правила, относящиеся к удалённым `WalletSection`/`ClientLedgerRail` (по селекторам): `.clients-wallet-layout`, `.clients-wallet-layout.is-solo`, `.clients-wallet-actions`, `.clients-wallet-form`, `.clients-wallet-form-debt`, `.clients-wallet-fields`, `.clients-wallet-recent`, `.clients-wallet-recent-head`, `.clients-wallet-recent-list`, `.clients-wallet-recent-empty`, `.clients-wallet-recent-link`, `.clients-ledger-rail`, `.clients-ledger-rail-head`, `.clients-ledger-rail-body`, и связанные с ними `@media`-ветки. Оставить общие: `.clients-section-title` (используется в PackagesSection), `.clients-package-*`, `.clients-history-*`, `.ui-*`.

Проверка отсутствия висячих ссылок на удалённые классы в TS/TSX:
```bash
grep -rnE "clients-wallet-(layout|actions|form|fields|recent)|clients-ledger-rail|client-detail-(tabs|tab|content|chips)" src --include=*.tsx --include=*.ts
```
Expected: пусто (все ссылки удалены вместе с компонентами/вкладками).

- [ ] **Step 7: Полный гейт — тесты + сборка**

```bash
cd src/AFK4.Operator.App.Web
bun test
bun run build
```
Expected: `bun test` — все зелёные (включая отдельный прогон `App.test`); `bun run build` — успешная сборка (`tsc -b` тайпчекает тест-файлы и сужения, `vite build` собирает). Если `tsc` ловит несоответствие пропсов/типов — починить по сообщению.

- [ ] **Step 8: Визуальная проверка на превью**

```bash
cd src/AFK4.Operator.App.Web
bun run dev
```
Открыть `http://127.0.0.1:5174/` (dev-mock по умолчанию; см. память operator-theme-and-preview). Проверить на широком окне и на узком (< 1100px):
- Раздел: список 300px | карточка на всю оставшуюся ширину; правого рейла истории нет.
- Карточка tabless: Баланс, Долг, быстрое пополнение, Пакеты, История видны одновременно.
- История ужата (сумма рядом с описанием, без больших дыр), скроллится в своей колонке.
- Долг-плитка красная только при долге; Погасить долг виден только при долге, открывает диалог; Корректировка открывает диалог.
- Быстрое пополнение: сумма + кнопка отрабатывают (в mock — тост-подтверждение).
- Узкое окно: Пакеты и История встают в стопку, вкладок нет; список остаётся.
- Тёмная и светлая темы (переключатель в шапке): деньги читаемы, поверхности-подъём корректны.

Подстроить `--clients-card-max` и брейкпоинт `1100px` при необходимости прямо в `12-players.css`, пере-проверить. Отдать пользователю ссылку на превью (не headless-скриншоты).

- [ ] **Step 9: Commit**

```bash
git add -A
git commit -m "feat(operator-clients): CSS новой раскладки (2 колонки, wallet-zone, split) + удаление рейла/старого кошелька/вкладок"
```

---

## Self-Review (выполнено при написании плана)

**Spec coverage:**
- §2 раскладка `список | карточка`, рейл убран → Task 4 (класс `.clients-layout`), Task 5 Step 2/Step 1 (удаление `ClientLedgerRail`). ✔
- §3.2 зона денег (плитки + inline-пополнение + вторичные кнопки) → Task 1 (`WalletZone`), Task 5 Step 4 (CSS). ✔
- §3.3 низ Пакеты | История → Task 3 (`.clients-detail-split`), Task 5 Step 5 (CSS). ✔
- §4 PayDebtModal → Task 2 + Task 4 (подключение, закрытие на успехе). ✔
- §5 удаления (рейл, WalletSection, вкладки, showLedgerRail, useMediaQuery, упрощение ledger-гейта) → Task 4 + Task 5. ✔
- §6 адаптив (стопка < 1100px, список постоянен, без вкладок) → Task 5 Step 4/5 (`@media`). ✔
- §7 границы (бэкенд/деньги/анти-мигание/атомы/i18n) → Global Constraints + пропсы не трогают money-path. ✔
- §9 критерии → Task 5 Step 7 (гейт) + Step 8 (превью-чеклист). ✔

**Placeholder scan:** код полный во всех Step; единственное осознанно-отложенное значение — `--clients-card-max`/брейкпоинт, помечены «подобрать на превью» с явным Step 8 на подстройку. Нет «TODO/добавить обработку/аналогично Task N». ✔

**Type consistency:** `WalletZone`/`PayDebtModal`/`ClientDetail` пропсы согласованы между Task 1/2/3 и вызовом в Task 4; удалённые пропсы (`activeTab`, `showLedgerRail`, `recentEntries`, `onSelectTab`, `topUpReason`, `onChangeTopUpReason`, `debtAmount`, `debtReason`, `onChangeDebtAmount`, `onChangeDebtReason`, `onPayDebt`) вычищены из вызова; добавленный `onOpenPayDebt` присутствует в типе (Task 3) и вызове (Task 4). `ledgerPaneVisible` остаётся в deps-массиве эффекта как одиночная переменная. ✔
