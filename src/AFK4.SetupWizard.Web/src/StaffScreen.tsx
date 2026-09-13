import { useState } from 'react';
import { ArrowLeft, ArrowRight, Loader2, UserPlus } from 'lucide-react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import type { WizardStaffInvited } from './wizardApi';

// Владельца в списке нет: он и так есть — это тот, кто сейчас ставит клуб.
const ROLES: { name: string; labelKey: MessageKey }[] = [
  { name: 'branch_manager', labelKey: 'roles.branch_manager' },
  { name: 'shift_supervisor', labelKey: 'roles.shift_supervisor' },
  { name: 'operator', labelKey: 'roles.operator' },
  { name: 'technician', labelKey: 'roles.technician' },
  { name: 'accountant', labelKey: 'roles.accountant' },
];

export interface StaffClient {
  invite(displayName: string, phoneNumber: string, roleName: string): Promise<WizardStaffInvited>;
}

interface StaffScreenProps {
  /// Номер шага в ЭТОМ прогоне мастера: шаги пропускаются, зашитая цифра врала.
  stepNumber: number;
  client: StaffClient;
  ownerName: string;
  branchName: string;
  onContinue(): void;
  onBack(): void;
}

export function StaffScreen({ stepNumber, client, ownerName, branchName, onContinue, onBack }: StaffScreenProps) {
  const { t, formatDate } = useI18n();
  const [displayName, setDisplayName] = useState('');
  const [phoneNumber, setPhoneNumber] = useState('');
  const [roleName, setRoleName] = useState(ROLES[2].name);
  const [invited, setInvited] = useState<WizardStaffInvited[]>([]);
  const [sending, setSending] = useState(false);
  const [failed, setFailed] = useState(false);

  const canSend = displayName.trim() !== '' && phoneNumber.trim() !== '' && !sending;

  async function invite(): Promise<void> {
    if (!canSend) return;
    setSending(true);
    setFailed(false);
    try {
      const result = await client.invite(displayName.trim(), phoneNumber.trim(), roleName);
      setInvited((current) => [...current, result]);
      setDisplayName('');
      setPhoneNumber('');
    } catch {
      setFailed(true);
    } finally {
      setSending(false);
    }
  }

  return (
    <section className="wizard-screen is-narrow">
      <div className="wizard-screen-head">
        <span className="wizard-screen-context">{ownerName} · {branchName}</span>
        <div className="wizard-screen-title-row">
          <span className="wizard-screen-step" aria-hidden>{stepNumber}</span>
          <h1>{t('setup.wizard.staff.title')}</h1>
        </div>
        <p>{t('setup.wizard.staff.subtitle')}</p>
      </div>

      <div className="wizard-field">
        <label className="wizard-field-label" htmlFor="staff-name">{t('setup.wizard.staff.name')}</label>
        <input
          id="staff-name"
          className="wizard-input"
          value={displayName}
          onChange={(event) => setDisplayName(event.target.value)}
        />
      </div>

      <div className="wizard-field">
        <label className="wizard-field-label" htmlFor="staff-phone">{t('setup.wizard.staff.phone')}</label>
        <input
          id="staff-phone"
          className="wizard-input"
          type="tel"
          inputMode="tel"
          placeholder="+992 90 000-00-00"
          value={phoneNumber}
          onChange={(event) => setPhoneNumber(event.target.value)}
        />
      </div>

      <div className="wizard-field">
        <label className="wizard-field-label" htmlFor="staff-role">{t('setup.wizard.staff.role')}</label>
        <select
          id="staff-role"
          className="wizard-input"
          value={roleName}
          onChange={(event) => setRoleName(event.target.value)}
        >
          {ROLES.map((role) => (
            <option key={role.name} value={role.name}>{t(role.labelKey)}</option>
          ))}
        </select>
      </div>

      <button type="button" className="wizard-button is-ghost" onClick={() => void invite()} disabled={!canSend}>
        {sending ? <Loader2 size={16} className="wizard-spin" aria-hidden /> : <UserPlus size={16} aria-hidden />}
        {t('setup.wizard.staff.add')}
      </button>

      {failed ? <p className="wizard-error">{t('setup.wizard.staff.failed')}</p> : null}

      {invited.length > 0 ? (
        <ul className="wizard-staff-list">
          {invited.map((staff) => (
            <li key={staff.code}>
              <span className="wizard-staff-name">
                {staff.displayName} · {t(`roles.${staff.roleName}` as MessageKey)}
              </span>
              {/* Код показываем здесь же: SMS может не дойти, а человек стоит рядом. */}
              <code className="wizard-staff-code">{staff.code}</code>
              <span className="wizard-staff-expiry">
                {t('setup.wizard.staff.expires', { time: formatDate(staff.expiresAtUtc) })}
              </span>
            </li>
          ))}
        </ul>
      ) : null}

      <div className="wizard-actions">
        <button type="button" className="wizard-button is-ghost" onClick={onBack}>
          <ArrowLeft size={16} aria-hidden />
          {t('setup.wizard.common.back')}
        </button>
        <button type="button" className="wizard-button" onClick={onContinue} disabled={sending}>
          <ArrowRight size={16} aria-hidden />
          {invited.length > 0 ? t('setup.wizard.staff.next') : t('setup.wizard.staff.skip')}
        </button>
      </div>
    </section>
  );
}
