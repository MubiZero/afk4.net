import { useI18n } from '@afk4/i18n';
import { EmptyState, Money } from '../../../operatorPrimitives';
import type { LoyaltySettingsController } from './useLoyaltySettings';

interface Props {
  controller: LoyaltySettingsController;
  currencyCode: string;
  hasBackend: boolean;
}

const RULE_BASE_MINOR = 10000; // 100.00 в minor units — база живого примера

type TFn = ReturnType<typeof useI18n>['t'];

interface RuleCardProps {
  enabled: boolean; onToggle: (v: boolean) => void;
  name: string; hint: string;
  percent: string; onPercent: (v: string) => void; percentAria: string;
  disabled: boolean;
  currencyCode: string;
  t: TFn;
}

// Карточка правила на уровне модуля (не внутри LoyaltySection): нужна стабильная функция-компонент,
// иначе React пересоздаёт identity на каждый рендер родителя и ремонтирует чекбокс+инпут процента —
// оператор теряет фокус клавиатуры после первой введённой цифры.
function RuleCard({
  enabled, onToggle, name, hint, percent, onPercent, percentAria, disabled, currencyCode, t
}: RuleCardProps) {
  const pct = Number(percent);
  const bonusMinor = Number.isFinite(pct) && pct > 0 ? Math.round((RULE_BASE_MINOR * pct) / 100) : 0;
  return (
    <div className={`payset-rule${enabled ? ' is-on' : ''}`}>
      <div className="payset-rule-top">
        <label className="payset-switch">
          <input
            type="checkbox"
            aria-label={name}
            checked={enabled}
            disabled={disabled}
            onChange={(event) => onToggle(event.currentTarget.checked)}
          />
          <span className="payset-track" />
          <span className="payset-knob" />
        </label>
        <div className="payset-rule-text">
          <div className="payset-rule-name">{name}</div>
          <div className="payset-rule-hint">{hint}</div>
        </div>
        <label className="payset-pct">
          <span>{t('op.loyalty.percentShort')}</span>
          <input
            type="number"
            min="0"
            max="100"
            aria-label={percentAria}
            value={percent}
            disabled={disabled || !enabled}
            onChange={(event) => onPercent(event.currentTarget.value)}
          />
        </label>
      </div>
      {enabled && bonusMinor > 0 && (
        <p className="payset-rule-example">
          {t('op.loyalty.example.prefix')} {t('op.loyalty.example.base')}
          {' → '}
          <Money minorUnits={bonusMinor} currencyCode={currencyCode} signed />
        </p>
      )}
    </div>
  );
}

// Содержимое секции «Как вы возвращаете»: правила кэшбэка + лимиты со своей кнопкой сохранения.
// Обёртку-секцию с заголовком/лидом даёт PaymentsSetupSection. Loading/error рисуются внутри
// секции, чтобы не подменять весь экран (у соседней зоны приёма своя жизнь).
export function LoyaltySection({ controller: c, currencyCode, hasBackend }: Props) {
  const { t } = useI18n();

  if (c.loadError) {
    return (
      <EmptyState
        title={t('op.management.state.errorTitle')}
        description={c.loadError}
        action={{ label: t('op.management.state.retry'), onClick: c.retry }}
      />
    );
  }

  if (hasBackend && !c.ready) {
    return (
      <div className="payset-loading" data-testid="loyalty-skeleton" aria-hidden="true">
        <div className="management-skeleton-line" />
        <div className="management-skeleton-line" />
        <div className="management-skeleton-line" />
      </div>
    );
  }

  return (
    <>
      <div className="payset-subhead">{t('op.loyalty.rules.title')}</div>
      <div className="payset-rules">
        <RuleCard
          disabled={c.disabled}
          enabled={c.topUpEnabled}
          onToggle={c.setTopUpEnabled}
          name={t('op.loyalty.topUpEnabled')}
          hint={t('op.loyalty.topUpHint')}
          percent={c.topUpPercent}
          onPercent={c.setTopUpPercent}
          percentAria={t('op.loyalty.topUpPercent')}
          currencyCode={currencyCode}
          t={t}
        />
        <RuleCard
          disabled={c.disabled}
          enabled={c.shopEnabled}
          onToggle={c.setShopEnabled}
          name={t('op.loyalty.shopEnabled')}
          hint={t('op.loyalty.shopHint')}
          percent={c.shopPercent}
          onPercent={c.setShopPercent}
          percentAria={t('op.loyalty.shopPercent')}
          currencyCode={currencyCode}
          t={t}
        />
        <RuleCard
          disabled={c.disabled}
          enabled={c.sessionEnabled}
          onToggle={c.setSessionEnabled}
          name={t('op.loyalty.sessionEnabled')}
          hint={t('op.loyalty.sessionHint')}
          percent={c.sessionPercent}
          onPercent={c.setSessionPercent}
          percentAria={t('op.loyalty.sessionPercent')}
          currencyCode={currencyCode}
          t={t}
        />
      </div>

      <div className="payset-divider" />

      <div className="payset-subhead">{t('op.loyalty.limits.title')}</div>
      <p className="payset-field-hint" style={{ marginTop: -6, marginBottom: 14 }}>{t('op.loyalty.limits.hint')}</p>
      <div className="payset-limits">
        <div className="payset-field">
          <label htmlFor="loyalty-cap">{`${t('op.loyalty.cap')}, ${currencyCode}`}</label>
          <div className="payset-field-input">
            <input
              id="loyalty-cap"
              inputMode="decimal"
              value={c.cashbackCap}
              disabled={c.disabled}
              onChange={(event) => c.setCashbackCap(event.currentTarget.value)}
            />
          </div>
          <p className="payset-field-hint">{t('op.loyalty.capHint')}</p>
        </div>
        <div className="payset-field">
          <label htmlFor="loyalty-min">{`${t('op.loyalty.minimum')}, ${currencyCode}`}</label>
          <div className="payset-field-input">
            <input
              id="loyalty-min"
              inputMode="decimal"
              value={c.minimumSource}
              disabled={c.disabled}
              onChange={(event) => c.setMinimumSource(event.currentTarget.value)}
            />
          </div>
          <p className="payset-field-hint">{t('op.loyalty.minimumHint')}</p>
        </div>
      </div>

      <div className="payset-foot">
        <button
          type="button"
          className="ui-btn ui-btn--primary"
          disabled={c.disabled || !c.dirty}
          onClick={() => void c.save()}
        >
          {t('op.loyalty.save')}
        </button>
      </div>
    </>
  );
}
