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

// Карточка правила на уровне модуля (не внутри LoyaltyTab): нужна стабильная функция-компонент,
// иначе React пересоздаёт identity на каждый рендер родителя и ремонтирует чекбокс+инпут процента —
// оператор теряет фокус клавиатуры после первой введённой цифры.
function RuleCard({
  enabled, onToggle, name, hint, percent, onPercent, percentAria, disabled, currencyCode, t
}: RuleCardProps) {
  const pct = Number(percent);
  const bonusMinor = Number.isFinite(pct) && pct > 0 ? Math.round((RULE_BASE_MINOR * pct) / 100) : 0;
  return (
    <div className={`loyalty-rule-card${enabled ? ' is-on' : ''}`}>
      <label className="mgmt-check loyalty-rule-toggle">
        <input
          type="checkbox"
          checked={enabled}
          disabled={disabled}
          onChange={(event) => onToggle(event.currentTarget.checked)}
        />
        <span className="loyalty-rule-text">
          <span className="loyalty-rule-name">{name}</span>
          <span className="loyalty-rule-hint">{hint}</span>
        </span>
      </label>
      <label className="loyalty-rule-percent">
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

// Компактная форма правил кэшбэка (без панели «Как это работает»): три правила стопкой + блок
// лимитов. Save-бар живёт в контейнере (ManagementScreen), поэтому здесь только поля. Loading/error
// рисуются внутри вкладки, чтобы не подменять весь экран (у соседней вкладки шлюзов своя жизнь).
export function LoyaltyTab({ controller: c, currencyCode, hasBackend }: Props) {
  const { t } = useI18n();

  if (c.loadError) {
    return (
      <div className="management-error-state">
        <EmptyState
          title={t('op.management.state.errorTitle')}
          description={c.loadError}
          action={{ label: t('op.management.state.retry'), onClick: c.retry }}
        />
      </div>
    );
  }

  if (hasBackend && !c.ready) {
    return (
      <div className="management-skeleton" data-testid="loyalty-skeleton" aria-hidden="true">
        <div className="management-skeleton-line" />
        <div className="management-skeleton-line" />
        <div className="management-skeleton-line" />
      </div>
    );
  }

  return (
    <div className="management-panel">
      <div className="mgmt-form loyalty-form">
        <div className="loyalty-rules">
          <div className="mgmt-section-title"><span>{t('op.loyalty.rules.title')}</span></div>
          <p className="loyalty-section-hint">{t('op.loyalty.rules.hint')}</p>
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

        <div className="loyalty-limits">
          <div className="mgmt-section-title"><span>{t('op.loyalty.limits.title')}</span></div>
          <p className="loyalty-section-hint">{t('op.loyalty.limits.hint')}</p>
          <div className="mgmt-form-grid">
            <label>{`${t('op.loyalty.cap')}, ${currencyCode}`}
              <input
                inputMode="decimal"
                value={c.cashbackCap}
                disabled={c.disabled}
                onChange={(event) => c.setCashbackCap(event.currentTarget.value)}
              />
              <span className="settings-field-hint">{t('op.loyalty.capHint')}</span>
            </label>
            <label>{`${t('op.loyalty.minimum')}, ${currencyCode}`}
              <input
                inputMode="decimal"
                value={c.minimumSource}
                disabled={c.disabled}
                onChange={(event) => c.setMinimumSource(event.currentTarget.value)}
              />
              <span className="settings-field-hint">{t('op.loyalty.minimumHint')}</span>
            </label>
          </div>
        </div>
      </div>
    </div>
  );
}
