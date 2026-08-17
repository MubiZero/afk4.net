import { useI18n } from '@afk4/i18n';
import { EmptyState } from '../../../operatorPrimitives';
import type { ReferralSettingsController } from './useReferralSettings';

interface Props {
  controller: ReferralSettingsController;
  currencyCode: string;
  hasBackend: boolean;
}

// «Приведи друга»: платит клуб, суммы назначает он же — как и кэшбэк рядом. Своя кнопка
// сохранения внутри секции, глобального save-бара на экране нет.
export function ReferralSection({ controller: c, currencyCode, hasBackend }: Props) {
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
      <div className="payset-loading" data-testid="referral-skeleton" aria-hidden="true">
        <div className="management-skeleton-line" />
        <div className="management-skeleton-line" />
      </div>
    );
  }

  return (
    <>
      <div className={`payset-rule${c.enabled ? ' is-on' : ''}`}>
        <div className="payset-rule-top">
          <label className="payset-switch">
            <input
              type="checkbox"
              aria-label={t('op.referral.enabled')}
              checked={c.enabled}
              disabled={c.disabled}
              onChange={(event) => c.setEnabled(event.currentTarget.checked)}
            />
            <span className="payset-track" />
            <span className="payset-knob" />
          </label>
          <div className="payset-rule-text">
            <div className="payset-rule-name">{t('op.referral.enabled')}</div>
            <div className="payset-rule-hint">{t('op.referral.enabledHint')}</div>
          </div>
        </div>
      </div>

      <div className="payset-divider" />

      <div className="payset-subhead">{t('op.referral.amounts.title')}</div>
      <div className="payset-limits">
        <div className="payset-field">
          <label htmlFor="referral-referrer">{`${t('op.referral.referrerBonus')}, ${currencyCode}`}</label>
          <div className="payset-field-input">
            <input
              id="referral-referrer"
              inputMode="decimal"
              value={c.referrerBonus}
              disabled={c.disabled}
              onChange={(event) => c.setReferrerBonus(event.currentTarget.value)}
            />
          </div>
          <p className="payset-field-hint">{t('op.referral.referrerBonusHint')}</p>
        </div>
        <div className="payset-field">
          <label htmlFor="referral-invitee">{`${t('op.referral.inviteeBonus')}, ${currencyCode}`}</label>
          <div className="payset-field-input">
            <input
              id="referral-invitee"
              inputMode="decimal"
              value={c.inviteeBonus}
              disabled={c.disabled}
              onChange={(event) => c.setInviteeBonus(event.currentTarget.value)}
            />
          </div>
          <p className="payset-field-hint">{t('op.referral.inviteeBonusHint')}</p>
        </div>
      </div>

      <div className="payset-divider" />

      <div className="payset-subhead">{t('op.referral.limits.title')}</div>
      <p className="payset-field-hint" style={{ marginTop: -6, marginBottom: 14 }}>
        {t('op.referral.limits.hint')}
      </p>
      <div className="payset-limits">
        <div className="payset-field">
          <label htmlFor="referral-minimum">{`${t('op.referral.minimumTopUp')}, ${currencyCode}`}</label>
          <div className="payset-field-input">
            <input
              id="referral-minimum"
              inputMode="decimal"
              value={c.minimumTopUp}
              disabled={c.disabled}
              onChange={(event) => c.setMinimumTopUp(event.currentTarget.value)}
            />
          </div>
          <p className="payset-field-hint">{t('op.referral.minimumTopUpHint')}</p>
        </div>
        <div className="payset-field">
          <label htmlFor="referral-window">{t('op.referral.claimWindow')}</label>
          <div className="payset-field-input">
            <input
              id="referral-window"
              type="number"
              min="0"
              value={c.claimWindowDays}
              disabled={c.disabled}
              onChange={(event) => c.setClaimWindowDays(event.currentTarget.value)}
            />
          </div>
          <p className="payset-field-hint">{t('op.referral.claimWindowHint')}</p>
        </div>
        <div className="payset-field">
          <label htmlFor="referral-cap">{t('op.referral.maxRewarded')}</label>
          <div className="payset-field-input">
            <input
              id="referral-cap"
              type="number"
              min="0"
              value={c.maxRewardedPerReferrer}
              disabled={c.disabled}
              onChange={(event) => c.setMaxRewardedPerReferrer(event.currentTarget.value)}
            />
          </div>
          <p className="payset-field-hint">{t('op.referral.maxRewardedHint')}</p>
        </div>
      </div>

      <div className="payset-foot">
        <button
          type="button"
          className="ui-btn ui-btn--primary"
          disabled={c.disabled || !c.dirty}
          onClick={() => void c.save()}
        >
          {t('op.referral.save')}
        </button>
      </div>
    </>
  );
}
