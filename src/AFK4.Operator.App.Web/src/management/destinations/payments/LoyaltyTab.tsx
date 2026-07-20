import { useI18n } from '@afk4/i18n';
import { EmptyState } from '../../../operatorPrimitives';
import type { LoyaltySettingsController } from './useLoyaltySettings';

interface Props {
  controller: LoyaltySettingsController;
  currencyCode: string;
  hasBackend: boolean;
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
          <div className="loyalty-rule">
            <label className="mgmt-check loyalty-rule-toggle">
              <input
                type="checkbox"
                checked={c.topUpEnabled}
                disabled={c.disabled}
                onChange={(event) => c.setTopUpEnabled(event.currentTarget.checked)}
              />
              <span className="loyalty-rule-text">
                <span className="loyalty-rule-name">{t('op.loyalty.topUpEnabled')}</span>
                <span className="loyalty-rule-hint">{t('op.loyalty.topUpHint')}</span>
              </span>
            </label>
            <label className="loyalty-rule-percent">
              <span>{t('op.loyalty.percentShort')}</span>
              <input
                type="number"
                min="0"
                max="100"
                aria-label={t('op.loyalty.topUpPercent')}
                value={c.topUpPercent}
                disabled={c.disabled || !c.topUpEnabled}
                onChange={(event) => c.setTopUpPercent(event.currentTarget.value)}
              />
            </label>
          </div>

          <div className="loyalty-rule">
            <label className="mgmt-check loyalty-rule-toggle">
              <input
                type="checkbox"
                checked={c.shopEnabled}
                disabled={c.disabled}
                onChange={(event) => c.setShopEnabled(event.currentTarget.checked)}
              />
              <span className="loyalty-rule-text">
                <span className="loyalty-rule-name">{t('op.loyalty.shopEnabled')}</span>
                <span className="loyalty-rule-hint">{t('op.loyalty.shopHint')}</span>
              </span>
            </label>
            <label className="loyalty-rule-percent">
              <span>{t('op.loyalty.percentShort')}</span>
              <input
                type="number"
                min="0"
                max="100"
                aria-label={t('op.loyalty.shopPercent')}
                value={c.shopPercent}
                disabled={c.disabled || !c.shopEnabled}
                onChange={(event) => c.setShopPercent(event.currentTarget.value)}
              />
            </label>
          </div>

          <div className="loyalty-rule">
            <label className="mgmt-check loyalty-rule-toggle">
              <input
                type="checkbox"
                checked={c.sessionEnabled}
                disabled={c.disabled}
                onChange={(event) => c.setSessionEnabled(event.currentTarget.checked)}
              />
              <span className="loyalty-rule-text">
                <span className="loyalty-rule-name">{t('op.loyalty.sessionEnabled')}</span>
                <span className="loyalty-rule-hint">{t('op.loyalty.sessionHint')}</span>
              </span>
            </label>
            <label className="loyalty-rule-percent">
              <span>{t('op.loyalty.percentShort')}</span>
              <input
                type="number"
                min="0"
                max="100"
                aria-label={t('op.loyalty.sessionPercent')}
                value={c.sessionPercent}
                disabled={c.disabled || !c.sessionEnabled}
                onChange={(event) => c.setSessionPercent(event.currentTarget.value)}
              />
            </label>
          </div>
        </div>

        <div className="loyalty-limits">
          <div className="mgmt-section-title"><span>{t('op.loyalty.limits.title')}</span></div>
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
