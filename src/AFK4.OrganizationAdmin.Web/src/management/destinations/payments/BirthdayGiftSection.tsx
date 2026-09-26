import { useI18n } from '@afk4/i18n';
import { LoadFailureState } from '../../../operatorPrimitives';
import { DeferredSkeleton, SkeletonControl } from '../../../LoadingSkeleton';
import { SetupFieldsSkeleton, SetupRuleSkeleton } from '../../kit/SetupSection';
import type { BirthdayGiftSettingsController } from './useBirthdayGiftSettings';

interface Props {
  controller: BirthdayGiftSettingsController;
  currencyCode: string;
  hasBackend: boolean;
}

// Подарок на день рождения: сумма на баланс в сам день рождения гостя. Кому — тем, кто ввёл дату
// в приложении заранее и был в клубе недавно; правила пишем словами прямо здесь.
export function BirthdayGiftSection({ controller: c, currencyCode, hasBackend }: Props) {
  const { t } = useI18n();

  if (c.loadError) {
    return (
      <LoadFailureState title={t('op.management.state.errorTitle')} failure={c.loadError} onRetry={c.retry} />
    );
  }

  if (hasBackend && !c.ready) {
    return (
      <DeferredSkeleton>
        <SetupRuleSkeleton hint={t('op.birthdayGift.enabledHint')} />
        <div className="payset-divider" />
        <SetupFieldsSkeleton hints={[t('op.birthdayGift.amountHint'), t('op.birthdayGift.recentVisitHint')]} />
        <div className="payset-foot"><SkeletonControl width="10rem" /></div>
      </DeferredSkeleton>
    );
  }

  return (
    <>
      <div className={`payset-rule${c.enabled ? ' is-on' : ''}`}>
        <div className="payset-rule-top">
          <label className="payset-switch">
            <input
              type="checkbox"
              aria-label={t('op.birthdayGift.enabled')}
              checked={c.enabled}
              disabled={c.disabled}
              onChange={(event) => c.setEnabled(event.currentTarget.checked)}
            />
            <span className="payset-track" />
            <span className="payset-knob" />
          </label>
          <div className="payset-rule-text">
            <div className="payset-rule-name">{t('op.birthdayGift.enabled')}</div>
            <div className="payset-rule-hint">{t('op.birthdayGift.enabledHint')}</div>
          </div>
        </div>
      </div>

      <div className="payset-divider" />

      <div className="payset-limits">
        <div className="payset-field">
          <label htmlFor="birthday-gift-amount">{`${t('op.birthdayGift.amount')}, ${currencyCode}`}</label>
          <div className="payset-field-input">
            <input
              id="birthday-gift-amount"
              inputMode="decimal"
              value={c.amount}
              disabled={c.disabled}
              onChange={(event) => c.setAmount(event.currentTarget.value)}
            />
          </div>
          <p className="payset-field-hint">{t('op.birthdayGift.amountHint')}</p>
        </div>
        <div className="payset-field">
          <label htmlFor="birthday-gift-visit">{t('op.birthdayGift.recentVisit')}</label>
          <div className="payset-field-input">
            <input
              id="birthday-gift-visit"
              type="number"
              min="0"
              value={c.recentVisitDays}
              disabled={c.disabled}
              onChange={(event) => c.setRecentVisitDays(event.currentTarget.value)}
            />
          </div>
          <p className="payset-field-hint">{t('op.birthdayGift.recentVisitHint')}</p>
        </div>
      </div>

      <div className="payset-foot">
        <button
          type="button"
          className="ui-btn ui-btn--primary"
          disabled={c.disabled || !c.dirty}
          onClick={() => void c.save()}
        >
          {t('op.birthdayGift.save')}
        </button>
      </div>
    </>
  );
}
