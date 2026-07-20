# Миграция отставших экранов «Управления» на дизайн-kit — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Перевести четыре отставших экрана раздела «Управление» (Клуб, Лояльность, Платёжные шлюзы, Новости) на общий дизайн-kit (`MgmtTable`/`MgmtDrawer`/`mgmt-form`/`ui-*`/`EmptyState`/`CriticalActionConfirmation`), меняя только разметку и раскладку.

**Architecture:** Каждый экран — тонкий destination-компонент внутри `ManagementScreen`. Логика (API-контракты, стейт-машины, валидация, permission-гейты, money-path guards) переносится 1:1 — правится только JSX-разметка и CSS-классы. Новые строки идут через `@afk4/i18n` в ru/en/tg.

**Tech Stack:** React 18 + TypeScript, `@afk4/i18n`, `bun test` (happy-dom + @testing-library/react), Vite, CSS-модули проекта (`src/styles/*.css`).

## Global Constraints

- **Только разметка/классы меняются.** API-вызовы, стейт-машины, валидация, идемпотентность, permission-гейты и money-path guards — переносятся дословно. Это не рефактор логики.
- **Переиспользовать принятые кирпичи**, не плодить новые классы `payment-card-*`/`news-item-*`/`settings-form-grid` в этих экранах: `MgmtTable`, `MgmtDrawer`, `mgmt-form`/`mgmt-form-grid`/`mgmt-form-wide`/`mgmt-check`/`mgmt-section-title`/`mgmt-form-actions`, `mgmt-master-detail`, `ui-btn`/`ui-btn--primary`/`ui-btn--danger`/`ui-btn--sm`, `ui-chip ui-chip--status` (варианты `is-live`/`is-warning`/`is-danger`/`is-neutral`), примитивы `EmptyState`/`Money`/`CriticalActionConfirmation` из `operatorPrimitives`.
- **i18n паритет обязателен.** Каждый новый ключ добавляется в ru, en И tg в `packages/i18n/src/messages.ts`. Гард `messages.test.ts` требует идентичные наборы ключей во всех трёх локалях. Гард `tg===ru` запрещает копию русского в tg (кроме whitelisted loanwords) — новые tg-значения ниже — **настоящий таджикский**. Гард `i18nKeysExist.test.ts` требует, чтобы каждый литеральный `t('key')` в исходниках оператора существовал в каталоге ru.
- **Тема-aware, surface-иерархия оператора:** светлая тема = ПОДЪЁМ (белая панель + `--shadow-card`), не затемнение. Акцент emerald (`--accent`). Использовать существующие CSS-переменные (`--surface-*`, `--border-*`, `--text-*`, `--space-*`, `--radius-*`, `--success-*`, `--warning-*`, `--danger-*`).
- **Гейт каждого слайса:** `bun test <файлы слайса>` зелёный И `bun run build` зелёный (build тайпчекает и тест-файлы — bun-моки типизировать). Коммит в конце слайса.
- Рабочая директория для команд: `src/AFK4.Operator.App.Web`. `bun` — по полному пути окружения (см. env-quirks). Ветка: `feat/operator-management-redesign` (текущая).

---

## Task 1: Клуб — структурная mgmt-form с read-only мета-строками

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/settings/SettingsProfileSection.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/management/destinations/ClubDestination.tsx` (только className обёртки, если нужно)
- Modify: `packages/i18n/src/messages.ts` (1 новый ключ ×3 локали)
- Modify: `src/AFK4.Operator.App.Web/src/styles/23-management-crud.css` (мета-строки)
- Test: `src/AFK4.Operator.App.Web/src/management/destinations/ClubDestination.test.tsx` (создать)

**Interfaces:**
- Consumes: `ManagementScreen` (save-бар), `SettingsProfileSection` props (`clubName`, `city`, `currencyCode`, `hasBackend`, `onClubNameChange`, `onCityChange`).
- Produces: `SettingsProfileSection` рендерит `mgmt-form` вместо `settings-form-grid`; переиспользуется в `BackendSettingsWorkspace` (проверить, что не сломалось).

> ⚠️ `SettingsProfileSection` используется в двух местах: `ClubDestination` и `BackendSettingsWorkspace` (мастер настроек). Смена разметки затронет оба. Это приемлемо — оба должны говорить на новом языке. Проверить, что `BackendSettingsWorkspace` не полагается на класс `settings-form-grid` для лейаута (он оборачивает секцию сам).

- [ ] **Step 1: Добавить i18n-ключ секции профиля**

В `packages/i18n/src/messages.ts` добавить в ru/en/tg (рядом с существующими `op.settings.profile.*`):

```ts
// ru
"op.club.profileSection": "Профиль клуба",
// en
"op.club.profileSection": "Club profile",
// tg
"op.club.profileSection": "Профили клуб",
```

- [ ] **Step 2: Переписать `SettingsProfileSection` на mgmt-form**

Заменить `return (...)` на:

```tsx
return (
  <div className="mgmt-form">
    <div className="mgmt-section-title"><span>{t('op.club.profileSection')}</span></div>
    <div className="mgmt-form-grid">
      <label>{t('op.settings.profile.clubName')}
        <input value={clubName} onChange={(event) => onClubNameChange(event.currentTarget.value)} />
      </label>
      <label>{t('op.settings.profile.city')}
        <input value={city} onChange={(event) => onCityChange(event.currentTarget.value)} />
      </label>
    </div>
    <div className="mgmt-meta-grid">
      <div className="mgmt-meta-row">
        <span className="mgmt-meta-label">{t('op.settings.profile.currency')}</span>
        <span className="mgmt-meta-value">{currencyCode}</span>
      </div>
      <div className="mgmt-meta-row">
        <span className="mgmt-meta-label">{t('op.settings.profile.branch')}</span>
        <span className="mgmt-meta-value">{hasBackend ? t('op.settings.profile.branchCurrent') : t('op.settings.profile.branchLocal')}</span>
      </div>
    </div>
  </div>
);
```

Импорт `useI18n` уже есть. Пропсы не меняются (сигнатура функции остаётся; `settingsDirty`/`onSave` — как есть).

- [ ] **Step 3: Добавить CSS мета-строк**

В конец `src/AFK4.Operator.App.Web/src/styles/23-management-crud.css`:

```css
/* Справочные (read-only) пары «метка → значение» в mgmt-form: не disabled-инпуты (читаются как
   сломанные), а тихие строки под редактируемыми полями. */
.mgmt-meta-grid {
  display: grid;
  gap: 8px;
  padding-top: 4px;
  border-top: 1px solid var(--border-subtle, var(--border-default));
}
.mgmt-meta-row {
  display: flex;
  align-items: baseline;
  justify-content: space-between;
  gap: 12px;
}
.mgmt-meta-label {
  color: var(--text-secondary);
  font-size: 12px;
  font-weight: 600;
}
.mgmt-meta-value {
  color: var(--text-primary);
  font-size: 13px;
  font-weight: 500;
}
```

- [ ] **Step 4: Написать тест `ClubDestination.test.tsx`**

```tsx
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../operatorToast';

const getBranchProfile = mock(async () => ({ name: 'AFK4 Центр', city: 'Душанбе' }));
const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../../operatorHelpers', () => ({
  ...actual,
  createAuthenticatedOperatorClients: () => ({
    settings: { getBranchProfile, updateBranchProfile: mock(async (_b: string, r: unknown) => r) }
  })
}));

const { ClubDestination } = await import('./ClubDestination');
const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o1' }, branchId: 'b1' } as never;

afterEach(() => { getBranchProfile.mockClear(); cleanup(); });

describe('ClubDestination', () => {
  it('renders club profile in a mgmt-form with read-only meta rows', async () => {
    const { container } = render(
      <I18nProvider initialLocale="ru"><ToastProvider>
        <ClubDestination backend={backend} session={{ permissions: [], organizationId: 'o1' } as never} currencyCode="TJS" />
      </ToastProvider></I18nProvider>
    );
    expect(await screen.findByDisplayValue('AFK4 Центр')).toBeInTheDocument();
    // Валюта/филиал — не инпуты, а мета-значения
    expect(container.querySelector('.mgmt-meta-value')).not.toBeNull();
    expect(screen.getByText('TJS')).toBeInTheDocument();
    expect(container.querySelector('.mgmt-form')).not.toBeNull();
    expect(container.querySelector('.settings-form-grid')).toBeNull();
  });
});
```

- [ ] **Step 5: Прогнать тесты (ожидаем PASS)**

Run: `cd src/AFK4.Operator.App.Web && bun test src/management/destinations/ClubDestination.test.tsx`
Expected: PASS. Затем прогнать соседей, которые монтируют `SettingsProfileSection`:
Run: `bun test src/settings src/BackendSettingsWorkspace.test.tsx 2>/dev/null || true`
Expected: PASS (или отсутствие теста — тогда пропустить).

- [ ] **Step 6: Проверить сборку**

Run: `cd src/AFK4.Operator.App.Web && bun run build`
Expected: успешно (0 ошибок TS).

- [ ] **Step 7: Коммит**

```bash
git add src/AFK4.Operator.App.Web/src/settings/SettingsProfileSection.tsx \
        src/AFK4.Operator.App.Web/src/management/destinations/ClubDestination.test.tsx \
        src/AFK4.Operator.App.Web/src/styles/23-management-crud.css \
        packages/i18n/src/messages.ts
git commit -m "feat(operator): rework Клуб profile to mgmt-form with read-only meta rows"
```

---

## Task 2: Лояльность — карточки правил, живой пример, «Ограничения начисления»

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/management/destinations/payments/LoyaltyTab.tsx`
- Modify: `packages/i18n/src/messages.ts` (4 новых ключа ×3)
- Modify: `src/AFK4.Operator.App.Web/src/styles/15-settings.css` (карточки правил + пример)
- Test: `src/AFK4.Operator.App.Web/src/management/destinations/PaymentsLoyaltyDestination.test.tsx` (дополнить)

**Interfaces:**
- Consumes: `LoyaltySettingsController` (поля `topUpEnabled`/`topUpPercent`/`setTopUpPercent` и аналоги для shop/session, `cashbackCap`, `minimumSource`, `disabled`, `ready`, `loadError`, `retry`), `currencyCode`, `hasBackend`. Контроллер НЕ меняется.
- Produces: разметка карточек правил; тесты `PaymentsLoyaltyDestination` продолжают находить `getByLabelText(/кэшбэк с пополнений/i)` и `/процент с пополнений/i` — **сохранить эти aria-label/label**.

- [ ] **Step 1: Добавить i18n-ключи**

В `packages/i18n/src/messages.ts` (ru/en/tg):

```ts
// ru
"op.loyalty.rules.hint": "Бонусы начисляются автоматически по включённым правилам.",
"op.loyalty.limits.hint": "Применяются ко всем правилам выше.",
"op.loyalty.example.prefix": "Например:",
"op.loyalty.example.base": "Со 100",
// en
"op.loyalty.rules.hint": "Bonuses are credited automatically per the enabled rules.",
"op.loyalty.limits.hint": "Apply to all rules above.",
"op.loyalty.example.prefix": "For example:",
"op.loyalty.example.base": "From 100",
// tg
"op.loyalty.rules.hint": "Бонусҳо аз рӯи қоидаҳои фаъол ба таври худкор ҳисоб карда мешаванд.",
"op.loyalty.limits.hint": "Ба ҳамаи қоидаҳои боло татбиқ мешаванд.",
"op.loyalty.example.prefix": "Масалан:",
"op.loyalty.example.base": "Аз 100",
```

> `op.loyalty.example.base` — текстовый префикс «Со 100», а бонус считаем и рендерим через `<Money signed>` (реальные единицы валюты, без ICU). База — 100 major = `100 * 100` minor units (2 знака), т.е. `10000` minor.

- [ ] **Step 2: Переписать `LoyaltyTab` — карточки правил + пример + ограничения**

Импортировать `Money`: `import { EmptyState, Money } from '../../../operatorPrimitives';`

Ввести локальный хелпер-компонент карточки правила ПРЯМО в файле (одна ответственность, DRY по трём правилам):

```tsx
const RULE_BASE_MINOR = 10000; // 100.00 в minor units — база живого примера

function RuleCard({
  enabled, onToggle, name, hint, percent, onPercent, percentAria, disabled, currencyCode, t
}: {
  enabled: boolean; onToggle: (v: boolean) => void;
  name: string; hint: string;
  percent: string; onPercent: (v: string) => void; percentAria: string;
  disabled: boolean; currencyCode: string; t: (k: string) => string;
}) {
  const pct = Number(percent);
  const bonusMinor = Number.isFinite(pct) && pct > 0 ? Math.round((RULE_BASE_MINOR * pct) / 100) : 0;
  return (
    <div className={`loyalty-rule-card${enabled ? ' is-on' : ''}`}>
      <label className="mgmt-check loyalty-rule-toggle">
        <input type="checkbox" checked={enabled} disabled={disabled} onChange={(e) => onToggle(e.currentTarget.checked)} />
        <span className="loyalty-rule-text">
          <span className="loyalty-rule-name">{name}</span>
          <span className="loyalty-rule-hint">{hint}</span>
        </span>
      </label>
      <label className="loyalty-rule-percent">
        <span>{t('op.loyalty.percentShort')}</span>
        <input type="number" min="0" max="100" aria-label={percentAria}
          value={percent} disabled={disabled || !enabled}
          onChange={(e) => onPercent(e.currentTarget.value)} />
      </label>
      {enabled && bonusMinor > 0 && (
        <p className="loyalty-rule-example">
          <span>{t('op.loyalty.example.prefix')} {t('op.loyalty.example.base')}</span>
          {' → '}
          <Money minorUnits={bonusMinor} currencyCode={currencyCode} signed />
        </p>
      )}
    </div>
  );
}
```

Заменить блок `<div className="loyalty-rules">…</div>` на три `RuleCard` (пропсы из контроллера `c`), сохранив прежние label-строки:

```tsx
<div className="loyalty-rules">
  <div className="mgmt-section-title"><span>{t('op.loyalty.rules.title')}</span></div>
  <p className="loyalty-section-hint">{t('op.loyalty.rules.hint')}</p>
  <RuleCard t={t} currencyCode={currencyCode} disabled={c.disabled}
    enabled={c.topUpEnabled} onToggle={c.setTopUpEnabled}
    name={t('op.loyalty.topUpEnabled')} hint={t('op.loyalty.topUpHint')}
    percent={c.topUpPercent} onPercent={c.setTopUpPercent} percentAria={t('op.loyalty.topUpPercent')} />
  <RuleCard t={t} currencyCode={currencyCode} disabled={c.disabled}
    enabled={c.shopEnabled} onToggle={c.setShopEnabled}
    name={t('op.loyalty.shopEnabled')} hint={t('op.loyalty.shopHint')}
    percent={c.shopPercent} onPercent={c.setShopPercent} percentAria={t('op.loyalty.shopPercent')} />
  <RuleCard t={t} currencyCode={currencyCode} disabled={c.disabled}
    enabled={c.sessionEnabled} onToggle={c.setSessionEnabled}
    name={t('op.loyalty.sessionEnabled')} hint={t('op.loyalty.sessionHint')}
    percent={c.sessionPercent} onPercent={c.setSessionPercent} percentAria={t('op.loyalty.sessionPercent')} />
</div>
```

В блоке лимитов добавить hint под заголовком:

```tsx
<div className="loyalty-limits">
  <div className="mgmt-section-title"><span>{t('op.loyalty.limits.title')}</span></div>
  <p className="loyalty-section-hint">{t('op.loyalty.limits.hint')}</p>
  <div className="mgmt-form-grid">
    {/* поля cap / minimum — как есть */}
  </div>
</div>
```

Loading/error-ветки (skeleton, `loadError`) — без изменений.

- [ ] **Step 3: CSS карточек правил**

Заменить блок `.loyalty-rule*` в `src/AFK4.Operator.App.Web/src/styles/15-settings.css` (строки ~791-840) на карточный вариант (старый `.loyalty-rule` → `.loyalty-rule-card`):

```css
.loyalty-section-hint {
  margin: -4px 0 4px;
  color: var(--text-secondary);
  font-size: 12px;
}
.loyalty-rule-card {
  display: grid;
  grid-template-columns: 1fr auto;
  align-items: start;
  gap: 12px 16px;
  padding: 14px 16px;
  border: 1px solid var(--border-default);
  border-radius: var(--radius-md);
  background: var(--surface-default);
}
.loyalty-rule-card.is-on {
  border-color: var(--border-accent);
  background: var(--surface-accent-soft);
}
.loyalty-rule-card + .loyalty-rule-card { margin-top: 10px; }
.loyalty-rule-toggle.mgmt-check { align-items: flex-start; padding: 0; }
.loyalty-rule-text { display: flex; flex-direction: column; gap: 2px; }
.loyalty-rule-name { color: var(--text-primary); font-size: 14px; font-weight: 600; }
.loyalty-rule-hint { color: var(--text-secondary); font-size: 12px; font-weight: 400; }
.loyalty-rule-percent {
  display: flex; align-items: center; gap: 8px;
  color: var(--text-secondary); font-size: 12px; font-weight: 600;
}
.loyalty-rule-percent input { width: 88px; text-align: right; }
.loyalty-rule-example {
  grid-column: 1 / -1; margin: 0;
  color: var(--text-secondary); font-size: 12px;
}
```

Проверить, что `.loyalty-form`/`.loyalty-rules`/`.loyalty-limits` (строки 774-790) остаются валидными (селектор `.loyalty-rule` больше не используется — удалить его правила).

- [ ] **Step 4: Дополнить тест — карточка показывает пример**

В `PaymentsLoyaltyDestination.test.tsx` добавить кейс:

```tsx
it('shows a live accrual example when a rule is enabled', async () => {
  view([permissionNames.manageLoyaltySettings]);
  const toggle = await screen.findByLabelText(/кэшбэк с пополнений/i);
  fireEvent.click(toggle);
  fireEvent.change(screen.getByLabelText(/процент с пополнений/i), { target: { value: '10' } });
  // 10% со 100 → +10.00 (Money signed рендерит с «+»)
  expect(await screen.findByText(/\+10/)).toBeInTheDocument();
});
```

- [ ] **Step 5: Прогнать тесты**

Run: `cd src/AFK4.Operator.App.Web && bun test src/management/destinations/PaymentsLoyaltyDestination.test.tsx`
Expected: PASS (все кейсы, включая существующие про save-бар и basis points).

- [ ] **Step 6: i18n-гарды**

Run: `cd /home/fedya/projects/afk4.net && bun test packages/i18n/src/messages.test.ts packages/i18n/src/voice.test.ts`
Expected: PASS (паритет ru/en/tg, tg≠ru на новых ключах).

- [ ] **Step 7: Сборка**

Run: `cd src/AFK4.Operator.App.Web && bun run build`
Expected: успешно.

- [ ] **Step 8: Коммит**

```bash
git add src/AFK4.Operator.App.Web/src/management/destinations/payments/LoyaltyTab.tsx \
        src/AFK4.Operator.App.Web/src/management/destinations/PaymentsLoyaltyDestination.test.tsx \
        src/AFK4.Operator.App.Web/src/styles/15-settings.css \
        packages/i18n/src/messages.ts
git commit -m "feat(operator): rework Лояльность to rule cards with live accrual example"
```

---

## Task 3: Платёжные шлюзы — dcgate на kit, выровнять с Eskhata

**Files:**
- Modify: `src/AFK4.Operator.App.Web/src/PaymentGatewaysWorkspace.tsx` (только render + confirm)
- Modify: `src/AFK4.Operator.App.Web/src/management/destinations/payments/GatewaysTab.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/management/destinations/payments/EskhataGatewayForm.tsx` (обёртка секции)
- Modify: `packages/i18n/src/messages.ts` (новые ключи ×3)
- Modify: `src/AFK4.Operator.App.Web/src/styles/13-payments.css` и/или `15-settings.css` (карты-строки, убрать divider)
- Test: `src/AFK4.Operator.App.Web/src/PaymentGatewaysWorkspace.test.tsx` (обновить под новую разметку)

**Interfaces:**
- Consumes: `PaymentGatewaysWorkspace` весь стейт/хендлеры (`provision`, `disable`, `startAttach`, `verifyCode`, `verifyPassword`, `reload`, стейт `gateways`/`statuses`/`loading`/`busy`/`cardNumber`/`scopeBranch`/attach-поля) — **не трогаем**. Примитивы `EmptyState`, `CriticalActionConfirmation`.
- Produces: `GatewaysTab` рендерит две секции без `<hr>`.

- [ ] **Step 1: i18n-ключи**

В `packages/i18n/src/messages.ts` (ru/en/tg). Часть строк уже есть в namespace `payments_cards.*` — переиспользуем; добавляем только новые:

```ts
// ru
"op.payments.section.cards": "Приём по картам",
"op.payments.cards.emptyHint": "Подключите карту, чтобы принимать оплату переводом.",
"op.payments.cards.disableTitle": "Отключить карту?",
"op.payments.cards.disableImpact": "Оплата на эту карту перестанет приниматься.",
// en
"op.payments.section.cards": "Card acceptance",
"op.payments.cards.emptyHint": "Add a card to accept payments by transfer.",
"op.payments.cards.disableTitle": "Disable card?",
"op.payments.cards.disableImpact": "Payments to this card will stop being accepted.",
// tg
"op.payments.section.cards": "Қабули пардохт бо корт",
"op.payments.cards.emptyHint": "Барои қабули пардохт бо гузаронидан як корт пайваст кунед.",
"op.payments.cards.disableTitle": "Кортро ғайрифаъол кардан?",
"op.payments.cards.disableImpact": "Пардохт ба ин корт қабул карда намешавад.",
```

- [ ] **Step 2: `PaymentGatewaysWorkspace` — заменить `window.confirm` на CriticalActionConfirmation**

Импорт: `import { CriticalActionConfirmation, EmptyState } from './operatorPrimitives';`
Добавить стейт: `const [disableTarget, setDisableTarget] = useState<string | null>(null);`
Заменить тело `disable` — убрать `window.confirm`, вынести подтверждение в UI:

```tsx
const disable = async (id: string) => {
  setBusy(true);
  try {
    await clients.disable(id);
    await reload();
  } catch (error) {
    setLoadError(projectOperatorError(error, t).detail);
  } finally {
    setBusy(false);
  }
};
```

Кнопка «Отключить» теперь ставит цель: `onClick={() => setDisableTarget(g.branchPaymentGatewayId)}`.

- [ ] **Step 3: `PaymentGatewaysWorkspace` — переписать render на kit-классы**

Заменить `return (...)`:

```tsx
return (
  <div className="mgmt-form payment-gateways-cards">
    <div className="mgmt-section-title"><span>{t('op.payments.section.cards')}</span></div>
    {loadError && <p className="ui-inline-error" role="alert">{loadError}</p>}

    <div className="mgmt-form-grid">
      <label>{t('payments_cards.card_number')}
        <input value={cardNumber} onChange={(e) => setCardNumber(e.currentTarget.value)} inputMode="numeric" />
      </label>
      <label className="mgmt-check">
        <input type="checkbox" checked={scopeBranch} onChange={(e) => setScopeBranch(e.currentTarget.checked)} />
        {scopeBranch ? t('payments_cards.scope.branch') : t('payments_cards.scope.org')}
      </label>
    </div>
    <div className="mgmt-form-actions">
      <button type="button" className="ui-btn ui-btn--primary"
        disabled={busy || cardNumber.trim().length < 12} onClick={() => void provision()}>
        {t('payments_cards.provision')}
      </button>
    </div>

    {loading ? (
      <p className="workspace-loading">{t('payments_cards.loading')}</p>
    ) : gateways.length === 0 ? (
      <EmptyState title={t('payments_cards.empty')} description={t('op.payments.cards.emptyHint')} />
    ) : (
      <div className="payment-card-rows">
        {gateways.map((g) => {
          const live = statuses[g.branchPaymentGatewayId];
          const known = live && (live.sessionHealth === 'online' || live.sessionHealth === 'offline' || live.sessionHealth === 'configured');
          const statusTone = g.status === 'disabled' ? 'is-neutral' : g.status === 'pending_telegram' ? 'is-warning' : 'is-live';
          return (
            <article key={g.branchPaymentGatewayId} className="payment-card-row" data-status={g.status}>
              <div className="payment-card-main">
                <span className="payment-card-pan">•••• {g.cardLast4}</span>
                <span className="ui-chip ui-chip--status ui-chip--xs is-neutral">
                  {g.branchId ? t('payments_cards.scope.branch') : t('payments_cards.scope.org')}
                </span>
                <span className={`ui-chip ui-chip--status ui-chip--xs ${statusTone}`}>
                  {t(`payments_cards.status.${g.status}` as MessageKey)}
                </span>
                {known && (
                  <span className={`ui-chip ui-chip--status ui-chip--xs ${live.sessionHealth === 'online' ? 'is-live' : 'is-neutral'}`}>
                    {t(`payments_cards.session.${live.sessionHealth}` as MessageKey)}
                  </span>
                )}
                {g.status !== 'disabled' && (
                  <button type="button" className="ui-btn ui-btn--sm ui-btn--danger payment-card-disable"
                    disabled={busy} onClick={() => setDisableTarget(g.branchPaymentGatewayId)}>
                    {t('payments_cards.disable')}
                  </button>
                )}
              </div>

              {live?.lastMessageAt && (
                <span className="payment-card-session-last">
                  {t('payments_cards.session.last_message')}: {formatDate(live.lastMessageAt)}
                </span>
              )}

              {g.status === 'pending_telegram' && (
                <div className="payment-card-attach mgmt-form">
                  <div className="mgmt-section-title"><span>{t('payments_cards.telegram.title')}</span></div>
                  {(attachId !== g.branchPaymentGatewayId || attachPhase === 'idle') && (
                    <div className="payment-attach-row">
                      <label>{t('payments_cards.telegram.phone')}
                        <input aria-label="phone" value={phone} onChange={(e) => setPhone(e.currentTarget.value)} />
                      </label>
                      <button type="button" className="ui-btn ui-btn--primary"
                        disabled={busy || !phone.trim()} onClick={() => void startAttach(g.branchPaymentGatewayId)}>
                        {t('payments_cards.telegram.start')}
                      </button>
                    </div>
                  )}
                  {attachId === g.branchPaymentGatewayId && attachPhase === 'code_required' && (
                    <div className="payment-attach-row">
                      <label>{t('payments_cards.telegram.code')}
                        <input value={code} onChange={(e) => setCode(e.currentTarget.value)} inputMode="numeric" />
                      </label>
                      <button type="button" className="ui-btn ui-btn--primary" disabled={busy} onClick={() => void verifyCode()}>
                        {t('payments_cards.telegram.code_submit')}
                      </button>
                    </div>
                  )}
                  {attachId === g.branchPaymentGatewayId && attachPhase === 'password_required' && (
                    <div className="payment-attach-row">
                      <label>{t('payments_cards.telegram.password')}
                        <input type="password" value={password} onChange={(e) => setPassword(e.currentTarget.value)} />
                      </label>
                      <button type="button" className="ui-btn ui-btn--primary" disabled={busy} onClick={() => void verifyPassword()}>
                        {t('payments_cards.telegram.password_submit')}
                      </button>
                    </div>
                  )}
                  {attachId === g.branchPaymentGatewayId && attachPhase === 'attached' && (
                    <p className="payment-card-attached">{t('payments_cards.telegram.attached')}</p>
                  )}
                </div>
              )}
            </article>
          );
        })}
      </div>
    )}

    {disableTarget && (
      <CriticalActionConfirmation
        title={t('op.payments.cards.disableTitle')}
        detail={`•••• ${gateways.find((g) => g.branchPaymentGatewayId === disableTarget)?.cardLast4 ?? ''}`}
        impact={t('op.payments.cards.disableImpact')}
        confirmLabel={t('payments_cards.disable')}
        disabled={busy}
        onCancel={() => setDisableTarget(null)}
        onConfirm={() => { const id = disableTarget; setDisableTarget(null); void disable(id); }}
      />
    )}
  </div>
);
```

Оставить импорт `MessageKey` (уже есть). Удалить неиспользуемые старые классы из JSX.

- [ ] **Step 4: `GatewaysTab` — убрать `<hr>`, две секции стопкой**

```tsx
export function GatewaysTab({ backend }: Props) {
  return (
    <div className="management-panel payment-gateways">
      <PaymentGatewaysWorkspace backend={backend} />
      <EskhataGatewayForm backend={backend} />
    </div>
  );
}
```

Удалить импорт/использование `eskhata-divider`.

- [ ] **Step 5: `EskhataGatewayForm` — согласовать статус-чип с общим языком**

В `EskhataGatewayForm.tsx` заменить `<span className="eskhata-status-chip">` на `<span className="ui-chip ui-chip--status ui-chip--xs is-neutral">` (единый чип). Класс-обёртку `.eskhata-gateway` оставить, но убрать верхний отступ divider'а (см. CSS ниже).

- [ ] **Step 6: CSS карт и отступов секций**

В `src/AFK4.Operator.App.Web/src/styles/15-settings.css`: удалить `.eskhata-divider` (строки ~863-868). Добавить/заменить (карты dcgate):

```css
.payment-gateways { display: grid; gap: 24px; }
.payment-gateways .eskhata-gateway { margin-top: 0; }
.payment-card-rows { display: grid; gap: 10px; }
.payment-card-row {
  display: grid; gap: 8px;
  padding: 12px 14px;
  border: 1px solid var(--border-default);
  border-radius: var(--radius-md);
  background: var(--surface-default);
}
.payment-card-main { display: flex; align-items: center; flex-wrap: wrap; gap: 10px; }
.payment-card-pan { font-weight: 600; color: var(--text-primary); }
.payment-card-disable { margin-left: auto; }
.payment-card-session-last { color: var(--text-secondary); font-size: 12px; }
.payment-attach-row { display: flex; align-items: flex-end; gap: 10px; }
.payment-attach-row label { flex: 1; }
.payment-card-attached { color: var(--success-text); font-size: 13px; font-weight: 600; margin: 0; }
.ui-inline-error { color: var(--danger-text); font-size: 13px; margin: 0; }
```

Удалить старые правила `.payment-cards-provision`, `.payment-cards-list`, `.payment-card-scope`, `.payment-card-status`, `.payment-card-session*` (там, где они определены — grep по имени; убрать осиротевшие).

- [ ] **Step 7: Обновить `PaymentGatewaysWorkspace.test.tsx`**

Прочитать текущий тест, обновить селекторы под новую разметку (кнопка «Отключить» теперь открывает `CriticalActionConfirmation` — после клика по «Отключить» кликнуть подтверждение). Ключевые проверки, которые должны сохраниться:
- provision-поле и кнопка «Создать»/provision работают;
- пустой список → `EmptyState` (`payments_cards.empty` текст присутствует);
- disable-flow: клик «Отключить» → появляется `role="alertdialog"` → клик confirmLabel → `clients.disable` вызван.

```tsx
// пример проверки disable-flow
fireEvent.click(screen.getByRole('button', { name: /отключить/i }));
fireEvent.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: /отключить/i }));
await waitFor(() => expect(disableMock).toHaveBeenCalled());
```

- [ ] **Step 8: Прогнать тесты**

Run: `cd src/AFK4.Operator.App.Web && bun test src/PaymentGatewaysWorkspace.test.tsx src/management/destinations/PaymentsLoyaltyDestination.test.tsx`
Expected: PASS.

Run: `cd /home/fedya/projects/afk4.net && bun test packages/i18n/src/messages.test.ts packages/i18n/src/voice.test.ts`
Expected: PASS.

- [ ] **Step 9: Сборка**

Run: `cd src/AFK4.Operator.App.Web && bun run build`
Expected: успешно.

- [ ] **Step 10: Коммит**

```bash
git add src/AFK4.Operator.App.Web/src/PaymentGatewaysWorkspace.tsx \
        src/AFK4.Operator.App.Web/src/PaymentGatewaysWorkspace.test.tsx \
        src/AFK4.Operator.App.Web/src/management/destinations/payments/GatewaysTab.tsx \
        src/AFK4.Operator.App.Web/src/management/destinations/payments/EskhataGatewayForm.tsx \
        src/AFK4.Operator.App.Web/src/styles/15-settings.css \
        packages/i18n/src/messages.ts
git commit -m "feat(operator): migrate dcgate cards to kit, unify with Eskhata section"
```

---

## Task 4: Новости — MgmtTable + MgmtDrawer (create+edit) + подтверждение удаления

**Files:**
- Rewrite: `src/AFK4.Operator.App.Web/src/NewsWorkspace.tsx`
- Modify: `src/AFK4.Operator.App.Web/src/management/destinations/NewsDestination.tsx` (contentWidth="full")
- Modify: `packages/i18n/src/messages.ts` (новые ключи ×3)
- Modify: `src/AFK4.Operator.App.Web/src/styles/23-management-crud.css` (при необходимости — textarea в mgmt-form)
- Test: `src/AFK4.Operator.App.Web/src/NewsWorkspace.test.tsx` (переписать под таблицу/drawer)

**Interfaces:**
- Consumes: `MgmtTable`, `MgmtDrawer`, `CriticalActionConfirmation`, `NewsClient` (list/listBranches/create/update/remove — не меняем), хелперы `toIsoOrNull`/`toLocalInput` (оставить).
- Produces: таблица новостей + drawer формы; тест находит строки по заголовку, «Создать новость» открывает drawer.

- [ ] **Step 1: i18n-ключи**

В `packages/i18n/src/messages.ts` (ru/en/tg):

```ts
// ru
"op.news.col.branch": "Филиал",
"op.news.col.status": "Статус",
"op.news.col.window": "Публикация",
"op.news.statusPublished": "Опубликовано",
"op.news.addCta": "Создать новость",
"op.news.createTitle": "Новая новость",
"op.news.deleteTitle": "Удалить новость?",
"op.news.deleteImpact": "Новость исчезнет у игроков в приложении.",
"op.news.emptyDescription": "Публикуйте турниры, акции и объявления для игроков.",
// en
"op.news.col.branch": "Branch",
"op.news.col.status": "Status",
"op.news.col.window": "Publishing",
"op.news.statusPublished": "Published",
"op.news.addCta": "New post",
"op.news.createTitle": "New post",
"op.news.deleteTitle": "Delete post?",
"op.news.deleteImpact": "The post will disappear for players in the app.",
"op.news.emptyDescription": "Publish tournaments, promos and announcements for players.",
// tg
"op.news.col.branch": "Филиал",
"op.news.col.status": "Ҳолат",
"op.news.col.window": "Нашр",
"op.news.statusPublished": "Нашршуда",
"op.news.addCta": "Хабари нав",
"op.news.createTitle": "Хабари нав",
"op.news.deleteTitle": "Хабарро нест кардан?",
"op.news.deleteImpact": "Хабар барои бозигарон дар барнома нопадид мешавад.",
"op.news.emptyDescription": "Мусобиқаҳо, аксияҳо ва эълонҳоро барои бозигарон нашр кунед.",
```

> `op.news.col.branch` tg = «Филиал» — уже в whitelist (loanword, см. `op.management.dest.club` соседство). Если гард `tg===ru` ругнётся на `op.news.col.branch`/`op.news.col.status` — добавить их в whitelist в `messages.test.ts` ТОЛЬКО если это настоящий loanword; «Филиал» — да, «Статус»→«Ҳолат» уже отличается, не нужен.

- [ ] **Step 2: Переписать `NewsWorkspace` — состояние выбора/drawer**

Сохранить: `EMPTY`, `toIsoOrNull`, `toLocalInput`, `NewsClient`, загрузку `items`/`branches`, `reload`, `save`, `remove`, валидацию. Заменить UI-стейт и render.

Добавить стейт:
```tsx
const [selectedId, setSelectedId] = useState<string | null>(null); // id или '__new__' для создания
const [deleteTarget, setDeleteTarget] = useState<NewsItemDto | null>(null);
const isDrawerOpen = selectedId !== null;
const isCreate = selectedId === '__new__';
```

`edit(item)` → `setForm(...)` (как есть) + `setSelectedId(item.id)`.
Открытие создания: `const openCreate = () => { setForm({ ...EMPTY }); setError(null); setSelectedId('__new__'); };`
После успешного `save()` — `setSelectedId(null)` (закрыть drawer) и `setForm({ ...EMPTY })`.
После `remove` — `setSelectedId(null)`.

Импорты:
```tsx
import { MgmtTable } from './management/kit/MgmtTable';
import { MgmtDrawer } from './management/kit/MgmtDrawer';
import { CriticalActionConfirmation } from './operatorPrimitives';
import { Newspaper } from 'lucide-react';
```
`useI18n` уже даёт `t`; добавить `formatDate`: `const { t, formatDate } = useI18n();`

- [ ] **Step 3: Render — таблица + drawer**

```tsx
if (!ready) return <p className="workspace-loading">{t('state.loading')}</p>;

const branchName = (branchId: string | null) =>
  branchId === null ? t('op.news.allBranches') : (branches.find((b) => b.branchId === branchId)?.name ?? '—');

const windowLabel = (item: NewsItemDto) => {
  const from = item.publishAtUtc ? formatDate(item.publishAtUtc) : '';
  const to = item.expiresAtUtc ? formatDate(item.expiresAtUtc) : '';
  if (!from && !to) return '—';
  return `${from || '…'} — ${to || '…'}`;
};

return (
  <div className="mgmt-master-detail">
    <MgmtTable<NewsItemDto>
      columns={[
        { key: 'title', header: t('op.news.fieldTitle'), render: (n) => n.title },
        { key: 'branch', header: t('op.news.col.branch'), render: (n) => branchName(n.branchId) },
        {
          key: 'status', header: t('op.news.col.status'),
          render: (n) => (
            <span className={`ui-chip ui-chip--status ui-chip--xs ${n.isPublished ? 'is-live' : 'is-neutral'}`}>
              {n.isPublished ? t('op.news.statusPublished') : t('op.news.draftTag')}
            </span>
          )
        },
        { key: 'window', header: t('op.news.col.window'), render: (n) => windowLabel(n) }
      ]}
      rows={items}
      rowKey={(n) => n.id}
      gridTemplate="1.6fr 1fr 0.8fr 1.2fr"
      selectedKey={isCreate ? null : selectedId}
      onSelectRow={(n) => edit(n)}
      toolbar={{
        title: t('op.management.dest.news'),
        primary: { label: t('op.news.addCta'), onClick: openCreate }
      }}
      empty={{
        icon: <Newspaper size={22} aria-hidden="true" />,
        title: t('op.news.empty'),
        description: t('op.news.emptyDescription'),
        action: { label: t('op.news.addCta'), onClick: openCreate }
      }}
    />

    {isDrawerOpen && (
      <MgmtDrawer
        title={isCreate ? t('op.news.createTitle') : form.title || t('op.news.createTitle')}
        subtitle={isCreate ? undefined : (form.isPublished ? t('op.news.statusPublished') : t('op.news.draftTag'))}
        onClose={() => { setSelectedId(null); setForm({ ...EMPTY }); setError(null); }}
        footer={
          <div className="mgmt-form-actions">
            {!isCreate && (
              <button type="button" className="ui-btn ui-btn--danger"
                onClick={() => setDeleteTarget(items.find((n) => n.id === form.id) ?? null)}>
                {t('op.news.delete')}
              </button>
            )}
            <button type="button" className="ui-btn ui-btn--primary" onClick={() => void save()}>
              {t('op.news.save')}
            </button>
          </div>
        }
      >
        <form className="mgmt-form" onSubmit={(e) => { e.preventDefault(); void save(); }}>
          <label>{t('op.news.fieldTitle')}
            <input value={form.title} onChange={(e) => setForm({ ...form, title: e.target.value })} />
          </label>
          <label>{t('op.news.fieldBranch')}
            <select value={form.branchId} onChange={(e) => setForm({ ...form, branchId: e.target.value })}>
              <option value="">{t('op.news.allBranches')}</option>
              {branches.map((b) => <option key={b.branchId} value={b.branchId}>{b.name}</option>)}
            </select>
          </label>
          <label>{t('op.news.fieldBody')}
            <textarea value={form.body} onChange={(e) => setForm({ ...form, body: e.target.value })} rows={5} />
          </label>
          <label>{t('op.news.fieldImage')}
            <input value={form.imageUrl} onChange={(e) => setForm({ ...form, imageUrl: e.target.value })} />
          </label>
          <label className="mgmt-check">
            <input type="checkbox" checked={form.isPublished} onChange={(e) => setForm({ ...form, isPublished: e.target.checked })} />
            {t('op.news.published')}
          </label>
          <label>{t('op.news.publishAt')}
            <input type="datetime-local" value={form.publishAt} onChange={(e) => setForm({ ...form, publishAt: e.target.value })} />
          </label>
          <label>{t('op.news.expiresAt')}
            <input type="datetime-local" value={form.expiresAt} onChange={(e) => setForm({ ...form, expiresAt: e.target.value })} />
          </label>
          {error && <p className="ui-inline-error" role="alert">{error}</p>}
        </form>
      </MgmtDrawer>
    )}

    {deleteTarget && (
      <CriticalActionConfirmation
        title={t('op.news.deleteTitle')}
        detail={deleteTarget.title}
        impact={t('op.news.deleteImpact')}
        confirmLabel={t('op.news.delete')}
        onCancel={() => setDeleteTarget(null)}
        onConfirm={() => { const id = deleteTarget.id; setDeleteTarget(null); void remove(id); }}
      />
    )}
  </div>
);
```

> `save()` уже делает `setForm({ ...EMPTY })` + `reload()`; добавить в его конец `setSelectedId(null)`. `remove(id)` — оставить как есть (reload внутри); закрытие drawer делает onConfirm через `setSelectedId` не нужно, но добавить `setSelectedId(null)` в `remove` безопасно.

- [ ] **Step 4: `NewsDestination` — contentWidth full**

```tsx
return (
  <ManagementScreen title={t('op.management.dest.news')} subtitle={t('op.management.dest.news.subtitle')} contentWidth="full">
    <NewsWorkspace backend={backend} />
  </ManagementScreen>
);
```
Убрать обёртку `<div className="management-panel">` (таблица сама панель), как в Товарах.

- [ ] **Step 5: CSS — textarea в mgmt-form**

Добавить в `23-management-crud.css` (если ещё нет правила для textarea):

```css
.mgmt-form textarea {
  min-height: 96px;
  border: 1px solid var(--border-default);
  border-radius: var(--radius-md);
  padding: 10px 12px;
  background: var(--surface-sunken);
  color: var(--text-primary);
  font: inherit;
  font-size: 13px;
  resize: vertical;
}
.mgmt-form textarea:focus-visible { outline: none; box-shadow: var(--focus-ring); }
```

- [ ] **Step 6: Переписать `NewsWorkspace.test.tsx` под таблицу/drawer**

Клиент-мок оставить как есть. Обновить кейсы:

```tsx
it('creates a news item via the drawer', async () => {
  const c = client();
  renderWorkspace(c);
  await waitFor(() => screen.getByRole('button', { name: /создать новость|хабари нав/i }));
  fireEvent.click(screen.getByRole('button', { name: /создать новость/i }));
  fireEvent.change(screen.getByLabelText(/заголовок/i), { target: { value: 'Турнир' } });
  fireEvent.change(screen.getByLabelText(/текст/i), { target: { value: 'В субботу' } });
  fireEvent.click(screen.getByRole('button', { name: /сохранить/i }));
  await waitFor(() => expect(c.created).toHaveLength(1));
  expect(c.created[0].title).toBe('Турнир');
});

it('rejects an empty title in the drawer', async () => {
  const c = client();
  renderWorkspace(c);
  fireEvent.click(await screen.findByRole('button', { name: /создать новость/i }));
  fireEvent.click(screen.getByRole('button', { name: /сохранить/i }));
  await waitFor(() => screen.getByText(/заголовок и текст обязательны/i));
  expect(c.created).toHaveLength(0);
});

it('lists items and deletes one via confirmation', async () => {
  const c = client([{
    id: 'x1', branchId: null, title: 'Старая', body: 'B', imageUrl: null,
    isPublished: true, publishAtUtc: null, expiresAtUtc: null,
    createdAtUtc: '2026-06-01T00:00:00Z', updatedAtUtc: '2026-06-01T00:00:00Z'
  }]);
  renderWorkspace(c);
  fireEvent.click(await screen.findByText('Старая'));            // строка → drawer
  fireEvent.click(screen.getByRole('button', { name: /удалить/i })); // футер drawer
  fireEvent.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: /удалить/i }));
  await waitFor(() => expect(c.removed).toEqual(['x1']));
});
```
Добавить импорт `within` из `@testing-library/react`.

- [ ] **Step 7: Прогнать тесты**

Run: `cd src/AFK4.Operator.App.Web && bun test src/NewsWorkspace.test.tsx`
Expected: PASS.

Run: `cd /home/fedya/projects/afk4.net && bun test packages/i18n/src/messages.test.ts packages/i18n/src/voice.test.ts src/AFK4.Operator.App.Web/src/i18nKeysExist.test.ts`
Expected: PASS.

- [ ] **Step 8: Сборка**

Run: `cd src/AFK4.Operator.App.Web && bun run build`
Expected: успешно.

- [ ] **Step 9: Коммит**

```bash
git add src/AFK4.Operator.App.Web/src/NewsWorkspace.tsx \
        src/AFK4.Operator.App.Web/src/NewsWorkspace.test.tsx \
        src/AFK4.Operator.App.Web/src/management/destinations/NewsDestination.tsx \
        src/AFK4.Operator.App.Web/src/styles/23-management-crud.css \
        packages/i18n/src/messages.ts
git commit -m "feat(operator): rebuild Новости as MgmtTable + drawer with delete confirmation"
```

---

## Task 5: Финальный прогон + живое превью

- [ ] **Step 1: Полный тест-прогон оператора**

Run: `cd src/AFK4.Operator.App.Web && bun test`
Expected: всё зелёное (в т.ч. App.test отдельным прогоном, если он изолирован — см. env-quirks).

- [ ] **Step 2: Полная сборка**

Run: `cd src/AFK4.Operator.App.Web && bun run build`
Expected: успешно.

- [ ] **Step 3: Живое превью для приёмки**

Запустить оператор-превью (скилл `operator-wpf-preview` или `bun run dev`), проверить визуально все четыре экрана в тёмной и светлой теме: Клуб, вкладки Платежи/Лояльность, Новости. Отдать пользователю URL/окно.

---

## Self-Review

**Spec coverage:**
- Платёжные шлюзы (dcgate на kit, EmptyState, CriticalActionConfirmation, статус-чипы, убрать hr) → Task 3 ✅
- Eskhata выровнен в общий ряд секций → Task 3 Step 5 ✅
- Лояльность: карточки правил, живой пример, «Ограничения» с hint → Task 2 ✅
- Клуб: mgmt-form + read-only мета-строки → Task 1 ✅
- Новости: MgmtTable + MgmtDrawer (create+edit) + delete confirm → Task 4 ✅
- i18n ru/en/tg честно, паритет, tg≠ru → ключи заданы во всех задачах + гарды в Step'ах ✅
- Только разметка/классы, логика 1:1 → Global Constraints + каждая задача ✅
- Вне скоупа (Eskhata Merchant API, новые эндпоинты) → не затронуто ✅

**Placeholder scan:** плейсхолдеров нет; все ключи, классы, код — конкретные.

**Type consistency:** `NewsItemDto`/`NewsItemInput` из `operatorApiClients`; `LoyaltySettingsController` поля совпадают с `useLoyaltySettings`; `MgmtColumn`/`RowAction` из kit/types; `CriticalActionConfirmation`/`EmptyState`/`Money` сигнатуры из `operatorPrimitives`. `selectedId === '__new__'` sentinel — консистентен между Step 2/3 Task 4.

**Риск-заметки для исполнителя:**
- `SettingsProfileSection` используется и в `BackendSettingsWorkspace` — проверить оба (Task 1 Step 5).
- `bun run build` тайпчекает тест-файлы — все bun-моки типизировать (`as never`/явные типы).
- Если `voice.test`/`tg===ru` ругнётся на новый ключ — значение либо реально перевести, либо (только для настоящих loanwords) внести в whitelist `messages.test.ts` с комментарием-обоснованием; НЕ копировать ru ради зелёного.
