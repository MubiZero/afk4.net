import { useState, type FormEvent } from 'react';
import { ArrowLeft, ArrowRight, Loader2, UserPlus } from 'lucide-react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import type { WizardStaffInvited } from './wizardApi';
import { wizardErrorMessage } from './wizardErrors';
import { localPhoneDigits, formatLocal, fullPhoneDigits } from './phoneFormat';

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

/// Введённое на экране. Живёт в App: экран монтируется заново на каждом шаге, и при «Назад»
/// пропадал список приглашённых вместе с их кодами — а код здесь единственная копия на случай,
/// если SMS не дойдёт.
export interface StaffDraft {
  displayName: string;
  phoneNumber: string;
  roleName: string;
  invited: WizardStaffInvited[];
}

interface StaffScreenProps {
  /// Номер шага в ЭТОМ прогоне мастера: шаги пропускаются, зашитая цифра врала.
  stepNumber: number;
  client: StaffClient;
  ownerName: string;
  branchName: string;
  /// Что было введено при прошлом заходе на шаг; null — заход первый.
  initialDraft?: StaffDraft | null;
  onContinue(draft: StaffDraft): void;
  onBack(draft: StaffDraft): void;
}

export function StaffScreen({
  stepNumber,
  client,
  ownerName,
  branchName,
  initialDraft = null,
  onContinue,
  onBack,
}: StaffScreenProps) {
  const { t, formatDate } = useI18n();
  const [displayName, setDisplayName] = useState(initialDraft?.displayName ?? '');
  const [phoneNumber, setPhoneNumber] = useState(initialDraft?.phoneNumber ?? '');
  const [roleName, setRoleName] = useState(initialDraft?.roleName ?? ROLES[2].name);
  const [invited, setInvited] = useState<WizardStaffInvited[]>(initialDraft?.invited ?? []);
  const [sending, setSending] = useState(false);
  const [failure, setFailure] = useState<string | null>(null);
  const [phoneTouched, setPhoneTouched] = useState(false);

  // Номер набирают со слуха, в шумном клубе. Проверка та же, что на входе в мастер: девять цифр
  // местной части, префикс +992 вне поля. Раньше поле принимало любую непустую строку — номер с
  // опечаткой в одной цифре уходил приглашением постороннему человеку, и узнать об этом было
  // неоткуда: поле очищается сразу после отправки.
  const phoneComplete = localPhoneDigits(phoneNumber).length === 9;
  const showPhoneHint = phoneTouched && phoneNumber.trim().length > 0 && !phoneComplete;
  const canSend = displayName.trim() !== '' && phoneComplete && !sending;
  const draft: StaffDraft = { displayName, phoneNumber, roleName, invited };

  async function invite(event: FormEvent<HTMLFormElement>): Promise<void> {
    event.preventDefault();
    if (!canSend) return;
    setSending(true);
    setFailure(null);
    try {
      const result = await client.invite(displayName.trim(), fullPhoneDigits(phoneNumber), roleName);
      setInvited((current) => [...current, result]);
      setDisplayName('');
      setPhoneNumber('');
      setPhoneTouched(false);
    } catch (error) {
      setFailure(wizardErrorMessage(error, t, 'setup.wizard.staff.failed'));
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

      {/* Форма — ради Enter: человек набрал номер и жмёт Enter, как на входе и на экране
          устройства. Поля вне формы Enter молча проглатывали. «Дальше» в форму не входит: Enter
          в поле приглашает, а не уводит со шага. */}
      <form className="wizard-form" onSubmit={invite} noValidate>
        <div className="ui-field">
          <label className="ui-field-label" htmlFor="staff-name">{t('setup.wizard.staff.name')}</label>
          <input
            id="staff-name"
            value={displayName}
            onChange={(event) => setDisplayName(event.target.value)}
          />
        </div>

        <div className="ui-field">
          <label className="ui-field-label" htmlFor="staff-phone">{t('setup.wizard.staff.phone')}</label>
          <div className="ui-phone-field">
            <span className="ui-phone-prefix" aria-hidden>+992</span>
            <input
              id="staff-phone"
              type="tel"
              inputMode="tel"
              placeholder="90 000 00 00"
              value={phoneNumber}
              aria-invalid={showPhoneHint}
              onBlur={() => setPhoneTouched(true)}
              onChange={(event) => setPhoneNumber(formatLocal(event.target.value))}
            />
          </div>
          {showPhoneHint && <span className="ui-field-hint">{t('setup.wizard.staff.phoneIncomplete')}</span>}
        </div>

        <div className="ui-field">
          <label className="ui-field-label" htmlFor="staff-role">{t('setup.wizard.staff.role')}</label>
          <select
            id="staff-role"
            value={roleName}
            onChange={(event) => setRoleName(event.target.value)}
          >
            {ROLES.map((role) => (
              <option key={role.name} value={role.name}>{t(role.labelKey)}</option>
            ))}
          </select>
        </div>

        {/* Работа экрана — пригласить, а не уйти с него: пока никого нет, главное действие здесь. */}
        <button
          type="submit"
          className={invited.length > 0 ? 'ui-btn' : 'ui-btn ui-btn--primary'}
          disabled={!canSend}
        >
          {sending ? <Loader2 size={16} className="ui-spinner" aria-hidden /> : <UserPlus size={16} aria-hidden />}
          {t('setup.wizard.staff.add')}
        </button>
      </form>

      {failure === null ? null : <p className="ui-alert" role="alert">{failure}</p>}

      {invited.length > 0 ? (
        <ul className="wizard-staff-list">
          {invited.map((staff) => (
            <li key={staff.code}>
              <span>
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
        <button type="button" className="ui-btn" onClick={() => onBack(draft)}>
          <ArrowLeft size={16} aria-hidden />
          {t('setup.wizard.common.back')}
        </button>
        <button
          type="button"
          className={invited.length > 0 ? 'ui-btn ui-btn--primary' : 'ui-btn'}
          onClick={() => onContinue(draft)}
          disabled={sending}
        >
          <ArrowRight size={16} aria-hidden />
          {invited.length > 0 ? t('setup.wizard.staff.next') : t('setup.wizard.staff.skip')}
        </button>
      </div>
    </section>
  );
}
