# Operator «Клиенты» — Slice S2 (power-tools + PIN) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Дать оператору три power-инструмента в карточке клиента — ручную денежную корректировку, полный возврат операции со строки истории и установку/сброс PIN — поверх уже существующих бэкенд-эндпоинтов.

**Architecture:** Чистый фронт-слайс (бэкенд не меняется). Три новые презентационные модалки (`CorrectionModal`/`RefundModal`/`PinModal`) на готовом `PanelModal`; оркестратор `BackendPlayersWorkspace` держит их состояние и выполняет действия через единый `runClientAction` (тот же паттерн, что top-up/debt). Кнопки-входы добавляются в `WalletSection` (корректировка), `HistorySection` (возврат на не-реверс строках) и шапку `ClientDetail` (PIN). Каждое действие гейтится `hasPermission`.

**Tech Stack:** React 18 + TypeScript, `@afk4/i18n`, `bun test` (happy-dom + @testing-library/react + jest-dom), Vite, lucide-react.

## Global Constraints

- **Бэкенд НЕ трогаем.** Эндпоинты уже есть: `POST /api/players/{id}/ledger/manual-corrections`, `POST /api/players/{id}/ledger/{ledgerEntryId}/refunds`, `POST /api/branches/{branchId}/players/{playerAccountId}/pin`. Никаких изменений в `src/AFK4.Platform.Api` / `src/AFK4.Shared.Contracts`. dotnet-гейты для S2 не нужны.
- **Деньги — minor units** на проводе; ввод парсится `parseMoneyInputMinorUnits` (`string → number|null`), отображается `formatMinorUnits(minor, currency)`. Бонус-время не трогаем: `quantitySeconds: 0`.
- **Знак суммы корректировки** определяется направлением: `credit` → `+amount`, `debit` → `-amount`. `accountType ∈ {'wallet','debt'}`.
- **Возврат — полный**: `amount` = полная сумма записи (`Math.abs(entry.amount.minorUnits)`), доступен только для записей с `reversesLedgerEntryId == null` (нельзя вернуть реверс).
- **Idempotency** — каждый write-вызов получает свежий `createIdempotencyKey('<op-name>')`.
- **Права** (точные, проверены в коде): корректировка → `billing.manual_correction`, возврат → `billing.refund`, PIN → `players.create`. Гейт каждого действия через `hasPermission`; недоступные входы — скрыты/задизейблены.
- **i18n**: новые ключи `op.players.correction.*` / `op.players.refund.*` / `op.players.pin.*` во ВСЕХ трёх локалях `locales/{ru,en,tg}.json`; tg — **реальный таджикский**, не копия ru (guard `messages.test`/`voice.test`). Никаких кириллических ALL-CAPS 4+ букв и слова «компьютер» (guard `voice.test`).
- **Тесты**: `bun test` (happy-dom + jest-dom), `I18nProvider initialLocale="ru"`, ассерты по русским меткам. Презентационные секции мокают колбэки через `mock(() => {})`, API не мокают. App.test — отдельным процессом (уже в `bun run test`).
- **Команды проверки:**
  - i18n: `cd packages/i18n && bun run gen && bun test`
  - фронт оператора: `cd src/AFK4.Operator.App.Web && bun run test`
  - сборка: `cd src/AFK4.Operator.App.Web && bun run build`

## File Structure

**Создаём:**
- `src/AFK4.Operator.App.Web/src/players/CorrectionModal.tsx` + `CorrectionModal.test.tsx` — модалка ручной корректировки (счёт + направление + сумма + причина).
- `src/AFK4.Operator.App.Web/src/players/RefundModal.tsx` + `RefundModal.test.tsx` — confirm возврата операции (детали записи + причина).
- `src/AFK4.Operator.App.Web/src/players/PinModal.tsx` + `PinModal.test.tsx` — установка/сброс PIN.

**Модифицируем:**
- `locales/{ru,en,tg}.json` — i18n-ключи.
- `src/AFK4.Operator.App.Web/src/operatorPermissions.ts` — 2 новых имени прав.
- `src/AFK4.Operator.App.Web/src/api/clients/players.ts` — 3 request-интерфейса + 3 метода.
- `src/AFK4.Operator.App.Web/src/players/HistorySection.tsx` + `.test.tsx` — кнопка «Вернуть».
- `src/AFK4.Operator.App.Web/src/players/WalletSection.tsx` + `.test.tsx` — ссылка «Ручная корректировка».
- `src/AFK4.Operator.App.Web/src/players/ClientDetail.tsx` + `.test.tsx` — кнопка PIN в шапке + проброс пропсов.
- `src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx` — оркестрация (состояние, действия, монтаж модалок).
- `src/AFK4.Operator.App.Web/src/devMockBackend.ts` — мутируемый журнал + write-обработчики.
- `src/AFK4.Operator.App.Web/src/styles/12-players.css` — baseline-CSS новых элементов.

**Решения по UI (зафиксировано):**
- Power-tools — через `PanelModal` (не новый drawer): память S1 «drawer-каркас перенесён в S2, YAGNI». Возврат — `PanelModal tone="warning"` (опасный визуал), не отдельный `CriticalActionConfirmation`, ради единого паттерна модалок раздела.
- PIN — **кнопка в шапке карточки** (рядом с «Бронь»), НЕ меню `⋯`. Меню `⋯` соберём в S3, когда добавятся «Править профиль»/«Деактивировать» — не плодим полу-меню с одним пунктом (#32 наоборот: законченная кнопка лучше меню-заглушки).
- Корректировка после успеха возвращает `WalletSummaryDto` → сразу `setWalletSummary`. Возврат возвращает `LedgerEntryDto` (новая реверс-запись) → перезагружаем wallet + первую страницу журнала. Журнал освежается через `ledgerReloadNonce` в deps загрузочного эффекта.

---

### Task 1: i18n-ключи (correction / refund / pin)

**Files:**
- Modify: `locales/ru.json`, `locales/en.json`, `locales/tg.json`
- Test: `packages/i18n/src/messages.test.ts` (паритет — существующий), `packages/i18n/src/voice.test.ts` (глоссарий — существующий)

**Interfaces:**
- Produces: ключи `op.players.correction.{title,subtitle,accountLabel,accountWallet,accountDebt,directionLabel,directionCredit,directionDebit,amountLabel,reasonLabel,reasonDefault,submit,openLink}`, `op.players.refund.{title,subtitle,reasonLabel,reasonDefault,confirm,rowBtn}`, `op.players.pin.{title,subtitle,label,hint,submit,openBtn}`, `op.players.actions.{correctionLabel,refundLabel,pinLabel}` (метки feedback), `op.players.error.{noPermCorrection,correctionInvalid,noPermRefund,refundInvalid,noPermPin,pinInvalid}`. Все в ru/en/tg.

- [ ] **Step 1: Добавить ключи в `locales/ru.json`**

Вставь после блока `op.players.error.*` (рядом со строкой `"op.players.error.actionNotConnected"`):

```json
  "op.players.correction.title": "Ручная корректировка",
  "op.players.correction.subtitle": "правка баланса или долга",
  "op.players.correction.accountLabel": "Счёт",
  "op.players.correction.accountWallet": "Кошелёк",
  "op.players.correction.accountDebt": "Долг",
  "op.players.correction.directionLabel": "Направление",
  "op.players.correction.directionCredit": "Начислить",
  "op.players.correction.directionDebit": "Списать",
  "op.players.correction.amountLabel": "Сумма корректировки",
  "op.players.correction.reasonLabel": "Причина",
  "op.players.correction.reasonDefault": "корректировка по сверке",
  "op.players.correction.submit": "Применить корректировку",
  "op.players.correction.openLink": "Ручная корректировка",
  "op.players.refund.title": "Возврат операции",
  "op.players.refund.subtitle": "полный возврат записи",
  "op.players.refund.reasonLabel": "Причина возврата",
  "op.players.refund.reasonDefault": "возврат по запросу клиента",
  "op.players.refund.confirm": "Вернуть операцию",
  "op.players.refund.rowBtn": "Вернуть",
  "op.players.pin.title": "PIN клиента",
  "op.players.pin.subtitle": "вход на игровое место",
  "op.players.pin.label": "Новый PIN",
  "op.players.pin.hint": "Минимум 4 символа.",
  "op.players.pin.submit": "Сохранить PIN",
  "op.players.pin.openBtn": "PIN",
  "op.players.actions.correctionLabel": "Ручная корректировка",
  "op.players.actions.refundLabel": "Возврат операции",
  "op.players.actions.pinLabel": "PIN клиента",
  "op.players.error.noPermCorrection": "Нет прав на ручную корректировку.",
  "op.players.error.correctionInvalid": "Заполните сумму корректировки и причину.",
  "op.players.error.noPermRefund": "Нет прав на возврат операции.",
  "op.players.error.refundInvalid": "Эту операцию нельзя вернуть.",
  "op.players.error.noPermPin": "Нет прав на установку PIN.",
  "op.players.error.pinInvalid": "PIN должен быть не короче 4 символов.",
```

- [ ] **Step 2: Добавить те же ключи в `locales/en.json`**

```json
  "op.players.correction.title": "Manual correction",
  "op.players.correction.subtitle": "adjust balance or debt",
  "op.players.correction.accountLabel": "Account",
  "op.players.correction.accountWallet": "Wallet",
  "op.players.correction.accountDebt": "Debt",
  "op.players.correction.directionLabel": "Direction",
  "op.players.correction.directionCredit": "Credit",
  "op.players.correction.directionDebit": "Debit",
  "op.players.correction.amountLabel": "Correction amount",
  "op.players.correction.reasonLabel": "Reason",
  "op.players.correction.reasonDefault": "reconciliation adjustment",
  "op.players.correction.submit": "Apply correction",
  "op.players.correction.openLink": "Manual correction",
  "op.players.refund.title": "Refund operation",
  "op.players.refund.subtitle": "full refund of the entry",
  "op.players.refund.reasonLabel": "Refund reason",
  "op.players.refund.reasonDefault": "refund at customer request",
  "op.players.refund.confirm": "Refund operation",
  "op.players.refund.rowBtn": "Refund",
  "op.players.pin.title": "Client PIN",
  "op.players.pin.subtitle": "sign-in at a gaming seat",
  "op.players.pin.label": "New PIN",
  "op.players.pin.hint": "At least 4 characters.",
  "op.players.pin.submit": "Save PIN",
  "op.players.pin.openBtn": "PIN",
  "op.players.actions.correctionLabel": "Manual correction",
  "op.players.actions.refundLabel": "Refund operation",
  "op.players.actions.pinLabel": "Client PIN",
  "op.players.error.noPermCorrection": "No permission for manual correction.",
  "op.players.error.correctionInvalid": "Fill in the correction amount and reason.",
  "op.players.error.noPermRefund": "No permission to refund operations.",
  "op.players.error.refundInvalid": "This operation cannot be refunded.",
  "op.players.error.noPermPin": "No permission to set a PIN.",
  "op.players.error.pinInvalid": "PIN must be at least 4 characters.",
```

- [ ] **Step 3: Добавить те же ключи в `locales/tg.json` (реальный таджикский, не копия ru)**

```json
  "op.players.correction.title": "Ислоҳи дастӣ",
  "op.players.correction.subtitle": "ислоҳи баланс ё қарз",
  "op.players.correction.accountLabel": "Ҳисоб",
  "op.players.correction.accountWallet": "Ҳамён",
  "op.players.correction.accountDebt": "Қарз",
  "op.players.correction.directionLabel": "Самт",
  "op.players.correction.directionCredit": "Илова кардан",
  "op.players.correction.directionDebit": "Кам кардан",
  "op.players.correction.amountLabel": "Маблағи ислоҳ",
  "op.players.correction.reasonLabel": "Сабаб",
  "op.players.correction.reasonDefault": "ислоҳ аз рӯи муқоиса",
  "op.players.correction.submit": "Татбиқи ислоҳ",
  "op.players.correction.openLink": "Ислоҳи дастӣ",
  "op.players.refund.title": "Баргардонидани амалиёт",
  "op.players.refund.subtitle": "баргардонидани пурраи сабт",
  "op.players.refund.reasonLabel": "Сабаби баргардонидан",
  "op.players.refund.reasonDefault": "баргардонидан бо дархости мизоҷ",
  "op.players.refund.confirm": "Баргардонидани амалиёт",
  "op.players.refund.rowBtn": "Баргардонидан",
  "op.players.pin.title": "PIN-и мизоҷ",
  "op.players.pin.subtitle": "воридшавӣ ба ҷойи бозӣ",
  "op.players.pin.label": "PIN-и нав",
  "op.players.pin.hint": "Камаш 4 аломат.",
  "op.players.pin.submit": "Захираи PIN",
  "op.players.pin.openBtn": "PIN",
  "op.players.actions.correctionLabel": "Ислоҳи дастӣ",
  "op.players.actions.refundLabel": "Баргардонидани амалиёт",
  "op.players.actions.pinLabel": "PIN-и мизоҷ",
  "op.players.error.noPermCorrection": "Барои ислоҳи дастӣ ҳуқуқ нест.",
  "op.players.error.correctionInvalid": "Маблағи ислоҳ ва сабабро пур кунед.",
  "op.players.error.noPermRefund": "Барои баргардонидани амалиёт ҳуқуқ нест.",
  "op.players.error.refundInvalid": "Ин амалиётро баргардонидан мумкин нест.",
  "op.players.error.noPermPin": "Барои гузоштани PIN ҳуқуқ нест.",
  "op.players.error.pinInvalid": "PIN бояд камаш аз 4 аломат бошад.",
```

- [ ] **Step 4: Регенерировать каталог и прогнать guard-тесты**

Run: `cd packages/i18n && bun run gen && bun test`
Expected: PASS — паритет ключей ru/en/tg сходится, нет `tg===ru`, нет ALL-CAPS/«компьютер».

- [ ] **Step 5: Commit**

```bash
git add locales/ru.json locales/en.json locales/tg.json packages/i18n/src/messages.ts
git commit -m "i18n(operator-clients): ключи power-tools (корректировка/возврат/PIN) ru/en/tg"
```

---

### Task 2: Права + методы API-клиента

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/operatorPermissions.ts:53` (после `approveMoneyAction`)
- Modify: `src/AFK4.Operator.App.Web/src/api/clients/players.ts`

**Interfaces:**
- Consumes: `PlatformApiClient` (`api.post<TResponse, TRequest>(path, body)`), `WalletSummaryDto`, `LedgerEntryDto`, `MoneyDto`, `Guid`.
- Produces:
  - `permissionNames.manualCorrection = 'billing.manual_correction'`, `permissionNames.refundLedgerEntry = 'billing.refund'`.
  - `ManualLedgerCorrectionRequest { organizationId, accountType, amount, quantitySeconds, reason, idempotencyKey }`.
  - `RefundLedgerEntryRequest { organizationId, ledgerEntryId, amount, reason, idempotencyKey }`.
  - `SetPlayerPinRequest { pin }`.
  - Методы клиента: `manualCorrection(playerAccountId, request): Promise<WalletSummaryDto>`, `refundLedgerEntry(playerAccountId, ledgerEntryId, request): Promise<LedgerEntryDto>`, `setPlayerPin(branchId, playerAccountId, request): Promise<void>`.

- [ ] **Step 1: Добавить имена прав в `operatorPermissions.ts`**

В объект `permissionNames`, после строки `approveMoneyAction: 'billing.money_action.approve',` (строка 53):

```ts
  manualCorrection: 'billing.manual_correction',
  refundLedgerEntry: 'billing.refund',
```

(В `workspacePermissionRules.players` НЕ добавляем — это под-действия карточки, не правила открытия раздела.)

- [ ] **Step 2: Добавить request-интерфейсы в `api/clients/players.ts`**

После `interface PurchasePackageRequest` (строка 96):

```ts
export interface ManualLedgerCorrectionRequest {
  organizationId: Guid;
  accountType: string;
  amount: MoneyDto;
  quantitySeconds: number;
  reason: string;
  idempotencyKey: string;
}

export interface RefundLedgerEntryRequest {
  organizationId: Guid;
  ledgerEntryId: Guid;
  amount: MoneyDto;
  reason: string;
  idempotencyKey: string;
}

export interface SetPlayerPinRequest {
  pin: string;
}
```

- [ ] **Step 3: Добавить методы в фабрику `createPlayerClient` (после `payDebt`, перед закрывающей `}`)**

```ts
    manualCorrection(playerAccountId: Guid, request: ManualLedgerCorrectionRequest): Promise<WalletSummaryDto> {
      return api.post<WalletSummaryDto, ManualLedgerCorrectionRequest>(`/api/players/${playerAccountId}/ledger/manual-corrections`, request);
    },
    refundLedgerEntry(playerAccountId: Guid, ledgerEntryId: Guid, request: RefundLedgerEntryRequest): Promise<LedgerEntryDto> {
      return api.post<LedgerEntryDto, RefundLedgerEntryRequest>(`/api/players/${playerAccountId}/ledger/${ledgerEntryId}/refunds`, request);
    },
    setPlayerPin(branchId: Guid, playerAccountId: Guid, request: SetPlayerPinRequest): Promise<void> {
      return api.post<void, SetPlayerPinRequest>(`/api/branches/${branchId}/players/${playerAccountId}/pin`, request);
    }
```

(Не забудь добавить запятую после `payDebt(...) { ... }`.)

- [ ] **Step 4: Тайпчек**

Run: `cd src/AFK4.Operator.App.Web && bun run build`
Expected: PASS — компиляция чистая (тонкий клиент юнит-тестами не покрывается, как и существующие методы; гейт — тайпчек сборки).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/operatorPermissions.ts src/AFK4.Operator.App.Web/src/api/clients/players.ts
git commit -m "feat(operator-clients): права billing.manual_correction/refund + методы клиента correction/refund/pin"
```

---

### Task 3: CorrectionModal

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/players/CorrectionModal.tsx`
- Test: `src/AFK4.Operator.App.Web/src/players/CorrectionModal.test.tsx`

**Interfaces:**
- Consumes: `PanelModal` (`{ title, subtitle, onClose, children, tone? }`), `useI18n`.
- Produces: `CorrectionAccount = 'wallet' | 'debt'`, `CorrectionDirection = 'credit' | 'debit'`, компонент `CorrectionModal(props)` с пропсами `account, direction, amount, reason, onChangeAccount, onChangeDirection, onChangeAmount, onChangeReason, onClose, onSubmit, busy`.

- [ ] **Step 1: Написать тест `CorrectionModal.test.tsx`**

```tsx
import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { CorrectionModal } from './CorrectionModal';

afterEach(cleanup);

const renderModal = (over: Partial<Parameters<typeof CorrectionModal>[0]> = {}) => {
  const onSubmit = mock(() => {});
  const onChangeDirection = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <CorrectionModal
        account="wallet"
        direction="credit"
        amount="50.00"
        reason="сверка"
        onChangeAccount={() => {}}
        onChangeDirection={onChangeDirection}
        onChangeAmount={() => {}}
        onChangeReason={() => {}}
        onClose={() => {}}
        onSubmit={onSubmit}
        busy={false}
        {...over}
      />
    </I18nProvider>
  );
  return { onSubmit, onChangeDirection };
};

describe('CorrectionModal', () => {
  it('renders amount and reason fields', () => {
    renderModal();
    expect(screen.getByLabelText('Сумма корректировки')).toBeInTheDocument();
    expect(screen.getByLabelText('Причина')).toBeInTheDocument();
  });

  it('fires onChangeDirection when «Списать» is clicked', () => {
    const { onChangeDirection } = renderModal();
    fireEvent.click(screen.getByRole('button', { name: 'Списать' }));
    expect(onChangeDirection).toHaveBeenCalledWith('debit');
  });

  it('fires onSubmit on form submit', () => {
    const { onSubmit } = renderModal();
    fireEvent.click(screen.getByRole('button', { name: /Применить корректировку/ }));
    expect(onSubmit).toHaveBeenCalled();
  });

  it('disables submit while busy', () => {
    renderModal({ busy: true });
    expect(screen.getByRole('button', { name: /Применить корректировку/ })).toBeDisabled();
  });
});
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/CorrectionModal.test.tsx`
Expected: FAIL — `Cannot find module './CorrectionModal'`.

- [ ] **Step 3: Реализовать `CorrectionModal.tsx`**

```tsx
import { useI18n } from '@afk4/i18n';
import { SlidersHorizontal } from 'lucide-react';
import { PanelModal } from '../PanelModal';

export type CorrectionAccount = 'wallet' | 'debt';
export type CorrectionDirection = 'credit' | 'debit';

// Ручная денежная корректировка (wallet/debt). Презентационный компонент: реальный вызов —
// в оркестраторе. Знак суммы задаёт направление (credit=+, debit=−); бонус-время не трогаем.
export function CorrectionModal({
  account,
  direction,
  amount,
  reason,
  onChangeAccount,
  onChangeDirection,
  onChangeAmount,
  onChangeReason,
  onClose,
  onSubmit,
  busy,
}: {
  account: CorrectionAccount;
  direction: CorrectionDirection;
  amount: string;
  reason: string;
  onChangeAccount: (value: CorrectionAccount) => void;
  onChangeDirection: (value: CorrectionDirection) => void;
  onChangeAmount: (value: string) => void;
  onChangeReason: (value: string) => void;
  onClose: () => void;
  onSubmit: () => void;
  busy: boolean;
}) {
  const { t } = useI18n();

  return (
    <PanelModal
      title={t('op.players.correction.title')}
      subtitle={t('op.players.correction.subtitle')}
      onClose={onClose}
    >
      <form
        className="clients-correction-form"
        onSubmit={(event) => {
          event.preventDefault();
          onSubmit();
        }}
      >
        <fieldset className="clients-segment">
          <legend>{t('op.players.correction.accountLabel')}</legend>
          <button type="button" className={account === 'wallet' ? 'active' : ''} disabled={busy} onClick={() => onChangeAccount('wallet')}>
            {t('op.players.correction.accountWallet')}
          </button>
          <button type="button" className={account === 'debt' ? 'active' : ''} disabled={busy} onClick={() => onChangeAccount('debt')}>
            {t('op.players.correction.accountDebt')}
          </button>
        </fieldset>

        <fieldset className="clients-segment">
          <legend>{t('op.players.correction.directionLabel')}</legend>
          <button type="button" className={direction === 'credit' ? 'active' : ''} disabled={busy} onClick={() => onChangeDirection('credit')}>
            {t('op.players.correction.directionCredit')}
          </button>
          <button type="button" className={direction === 'debit' ? 'active' : ''} disabled={busy} onClick={() => onChangeDirection('debit')}>
            {t('op.players.correction.directionDebit')}
          </button>
        </fieldset>

        <label htmlFor="correction-amount">{t('op.players.correction.amountLabel')}</label>
        <input
          id="correction-amount"
          inputMode="decimal"
          value={amount}
          disabled={busy}
          onChange={(event) => onChangeAmount(event.currentTarget.value)}
        />

        <label htmlFor="correction-reason">{t('op.players.correction.reasonLabel')}</label>
        <input
          id="correction-reason"
          value={reason}
          disabled={busy}
          onChange={(event) => onChangeReason(event.currentTarget.value)}
        />

        <button type="submit" className="clients-primary-action" disabled={busy}>
          <SlidersHorizontal size={15} aria-hidden="true" />
          {t('op.players.correction.submit')}
        </button>
      </form>
    </PanelModal>
  );
}
```

- [ ] **Step 4: Запустить тест — должен пройти**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/CorrectionModal.test.tsx`
Expected: PASS (4 теста).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/players/CorrectionModal.tsx src/AFK4.Operator.App.Web/src/players/CorrectionModal.test.tsx
git commit -m "feat(operator-clients): CorrectionModal — ручная денежная корректировка"
```

---

### Task 4: RefundModal

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/players/RefundModal.tsx`
- Test: `src/AFK4.Operator.App.Web/src/players/RefundModal.test.tsx`

**Interfaces:**
- Consumes: `PanelModal` (`tone="warning"`), `LedgerEntryDto` (из `../operatorApiClients`), `projectLedgerEntry` (из `./playersModel`), `formatMinorUnits`.
- Produces: компонент `RefundModal({ entry, currencyCode, reason, onChangeReason, onClose, onConfirm, busy })`.

- [ ] **Step 1: Написать тест `RefundModal.test.tsx`**

```tsx
import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { LedgerEntryDto } from '../operatorApiClients';
import { RefundModal } from './RefundModal';

afterEach(cleanup);

const entry = (over: Partial<LedgerEntryDto> = {}): LedgerEntryDto => ({
  ledgerEntryId: 'le-1',
  organizationId: 'org-1',
  branchId: 'br-1',
  playerAccountId: 'pl-1',
  sessionId: null,
  playerPackageId: null,
  entryType: 'top_up',
  accountType: 'wallet',
  amount: { currencyCode: 'TJS', minorUnits: 50000 },
  quantitySeconds: 0,
  description: 'Пополнение кошелька',
  reason: 'Касса',
  reversesLedgerEntryId: null,
  createdByStaffUserId: 'st-1',
  createdAtUtc: '2026-05-13T10:00:00Z',
  ...over,
});

const renderModal = (over: Partial<Parameters<typeof RefundModal>[0]> = {}) => {
  const onConfirm = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <RefundModal
        entry={entry()}
        currencyCode="TJS"
        reason="возврат"
        onChangeReason={() => {}}
        onClose={() => {}}
        onConfirm={onConfirm}
        busy={false}
        {...over}
      />
    </I18nProvider>
  );
  return { onConfirm };
};

describe('RefundModal', () => {
  it('shows the entry type label and reason field', () => {
    renderModal();
    expect(screen.getByText('Пополнение')).toBeInTheDocument();
    expect(screen.getByLabelText('Причина возврата')).toBeInTheDocument();
  });

  it('fires onConfirm on submit', () => {
    const { onConfirm } = renderModal();
    fireEvent.click(screen.getByRole('button', { name: /Вернуть операцию/ }));
    expect(onConfirm).toHaveBeenCalled();
  });

  it('disables confirm while busy', () => {
    renderModal({ busy: true });
    expect(screen.getByRole('button', { name: /Вернуть операцию/ })).toBeDisabled();
  });
});
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/RefundModal.test.tsx`
Expected: FAIL — `Cannot find module './RefundModal'`.

- [ ] **Step 3: Реализовать `RefundModal.tsx`**

```tsx
import { useI18n } from '@afk4/i18n';
import { Undo2 } from 'lucide-react';
import type { LedgerEntryDto } from '../operatorApiClients';
import { formatMinorUnits } from '../operatorHelpers';
import { PanelModal } from '../PanelModal';
import { projectLedgerEntry } from './playersModel';

// Подтверждение ПОЛНОГО возврата операции (со строки истории). tone=warning — опасное действие.
// Реальный вызов держит оркестратор; сумма возврата = полная сумма записи.
export function RefundModal({
  entry,
  currencyCode,
  reason,
  onChangeReason,
  onClose,
  onConfirm,
  busy,
}: {
  entry: LedgerEntryDto;
  currencyCode: string;
  reason: string;
  onChangeReason: (value: string) => void;
  onClose: () => void;
  onConfirm: () => void;
  busy: boolean;
}) {
  const { t } = useI18n();
  const view = projectLedgerEntry(entry, t);
  const amount = formatMinorUnits(Math.abs(view.amountMinorUnits), view.currencyCode || currencyCode);

  return (
    <PanelModal
      title={t('op.players.refund.title')}
      subtitle={t('op.players.refund.subtitle')}
      onClose={onClose}
      tone="warning"
    >
      <form
        className="clients-refund-form"
        onSubmit={(event) => {
          event.preventDefault();
          onConfirm();
        }}
      >
        <p className="clients-refund-summary">
          <span>{view.typeLabel}</span>
          <strong>{amount}</strong>
          <em>{view.timeLabel}</em>
        </p>

        <label htmlFor="refund-reason">{t('op.players.refund.reasonLabel')}</label>
        <input
          id="refund-reason"
          value={reason}
          disabled={busy}
          onChange={(event) => onChangeReason(event.currentTarget.value)}
        />

        <button type="submit" className="clients-primary-action clients-danger-action" disabled={busy}>
          <Undo2 size={15} aria-hidden="true" />
          {t('op.players.refund.confirm')}
        </button>
      </form>
    </PanelModal>
  );
}
```

- [ ] **Step 4: Запустить тест — должен пройти**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/RefundModal.test.tsx`
Expected: PASS (3 теста).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/players/RefundModal.tsx src/AFK4.Operator.App.Web/src/players/RefundModal.test.tsx
git commit -m "feat(operator-clients): RefundModal — подтверждение полного возврата операции"
```

---

### Task 5: PinModal

**Files:**
- Create: `src/AFK4.Operator.App.Web/src/players/PinModal.tsx`
- Test: `src/AFK4.Operator.App.Web/src/players/PinModal.test.tsx`

**Interfaces:**
- Consumes: `PanelModal`, `useI18n`.
- Produces: компонент `PinModal({ pin, onChangePin, onClose, onSubmit, busy })`. Кнопка submit задизейблена при `pin.trim().length < 4` или `busy`.

- [ ] **Step 1: Написать тест `PinModal.test.tsx`**

```tsx
import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { PinModal } from './PinModal';

afterEach(cleanup);

const renderModal = (over: Partial<Parameters<typeof PinModal>[0]> = {}) => {
  const onSubmit = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <PinModal
        pin="1234"
        onChangePin={() => {}}
        onClose={() => {}}
        onSubmit={onSubmit}
        busy={false}
        {...over}
      />
    </I18nProvider>
  );
  return { onSubmit };
};

describe('PinModal', () => {
  it('renders the PIN field', () => {
    renderModal();
    expect(screen.getByLabelText('Новый PIN')).toBeInTheDocument();
  });

  it('disables submit when PIN is shorter than 4 chars', () => {
    renderModal({ pin: '12' });
    expect(screen.getByRole('button', { name: /Сохранить PIN/ })).toBeDisabled();
  });

  it('fires onSubmit when PIN is valid', () => {
    const { onSubmit } = renderModal({ pin: '4567' });
    fireEvent.click(screen.getByRole('button', { name: /Сохранить PIN/ }));
    expect(onSubmit).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/PinModal.test.tsx`
Expected: FAIL — `Cannot find module './PinModal'`.

- [ ] **Step 3: Реализовать `PinModal.tsx`**

```tsx
import { useI18n } from '@afk4/i18n';
import { KeyRound } from 'lucide-react';
import { PanelModal } from '../PanelModal';

// Установка/сброс PIN клиента (вход на игровое место). Презентационный; валидация ≥4 — зеркало бэка.
export function PinModal({
  pin,
  onChangePin,
  onClose,
  onSubmit,
  busy,
}: {
  pin: string;
  onChangePin: (value: string) => void;
  onClose: () => void;
  onSubmit: () => void;
  busy: boolean;
}) {
  const { t } = useI18n();
  const tooShort = pin.trim().length < 4;

  return (
    <PanelModal title={t('op.players.pin.title')} subtitle={t('op.players.pin.subtitle')} onClose={onClose}>
      <form
        className="clients-pin-form"
        onSubmit={(event) => {
          event.preventDefault();
          onSubmit();
        }}
      >
        <label htmlFor="client-pin">{t('op.players.pin.label')}</label>
        <input
          id="client-pin"
          inputMode="numeric"
          autoFocus
          value={pin}
          disabled={busy}
          onChange={(event) => onChangePin(event.currentTarget.value)}
        />
        <span className="clients-pin-hint">{t('op.players.pin.hint')}</span>

        <button type="submit" className="clients-primary-action" disabled={busy || tooShort}>
          <KeyRound size={15} aria-hidden="true" />
          {t('op.players.pin.submit')}
        </button>
      </form>
    </PanelModal>
  );
}
```

- [ ] **Step 4: Запустить тест — должен пройти**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/PinModal.test.tsx`
Expected: PASS (3 теста).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/players/PinModal.tsx src/AFK4.Operator.App.Web/src/players/PinModal.test.tsx
git commit -m "feat(operator-clients): PinModal — установка/сброс PIN клиента"
```

---

### Task 6: HistorySection — кнопка «Вернуть»

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/players/HistorySection.tsx`
- Test: `src/AFK4.Operator.App.Web/src/players/HistorySection.test.tsx`

**Interfaces:**
- Consumes: существующие пропсы HistorySection + `LedgerEntryDto`, `projectLedgerEntry`.
- Produces: новые пропсы `canRefund: boolean`, `onRefund: (entry: LedgerEntryDto) => void`. Кнопка «Вернуть» рендерится в строке **только** при `canRefund && !view.isReversal`.

- [ ] **Step 1: Добавить тесты в `HistorySection.test.tsx`**

Найди фабрику `entry` (существующую) и добавь два теста в `describe('HistorySection', ...)`:

```tsx
  it('renders a refund button on non-reversal rows when canRefund', () => {
    const onRefund = mock(() => {});
    renderSection({ entries: [entry({ entryType: 'top_up', reversesLedgerEntryId: null })], canRefund: true, onRefund });
    fireEvent.click(screen.getByRole('button', { name: 'Вернуть' }));
    expect(onRefund).toHaveBeenCalled();
  });

  it('hides the refund button on reversal rows', () => {
    renderSection({ entries: [entry({ entryType: 'refund', reversesLedgerEntryId: 'le-001' })], canRefund: true });
    expect(screen.queryByRole('button', { name: 'Вернуть' })).toBeNull();
  });
```

В хелпере `renderSection` добавь дефолты `canRefund: false` и `onRefund: mock(() => {})` (если хелпер задаёт пропсы явно — пробрось эти два через `...over`). Точная правка хелпера зависит от его текущей формы; обеспечь, что `canRefund`/`onRefund` прокидываются в `<HistorySection>`.

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/HistorySection.test.tsx`
Expected: FAIL — кнопки «Вернуть» нет / проп не принимается.

- [ ] **Step 3: Реализовать изменения в `HistorySection.tsx`**

Добавь импорт иконки (строка 2 — расширить существующий импорт lucide):

```tsx
import { History, RefreshCw, Undo2 } from 'lucide-react';
```

Добавь в сигнатуру пропсов (после `loading: boolean`):

```tsx
  canRefund,
  onRefund,
```
и в типы пропсов (после `loading: boolean;`):
```tsx
  canRefund: boolean;
  onRefund: (entry: LedgerEntryDto) => void;
```

В рендере строки, между `</div>` (закрытие `client-history-body`) и `<b className="client-history-amount">`, добавь действие. Текущий блок:

```tsx
                  </div>
                  <b className="client-history-amount">{sign}{amount}</b>
```
заменить на:
```tsx
                  </div>
                  <div className="client-history-aside">
                    <b className="client-history-amount">{sign}{amount}</b>
                    {canRefund && !view.isReversal && (
                      <button
                        type="button"
                        className="client-history-refund"
                        onClick={() => onRefund(raw)}
                      >
                        <Undo2 size={13} aria-hidden="true" />
                        {t('op.players.refund.rowBtn')}
                      </button>
                    )}
                  </div>
```

- [ ] **Step 4: Запустить тест — должен пройти**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/HistorySection.test.tsx`
Expected: PASS (включая новые два теста).

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/players/HistorySection.tsx src/AFK4.Operator.App.Web/src/players/HistorySection.test.tsx
git commit -m "feat(operator-clients): HistorySection — кнопка «Вернуть» на не-реверс строках"
```

---

### Task 7: WalletSection — ссылка «Ручная корректировка»

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/players/WalletSection.tsx`
- Test: `src/AFK4.Operator.App.Web/src/players/WalletSection.test.tsx`

**Interfaces:**
- Produces: новые пропсы `canCorrect: boolean`, `onCorrect: () => void`. Кнопка-ссылка «Ручная корректировка» рендерится после долговой формы, только при `canCorrect`.

- [ ] **Step 1: Добавить тест в `WalletSection.test.tsx`**

В хелпер `renderSection` добавь дефолты `canCorrect: false`, `onCorrect: mock(() => {})` и пробрось их в `<WalletSection>` (вместе с возвратом `onCorrect` из хелпера). Добавь тест:

```tsx
  it('fires onCorrect when the correction link is clicked', () => {
    const onCorrect = mock(() => {});
    renderSection({ canCorrect: true, onCorrect });
    fireEvent.click(screen.getByRole('button', { name: /Ручная корректировка/ }));
    expect(onCorrect).toHaveBeenCalled();
  });

  it('hides the correction link without permission', () => {
    renderSection({ canCorrect: false });
    expect(screen.queryByRole('button', { name: /Ручная корректировка/ })).toBeNull();
  });
```

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/WalletSection.test.tsx`
Expected: FAIL — ссылки нет / проп не принимается.

- [ ] **Step 3: Реализовать изменения в `WalletSection.tsx`**

Добавь иконку в импорт (строка 2):

```tsx
import { CircleDollarSign, ReceiptText, SlidersHorizontal } from 'lucide-react';
```

В деструктуризацию пропсов (после `onPayDebt,`) добавь:
```tsx
  canCorrect,
  onCorrect,
```
и в типы (после `onPayDebt: () => void;`):
```tsx
  canCorrect: boolean;
  onCorrect: () => void;
```

Перед закрывающим `</div>` секции (после долговой `</form>`) добавь:

```tsx
      {canCorrect && (
        <button type="button" className="clients-wallet-correction-link" onClick={onCorrect}>
          <SlidersHorizontal size={14} aria-hidden="true" />
          {t('op.players.correction.openLink')}
        </button>
      )}
```

- [ ] **Step 4: Запустить тест — должен пройти**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/WalletSection.test.tsx`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/players/WalletSection.tsx src/AFK4.Operator.App.Web/src/players/WalletSection.test.tsx
git commit -m "feat(operator-clients): WalletSection — вход в ручную корректировку"
```

---

### Task 8: ClientDetail — кнопка PIN + проброс пропсов

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/players/ClientDetail.tsx`
- Test: `src/AFK4.Operator.App.Web/src/players/ClientDetail.test.tsx`

**Interfaces:**
- Consumes: пропсы Wallet/History из Task 6/7.
- Produces: новые пропсы ClientDetail: `canSetPin: boolean`, `onSetPin: () => void`, `canCorrect: boolean`, `onCorrect: () => void`, `canRefund: boolean`, `onRefund: (entry: LedgerEntryDto) => void`. Кнопка «PIN» в шапке (рядом с «Бронь»), gated `canSetPin`. Пропсы Wallet/History проброшены вниз.

- [ ] **Step 1: Добавить тесты в `ClientDetail.test.tsx`**

Добавь в существующий рендер-хелпер дефолты новых пропсов (`canSetPin: false`, `onSetPin: mock`, `canCorrect: false`, `onCorrect: mock`, `canRefund: false`, `onRefund: mock`) и пробрось в `<ClientDetail>`. Добавь тесты:

```tsx
  it('shows the PIN button in the header when canSetPin', () => {
    const onSetPin = mock(() => {});
    renderDetail({ canSetPin: true, onSetPin });
    fireEvent.click(screen.getByRole('button', { name: /PIN/ }));
    expect(onSetPin).toHaveBeenCalled();
  });

  it('hides the PIN button without permission', () => {
    renderDetail({ canSetPin: false });
    expect(screen.queryByRole('button', { name: /PIN/ })).toBeNull();
  });
```

(Если у клиента в фикстуре теста `client === null`, обеспечь непустого клиента, чтобы шапка рендерилась.)

- [ ] **Step 2: Запустить тест — убедиться, что падает**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/ClientDetail.test.tsx`
Expected: FAIL — кнопки PIN нет.

- [ ] **Step 3: Реализовать изменения в `ClientDetail.tsx`**

Добавь иконку в импорт (строка 2):
```tsx
import { CalendarClock, KeyRound } from 'lucide-react';
```

В тип пропсов (после `onCreateReservation: () => void;`) добавь:
```tsx
  canSetPin: boolean;
  onSetPin: () => void;
  canCorrect: boolean;
  onCorrect: () => void;
  canRefund: boolean;
  onRefund: (entry: LedgerEntryDto) => void;
```

В шапке `client-detail-head`, перед кнопкой `client-detail-reservation`, добавь кнопку PIN:
```tsx
        {props.canSetPin && (
          <button type="button" className="client-detail-pin" onClick={props.onSetPin}>
            <KeyRound size={15} aria-hidden="true" />
            {t('op.players.pin.openBtn')}
          </button>
        )}
```

В рендере `<WalletSection ...>` добавь пропсы:
```tsx
            canCorrect={props.canCorrect}
            onCorrect={props.onCorrect}
```

В рендере `<HistorySection ...>` добавь пропсы:
```tsx
            canRefund={props.canRefund}
            onRefund={props.onRefund}
```

- [ ] **Step 4: Запустить тест — должен пройти**

Run: `cd src/AFK4.Operator.App.Web && bun test src/players/ClientDetail.test.tsx`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/players/ClientDetail.tsx src/AFK4.Operator.App.Web/src/players/ClientDetail.test.tsx
git commit -m "feat(operator-clients): ClientDetail — кнопка PIN + проброс корректировки/возврата"
```

---

### Task 9: Оркестратор — состояние, действия, монтаж модалок

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx`
- Test: `src/AFK4.Operator.App.Web/src/App.test.tsx` (smoke через `gotoWorkspace('Клиенты')` — отдельный прогон)

**Interfaces:**
- Consumes: методы клиента `manualCorrection`/`refundLedgerEntry`/`setPlayerPin`, права `manualCorrection`/`refundLedgerEntry`/`createPlayerAccount`, модалки `CorrectionModal`/`RefundModal`/`PinModal`, типы `CorrectionAccount`/`CorrectionDirection`.
- Produces: расширенный `PlayerActionId`, новые флаги прав `canManualCorrect`/`canRefundLedger`/`canSetClientPin`, проброс новых пропсов в `<ClientDetail>`, монтаж трёх модалок.

- [ ] **Step 1: Импорты и тип действия**

Добавь импорты:
```tsx
import { CorrectionModal, type CorrectionAccount, type CorrectionDirection } from './players/CorrectionModal';
import { RefundModal } from './players/RefundModal';
import { PinModal } from './players/PinModal';
```

Расширь тип действия (строка 27):
```tsx
type PlayerActionId = 'topUp' | 'writeOffDebt' | 'buyPackage' | 'booking' | 'newCard' | 'correction' | 'refund' | 'setPin';
```

- [ ] **Step 2: Состояние модалок/полей**

После `const [ledgerLoading, setLedgerLoading] = useState(false);` (строка 52) добавь:
```tsx
  const [correctionOpen, setCorrectionOpen] = useState(false);
  const [correctionAccount, setCorrectionAccount] = useState<CorrectionAccount>('wallet');
  const [correctionDirection, setCorrectionDirection] = useState<CorrectionDirection>('credit');
  const [correctionAmount, setCorrectionAmount] = useState('50.00');
  const [correctionReason, setCorrectionReason] = useState(() => t('op.players.correction.reasonDefault'));
  const [refundTarget, setRefundTarget] = useState<LedgerEntryDto | null>(null);
  const [refundReason, setRefundReason] = useState(() => t('op.players.refund.reasonDefault'));
  const [pinOpen, setPinOpen] = useState(false);
  const [pinValue, setPinValue] = useState('');
  const [ledgerReloadNonce, setLedgerReloadNonce] = useState(0);
```

- [ ] **Step 3: Журнал перезагружается по nonce**

В массив зависимостей загрузочного эффекта журнала (строки 191-200) добавь `ledgerReloadNonce`:
```tsx
    ledgerFilter,
    canViewLedger,
    ledgerReloadNonce
```
И добавь хелпер рядом с `changeLedgerFilter`:
```tsx
  const bumpLedger = () => setLedgerReloadNonce((n) => n + 1);
```

- [ ] **Step 4: Флаги прав**

После `canCreateClientReservation` (строка 250) добавь:
```tsx
  const canManualCorrect = backend !== null
    && selectedClient !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && hasPermission(backend.session, permissionNames.manualCorrection);
  const canRefundLedger = backend !== null
    && selectedClient !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && hasPermission(backend.session, permissionNames.refundLedgerEntry);
  const canSetClientPin = backend !== null
    && selectedClient !== null
    && selectedClient.source === 'backend'
    && Boolean(selectedClient.playerAccountId)
    && hasPermission(backend.session, permissionNames.createPlayerAccount);
```

- [ ] **Step 5: Ветки действий в `runClientAction`**

Перед `else { throw new Error(t('op.players.error.actionNotConnected')); }` (строка 391) добавь три ветки:

```tsx
      } else if (id === 'correction') {
        if (!hasPermission(nextBackend.session, permissionNames.manualCorrection)) {
          throw new Error(t('op.players.error.noPermCorrection'));
        }

        const backendClient = requireSelectedBackendClient();

        const magnitude = parseMoneyInputMinorUnits(correctionAmount);
        const reason = correctionReason.trim();
        if (magnitude === null || magnitude <= 0 || !reason) {
          throw new Error(t('op.players.error.correctionInvalid'));
        }

        const signed = correctionDirection === 'debit' ? -magnitude : magnitude;
        const wallet = await apiClients.players.manualCorrection(backendClient.playerAccountId, {
          organizationId: nextBackend.session.organizationId,
          accountType: correctionAccount,
          amount: { currencyCode, minorUnits: signed },
          quantitySeconds: 0,
          reason,
          idempotencyKey: createIdempotencyKey('manual-correction')
        });
        setWalletSummary(wallet);
        bumpLedger();
        setCorrectionOpen(false);
      } else if (id === 'refund') {
        if (!hasPermission(nextBackend.session, permissionNames.refundLedgerEntry)) {
          throw new Error(t('op.players.error.noPermRefund'));
        }

        const backendClient = requireSelectedBackendClient();
        if (refundTarget === null || refundTarget.reversesLedgerEntryId !== null) {
          throw new Error(t('op.players.error.refundInvalid'));
        }

        const reason = refundReason.trim();
        await apiClients.players.refundLedgerEntry(backendClient.playerAccountId, refundTarget.ledgerEntryId, {
          organizationId: nextBackend.session.organizationId,
          ledgerEntryId: refundTarget.ledgerEntryId,
          amount: { currencyCode, minorUnits: Math.abs(refundTarget.amount.minorUnits) },
          reason,
          idempotencyKey: createIdempotencyKey('ledger-refund')
        });
        const wallet = await apiClients.players.getWalletSummary(backendClient.playerAccountId);
        setWalletSummary(wallet);
        bumpLedger();
        setRefundTarget(null);
      } else if (id === 'setPin') {
        if (!hasPermission(nextBackend.session, permissionNames.createPlayerAccount)) {
          throw new Error(t('op.players.error.noPermPin'));
        }

        const backendClient = requireSelectedBackendClient();
        const pin = pinValue.trim();
        if (pin.length < 4) {
          throw new Error(t('op.players.error.pinInvalid'));
        }

        await apiClients.players.setPlayerPin(nextBackend.branchId, backendClient.playerAccountId, { pin });
        setPinValue('');
        setPinOpen(false);
```

- [ ] **Step 6: Проброс пропсов в `<ClientDetail>` и монтаж модалок**

В `<ClientDetail ...>` (после `onCreateReservation={...}`) добавь:
```tsx
          canSetPin={canSetClientPin}
          onSetPin={() => setPinOpen(true)}
          canCorrect={canManualCorrect}
          onCorrect={() => setCorrectionOpen(true)}
          canRefund={canRefundLedger}
          onRefund={(entry) => setRefundTarget(entry)}
```

Перед закрывающим `</main>` (после блока `{newClientOpen && (...)}`) добавь:
```tsx
      {correctionOpen && (
        <CorrectionModal
          account={correctionAccount}
          direction={correctionDirection}
          amount={correctionAmount}
          reason={correctionReason}
          onChangeAccount={setCorrectionAccount}
          onChangeDirection={setCorrectionDirection}
          onChangeAmount={setCorrectionAmount}
          onChangeReason={setCorrectionReason}
          onClose={() => setCorrectionOpen(false)}
          onSubmit={() => void runClientAction('correction', t('op.players.actions.correctionLabel'))}
          busy={feedback.state === 'pending'}
        />
      )}

      {refundTarget !== null && (
        <RefundModal
          entry={refundTarget}
          currencyCode={currencyCode}
          reason={refundReason}
          onChangeReason={setRefundReason}
          onClose={() => setRefundTarget(null)}
          onConfirm={() => void runClientAction('refund', t('op.players.actions.refundLabel'))}
          busy={feedback.state === 'pending'}
        />
      )}

      {pinOpen && (
        <PinModal
          pin={pinValue}
          onChangePin={setPinValue}
          onClose={() => setPinOpen(false)}
          onSubmit={() => void runClientAction('setPin', t('op.players.actions.pinLabel'))}
          busy={feedback.state === 'pending'}
        />
      )}
```

- [ ] **Step 7: Тайпчек + smoke**

Run: `cd src/AFK4.Operator.App.Web && bun run build`
Expected: PASS (типы сходятся).

Run: `cd src/AFK4.Operator.App.Web && bun test src/App.test.tsx`
Expected: PASS — раздел «Клиенты» по-прежнему открывается без ошибок.

- [ ] **Step 8: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/BackendPlayersWorkspace.tsx
git commit -m "feat(operator-clients): оркестрация power-tools — корректировка/возврат/PIN"
```

---

### Task 10: dev-mock — мутируемый журнал + write-обработчики

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/devMockBackend.ts`

**Interfaces:**
- Produces: журнал `pl-1` становится мутируемым (модульный массив); write-эндпоинты `manual-corrections` / `refunds` / `pin` отражаются в `walletSummary`/`ledger` превью.

- [ ] **Step 1: Превратить журнал в мутируемое хранилище**

Замени статический `ledgerLog()` на ленивую инициализацию мутируемого массива. Найди `function ledgerLog(): Array<Record<string, unknown>> { ... }` и сразу после него добавь:

```ts
// Мутируемое хранилище журнала для превью: write-действия (корректировка/возврат) добавляют сюда
// записи, чтобы История/баланс в превью обновлялись. Лениво инициализируется из ledgerLog().
let mutableLedger: Array<Record<string, unknown>> | null = null;
function ledger(): Array<Record<string, unknown>> {
  if (mutableLedger === null) mutableLedger = ledgerLog();
  return mutableLedger;
}
let nextLedgerSeq = 1000;
function prependLedger(entry: Record<string, unknown>): void {
  ledger().unshift(entry);
}
```

Замени использование `ledgerLog()` в `ledgerPage` (строка 312) и `walletSummary` (строка 331) на `ledger()`:
- В `ledgerPage`: `let all = ledger();`
- В `walletSummary`: `recentEntries: ledger().slice(0, 5)`.

- [ ] **Step 2: Динамический баланс кошелька в превью**

Замени `function walletSummary()` на версию, считающую баланс/долг из журнала (чтобы корректировка/возврат были видны):

```ts
function walletSummary() {
  const log = ledger();
  const sumByAccount = (account: string) =>
    log.filter((e) => e.accountType === account)
      .reduce((acc, e) => acc + ((e.amount as { minorUnits: number }).minorUnits ?? 0), 0);
  const wallet = 45000 + sumByAccount('wallet') - ledgerLogWalletBaseline;
  const debt = Math.max(0, sumByAccount('debt'));
  return { playerAccountId: 'pl-1', walletBalance: money(wallet), debtBalance: money(debt), recentEntries: log.slice(0, 5) };
}
```

И добавь рядом константу базовой суммы кошелька из исходного журнала (чтобы стартовый баланс остался 45000):

```ts
// Сумма wallet-проводок в исходном журнале — вычитаем, чтобы стартовый баланс превью был 45000.
const ledgerLogWalletBaseline = ledgerLog()
  .filter((e) => e.accountType === 'wallet')
  .reduce((acc, e) => acc + ((e.amount as { minorUnits: number }).minorUnits ?? 0), 0);
```

(Размести `ledgerLogWalletBaseline` выше `walletSummary`.)

- [ ] **Step 3: Write-обработчики в `devMockFetch`**

Перед финальным `if (method !== 'GET') { return noContent(); }` (строка 391) добавь:

```ts
  if (url.pathname.endsWith('/ledger/manual-corrections') && method === 'POST') {
    let req: Record<string, unknown> = {};
    try { req = JSON.parse(String(init?.body ?? '{}')) as Record<string, unknown>; } catch { req = {}; }
    const amount = req.amount as { currencyCode: string; minorUnits: number } | undefined;
    prependLedger({
      ledgerEntryId: `le-c${nextLedgerSeq++}`, organizationId: ORG, branchId: BRANCH, playerAccountId: 'pl-1',
      sessionId: null, playerPackageId: null, entryType: 'manual_correction',
      accountType: (req.accountType as string) ?? 'wallet', amount: amount ?? money(0),
      quantitySeconds: 0, description: 'Ручная корректировка', reason: (req.reason as string) ?? '',
      reversesLedgerEntryId: null, createdByStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
      createdAtUtc: minutesAgoUtc(0)
    });
    return json(walletSummary());
  }
  if (url.pathname.endsWith('/refunds') && url.pathname.includes('/ledger/') && method === 'POST') {
    let req: Record<string, unknown> = {};
    try { req = JSON.parse(String(init?.body ?? '{}')) as Record<string, unknown>; } catch { req = {}; }
    const amount = req.amount as { currencyCode: string; minorUnits: number } | undefined;
    const reversedId = (req.ledgerEntryId as string) ?? null;
    const entry = {
      ledgerEntryId: `le-r${nextLedgerSeq++}`, organizationId: ORG, branchId: BRANCH, playerAccountId: 'pl-1',
      sessionId: null, playerPackageId: null, entryType: 'refund', accountType: 'wallet',
      amount: money(-Math.abs(amount?.minorUnits ?? 0)), quantitySeconds: 0,
      description: 'Возврат операции', reason: (req.reason as string) ?? '',
      reversesLedgerEntryId: reversedId, createdByStaffUserId: '3db1367b-88c6-4b1c-99c3-bcbb5f4d5134',
      createdAtUtc: minutesAgoUtc(0)
    };
    prependLedger(entry);
    return json(entry);
  }
  if (url.pathname.endsWith('/pin') && method === 'POST') {
    return noContent();
  }
```

- [ ] **Step 4: Smoke превью вручную (опционально, не в CI)**

`cd src/AFK4.Operator.App.Web && bun run dev` → открыть http://127.0.0.1:5174/ → раздел «Клиенты» → проверить: ссылка «Ручная корректировка» открывает модалку и после применения меняет баланс и добавляет запись; «Вернуть» на строке отрабатывает; «PIN» сохраняется без ошибки.

- [ ] **Step 5: Тайпчек**

Run: `cd src/AFK4.Operator.App.Web && bun run build`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/devMockBackend.ts
git commit -m "feat(operator-clients): dev-mock — мутируемый журнал + writes корректировки/возврата/PIN"
```

---

### Task 11: CSS baseline + финальная проверка

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/styles/12-players.css`

**Interfaces:**
- Produces: визуальный baseline для `.clients-segment`, `.clients-correction-form`, `.clients-refund-form`, `.clients-refund-summary`, `.clients-pin-form`, `.clients-pin-hint`, `.clients-wallet-correction-link`, `.client-detail-pin`, `.client-history-aside`, `.client-history-refund`, `.clients-danger-action`.

- [ ] **Step 1: Добавить baseline-CSS**

Добавь в конец `src/styles/12-players.css` (используй существующие токены/переменные темы по образцу соседних правил файла; цвета — через CSS-переменные раздела, не хардкод hex, если файл так устроен):

```css
/* S2 power-tools: сегменты, формы модалок, входные кнопки */
.clients-segment {
  display: flex;
  gap: 6px;
  border: none;
  padding: 0;
  margin: 0;
}
.clients-segment legend {
  font-size: 12px;
  opacity: 0.7;
  margin-bottom: 4px;
}
.clients-segment button {
  flex: 1;
  padding: 8px 10px;
  border-radius: 8px;
  cursor: pointer;
}
.clients-segment button.active {
  font-weight: 600;
}
.clients-correction-form,
.clients-refund-form,
.clients-pin-form {
  display: flex;
  flex-direction: column;
  gap: 10px;
}
.clients-refund-summary {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 8px;
  margin: 0;
}
.clients-pin-hint {
  font-size: 12px;
  opacity: 0.7;
}
.clients-wallet-correction-link {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  background: none;
  border: none;
  cursor: pointer;
  padding: 6px 0;
}
.client-detail-pin {
  display: inline-flex;
  align-items: center;
  gap: 6px;
  cursor: pointer;
}
.client-history-aside {
  display: flex;
  flex-direction: column;
  align-items: flex-end;
  gap: 4px;
}
.client-history-refund {
  display: inline-flex;
  align-items: center;
  gap: 4px;
  font-size: 12px;
  background: none;
  border: none;
  cursor: pointer;
}
```

**Примечание исполнителю:**
- Кнопка подтверждения возврата в `RefundModal` имеет класс `clients-primary-action clients-danger-action`. Дай `.clients-danger-action` **реальный** опасный акцент по образцу существующего правила `.critical-confirmation .danger` в CSS оператора (НЕ оставляй пустое правило — пустой селектор ревью забракует). Если в `.critical-confirmation .danger` используется конкретный danger-токен/переменная — переиспользуй её.
- Сверь остальные токены/переменные с соседними правилами `12-players.css` (этот файл уже использует тему раздела — синий акцент оператора, dark baseline). Если в файле приняты конкретные переменные (`--clients-*`, `--shell-*`), используй их вместо общих значений; правила выше — структурный baseline, а не финальная палитра.

- [ ] **Step 2: Полный прогон тестов оператора**

Run: `cd src/AFK4.Operator.App.Web && bun run test`
Expected: PASS — все subdir-тесты (вкл. новые CorrectionModal/RefundModal/PinModal и обновлённые Wallet/History/ClientDetail) + App.test.

- [ ] **Step 3: Сборка**

Run: `cd src/AFK4.Operator.App.Web && bun run build`
Expected: PASS.

- [ ] **Step 4: i18n guard**

Run: `cd packages/i18n && bun test`
Expected: PASS — паритет ru/en/tg + voice/глоссарий.

- [ ] **Step 5: Commit**

```bash
git add src/AFK4.Operator.App.Web/src/styles/12-players.css
git commit -m "style(operator-clients): baseline-CSS power-tools (сегменты/модалки/входные кнопки)"
```

---

## Self-Review

**Spec coverage** (против `2026-06-22-operator-clients-overhaul-design.md`, разделы «Power-tools (S2)» и «PIN клиента (S2)»):
- Ручная корректировка `POST .../ledger/manual-corrections`, только деньги, `quantitySeconds=0` → Task 2 (клиент) + Task 3 (UI) + Task 9 (действие). ✓
- Возврат `POST .../ledger/{id}/refunds`, полная сумма, только `reversesLedgerEntryId == null` → Task 2 + Task 4 + Task 6 (скрытие на реверсах) + Task 9 (guard `reversesLedgerEntryId !== null`). ✓
- Право корректировки/возврата (спека: «подтвердить при реализации») → подтверждено: `billing.manual_correction` / `billing.refund`, Task 2. ✓
- PIN `POST /api/branches/{branchId}/players/{id}/pin`, ≥4, право `players.create` → Task 2 + Task 5 + Task 8 (вход) + Task 9. ✓
- Гейтинг каждого действия `hasPermission`, недоступные скрыты → Task 6/7/8 (условный рендер) + Task 9 (флаги + проверка внутри действия). ✓
- dev-mock write-эндпоинтов (#14, иначе превью не видно) → Task 10. ✓
- i18n ru/en/tg, tg реальный → Task 1. ✓
- Тесты компонентов + App.test отдельным прогоном → каждая UI-задача + Task 9/11. ✓

**Placeholder scan:** код приведён полностью во всех code-шагах; CSS-задача явно помечает структурный baseline и отсылает к токенам файла (не плейсхолдер логики). Нет «add error handling/TBD».

**Type consistency:** `CorrectionAccount`/`CorrectionDirection` определены в Task 3, импортированы в Task 9. Методы клиента `manualCorrection`/`refundLedgerEntry`/`setPlayerPin` определены в Task 2, вызваны в Task 9 с теми же сигнатурами. Пропсы `canCorrect`/`onCorrect` (Wallet, Task 7), `canRefund`/`onRefund` (History, Task 6), `canSetPin`/`onSetPin`+`canCorrect`/`canRefund` (ClientDetail, Task 8) — проброшены оркестратором в Task 9 одноимённо. `ledgerReloadNonce` объявлен и добавлен в deps в Task 9. `bumpLedger` объявлен в Task 9 Step 3, вызван в Step 5.

**Риск-ноты для исполнителя:**
- Точная форма рендер-хелперов в `HistorySection.test.tsx`/`WalletSection.test.tsx`/`ClientDetail.test.tsx` может отличаться — добавляй новые пропсы в их `over`-механизм, не ломая существующие тесты.
- `GuardLegacyMoneyAction` на бэке может отклонить над-пороговую корректировку/возврат (уход в approval) — это нормально: ошибка прилетит в `catch` и покажется через `projectOperatorError`. Отдельной обработки в S2 не делаем.
