import { useEffect, useState } from 'react';
import { CalendarCheck, UserRoundSearch, CircleSlash } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import type { MessageKey } from '@afk4/i18n';
import { ManagementScreen, type SaveState } from '../../ManagementScreen';
import { SetupSection } from '../../kit/SetupSection';
import { projectOperatorError } from '../../../apiErrors';
import { createAuthenticatedOperatorClients, emptyFeedback } from '../../../operatorHelpers';
import { useFeedbackToasts } from '../../../useFeedbackToasts';
import type { Feedback, LoadStatus } from '../../../operatorTypes';
import type { BranchBookingSettingsDto } from '../../../api/clients/settings';
import {
  bookingAcceptanceModes,
  bookingRulesDefaults,
  bookingRulesLimits,
  bookingRulesToForm,
  buildBookingRulesRequest,
  type BookingRulesForm
} from './bookingRulesModel';
import { managementScreenState } from '../types';
import type { DestinationProps } from '../types';

const modeCopy: Record<string, { title: MessageKey; hint: MessageKey }> = {
  off: { title: 'op.bookingRules.mode.off', hint: 'op.bookingRules.mode.off.hint' },
  manual: { title: 'op.bookingRules.mode.manual', hint: 'op.bookingRules.mode.manual.hint' },
  auto: { title: 'op.bookingRules.mode.auto', hint: 'op.bookingRules.mode.auto.hint' }
};

// Тумблер того же вида, что в «Платежах и лояльности»: тактильнее галочки и уже знаком оператору.
function RuleSwitch({
  checked, onChange, disabled, name, hint
}: {
  checked: boolean;
  onChange: (value: boolean) => void;
  disabled: boolean;
  name: string;
  hint: string;
}) {
  return (
    <div className={`payset-rule${checked ? ' is-on' : ''}`}>
      <div className="payset-rule-top">
        <label className="payset-switch">
          <input
            type="checkbox"
            aria-label={name}
            checked={checked}
            disabled={disabled}
            onChange={(event) => onChange(event.currentTarget.checked)}
          />
          <span className="payset-track" />
          <span className="payset-knob" />
        </label>
        <div className="payset-rule-text">
          <div className="payset-rule-name">{name}</div>
          <div className="payset-rule-hint">{hint}</div>
        </div>
      </div>
    </div>
  );
}

function NumberField({
  id, label, hint, unit, value, bounds, disabled, onChange
}: {
  id: string;
  label: string;
  hint: string;
  unit: string;
  value: string;
  bounds: { min: number; max: number };
  disabled: boolean;
  onChange: (value: string) => void;
}) {
  return (
    <div className="payset-field">
      <label htmlFor={id}>{label}</label>
      <div className="payset-field-input">
        <input
          id={id}
          type="number"
          inputMode="numeric"
          min={bounds.min}
          max={bounds.max}
          value={value}
          disabled={disabled}
          onChange={(event) => onChange(event.currentTarget.value)}
        />
        <span className="payset-field-unit">{unit}</span>
      </div>
      <p className="payset-field-hint">{hint}</p>
    </div>
  );
}

// «Приём броней» — настройки филиала, от которых зависит, что вообще происходит с заявкой из
// приложения: приходит ли она, сколько ждёт ответа, что просят с незнакомого гостя и что
// остаётся клубу, если человек не приехал. Экран редкого визита, поэтому у каждой секции есть
// человеческий лид, а у каждого числа — подпись, что случится с этим значением.
export function BookingIntakeDestination({ backend, onDirtyChange }: DestinationProps) {
  const { t, formatDate } = useI18n();
  const [form, setForm] = useState<BookingRulesForm>(bookingRulesDefaults);
  const [baseline, setBaseline] = useState<BookingRulesForm>(bookingRulesDefaults);
  const [updatedAtUtc, setUpdatedAtUtc] = useState<string | null>(null);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>(backend === null ? 'fixture' : 'loading');
  const [loadErrorDetail, setLoadErrorDetail] = useState<string | undefined>();
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [reloadNonce, setReloadNonce] = useState(0);
  useFeedbackToasts(feedback);

  const apply = (settings: BranchBookingSettingsDto) => {
    const mapped = bookingRulesToForm(settings);
    setForm(mapped);
    setBaseline(mapped);
    setUpdatedAtUtc(settings.updatedAtUtc);
    setDirty(false);
  };

  useEffect(() => {
    if (backend === null) {
      setLoadStatus('fixture');
      return undefined;
    }
    let active = true;
    setLoadStatus('loading');
    setLoadErrorDetail(undefined);
    createAuthenticatedOperatorClients(backend.config, backend.session).settings
      .getBookingSettings(backend.branchId)
      .then((settings) => {
        if (!active) return;
        apply(settings);
        setLoadStatus('backend');
      })
      .catch((error) => {
        if (!active) return;
        setLoadStatus('failed');
        setLoadErrorDetail(projectOperatorError(error, t).detail);
      });
    return () => { active = false; };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, reloadNonce]);

  useEffect(() => { onDirtyChange?.(dirty); }, [dirty, onDirtyChange]);

  const onField = <K extends keyof BookingRulesForm>(key: K, value: BookingRulesForm[K]) => {
    setForm((prev) => ({ ...prev, [key]: value }));
    setDirty(true);
    setSaved(false);
  };

  const save = async () => {
    if (backend === null) return;
    const request = buildBookingRulesRequest(backend.session.organizationId, form);
    if (request === null) {
      setFeedback({ label: t('op.management.dest.booking'), state: 'failed', detail: t('op.bookingRules.rangeError') });
      return;
    }
    setSaving(true);
    setFeedback({ label: t('op.management.dest.booking'), state: 'pending' });
    try {
      const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
      apply(await clients.settings.updateBookingSettings(backend.branchId, request));
      setSaved(true);
      setFeedback({ label: t('op.management.dest.booking'), state: 'confirmed' });
    } catch (error) {
      setFeedback({ label: t('op.management.dest.booking'), state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setSaving(false);
    }
  };

  const discard = () => {
    setForm(baseline);
    setDirty(false);
    setSaved(false);
    setFeedback(emptyFeedback);
  };

  const disabled = backend === null || saving;
  const saveState: SaveState = saving ? 'saving' : dirty ? 'dirty' : saved ? 'saved' : 'clean';

  return (
    <ManagementScreen
      title={t('op.management.dest.booking')}
      subtitle={t('op.management.dest.booking.subtitle')}
      contentWidth="wide"
      state={managementScreenState(loadStatus)}
      errorDetail={loadErrorDetail}
      onRetry={() => setReloadNonce((nonce) => nonce + 1)}
      save={{ state: saveState, onSave: () => void save(), onDiscard: discard, disabled }}
    >
      <p className="payset-origin-note">
        {updatedAtUtc === null
          ? t('op.bookingRules.defaultsNote')
          : t('op.bookingRules.updatedAt', { date: formatDate(updatedAtUtc) })}
      </p>

      <div className="payset-columns">
        <SetupSection
          Icon={CalendarCheck}
          title={t('op.bookingRules.zone.acceptance')}
          lead={t('op.bookingRules.zone.acceptance.lead')}
        >
          <div className="payset-rules" role="radiogroup" aria-label={t('op.bookingRules.zone.acceptance')}>
            {bookingAcceptanceModes.map((mode) => (
              <label key={mode} className={`payset-rule payset-mode${form.acceptanceMode === mode ? ' is-on' : ''}`}>
                <div className="payset-rule-top">
                  <input
                    type="radio"
                    name="booking-acceptance-mode"
                    className="payset-mode-radio"
                    value={mode}
                    checked={form.acceptanceMode === mode}
                    disabled={disabled}
                    onChange={() => onField('acceptanceMode', mode)}
                  />
                  <div className="payset-rule-text">
                    <div className="payset-rule-name">{t(modeCopy[mode].title)}</div>
                    <div className="payset-rule-hint">{t(modeCopy[mode].hint)}</div>
                  </div>
                </div>
              </label>
            ))}
          </div>

          <div className="payset-divider" />

          <div className="payset-limits payset-limits--single">
            <NumberField
              id="booking-respond-within"
              label={t('op.bookingRules.respondWithin')}
              hint={t('op.bookingRules.respondWithin.hint')}
              unit={t('op.booking.durationUnit')}
              value={form.respondWithinMinutes}
              bounds={bookingRulesLimits.respondWithinMinutes}
              disabled={disabled}
              onChange={(value) => onField('respondWithinMinutes', value)}
            />
          </div>
        </SetupSection>

        <SetupSection
          Icon={UserRoundSearch}
          title={t('op.bookingRules.zone.newGuests')}
          lead={t('op.bookingRules.zone.newGuests.lead')}
        >
          <div className="payset-limits">
            <NumberField
              id="booking-regular-after"
              label={t('op.bookingRules.regularAfterVisits')}
              hint={t('op.bookingRules.regularAfterVisits.hint')}
              unit={t('op.bookingRules.visitsUnit')}
              value={form.regularAfterVisits}
              bounds={bookingRulesLimits.regularAfterVisits}
              disabled={disabled}
              onChange={(value) => onField('regularAfterVisits', value)}
            />
            <NumberField
              id="booking-max-active"
              label={t('op.bookingRules.maxActive')}
              hint=""
              unit={t('op.bookingRules.bookingsUnit')}
              value={form.maxActiveReservationsForNewGuests}
              bounds={bookingRulesLimits.maxActiveReservationsForNewGuests}
              disabled={disabled}
              onChange={(value) => onField('maxActiveReservationsForNewGuests', value)}
            />
          </div>

          <div className="payset-divider" />

          <RuleSwitch
            checked={form.requirePrepaymentFromNewGuests}
            disabled={disabled}
            name={t('op.bookingRules.requirePrepayment')}
            hint={t('op.bookingRules.requirePrepayment.hint')}
            onChange={(value) => onField('requirePrepaymentFromNewGuests', value)}
          />
        </SetupSection>

        <SetupSection
          Icon={CircleSlash}
          title={t('op.bookingRules.zone.noShow')}
          lead={t('op.bookingRules.zone.noShow.lead')}
        >
          <div className="payset-limits payset-limits--single">
            <NumberField
              id="booking-hold-seat"
              label={t('op.bookingRules.holdSeat')}
              hint={t('op.bookingRules.holdSeat.hint')}
              unit={t('op.booking.durationUnit')}
              value={form.holdSeatAfterStartMinutes}
              bounds={bookingRulesLimits.holdSeatAfterStartMinutes}
              disabled={disabled}
              onChange={(value) => onField('holdSeatAfterStartMinutes', value)}
            />
          </div>

          <div className="payset-divider" />

          <RuleSwitch
            checked={form.keepPrepaymentOnNoShow}
            disabled={disabled}
            name={t('op.bookingRules.keepPrepayment')}
            hint={t('op.bookingRules.keepPrepayment.hint')}
            onChange={(value) => onField('keepPrepaymentOnNoShow', value)}
          />
        </SetupSection>
      </div>
    </ManagementScreen>
  );
}
