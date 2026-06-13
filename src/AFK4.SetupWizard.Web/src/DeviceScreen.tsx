import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { ArrowLeft, ArrowRight, Loader2 } from 'lucide-react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { isHostBridgeUnavailableError } from './hostBridge';
import {
  type WizardBranch,
  type WizardEnrollResult,
  type WizardInstallClient,
  type WizardRole,
  type WizardSeat,
} from './wizardApi';

interface DeviceScreenProps {
  installClient: WizardInstallClient;
  ownerName: string;
  branch: WizardBranch;
  role: WizardRole;
  defaultDisplayName: string;
  onEnrolled(result: WizardEnrollResult, selectedSeat: WizardSeat | null): void;
  onBusyChange?(installing: boolean): void;
  onBack(): void;
}

type RequestState =
  | { kind: 'idle' }
  | { kind: 'creating' }
  | { kind: 'enrolling' }
  | { kind: 'error'; message: string };

export function DeviceScreen({
  installClient,
  ownerName,
  branch,
  role,
  defaultDisplayName,
  onEnrolled,
  onBusyChange,
  onBack,
}: DeviceScreenProps) {
  const { t } = useI18n();
  const [displayName, setDisplayName] = useState(defaultDisplayName);
  const [request, setRequest] = useState<RequestState>({ kind: 'idle' });

  const requiresSeat = role === 'gaming_pc';
  const trimmedDisplayName = displayName.trim();
  const displayNameValid = trimmedDisplayName.length >= 3 && trimmedDisplayName.length <= 32;
  // Место всегда лежит в зоне; новый филиал сидится с зоной по умолчанию («Main Hall»),
  // поэтому defaultZone практически всегда есть. Guard оставляем на случай пустого филиала.
  const defaultZone = branch.zones[0] ?? null;
  const canEnroll = displayNameValid && (!requiresSeat || defaultZone !== null);

  const busy = request.kind === 'creating' || request.kind === 'enrolling';
  // Титлбар защищает кнопку закрытия, пока идёт enroll (msiexec на хосте).
  const installing = request.kind === 'enrolling';
  useEffect(() => {
    onBusyChange?.(installing);
    return () => onBusyChange?.(false);
  }, [installing, onBusyChange]);

  const handleSubmit = useCallback(
    async (event: FormEvent<HTMLFormElement>) => {
      event.preventDefault();
      if (!canEnroll || busy) {
        return;
      }
      try {
        let seat: WizardSeat | null = null;
        if (requiresSeat) {
          setRequest({ kind: 'creating' });
          // canEnroll (checked above) already guarantees defaultZone !== null when requiresSeat is true.
          seat = await installClient.createSeat({
            branchId: branch.branchId,
            zoneId: defaultZone!.zoneId,
            zoneName: defaultZone!.name,
            name: trimmedDisplayName,
          });
        }
        setRequest({ kind: 'enrolling' });
        const result = await installClient.enrollDevice({
          branchId: branch.branchId,
          seatId: seat ? seat.seatId : null,
          role,
          displayName: trimmedDisplayName,
        });
        onEnrolled(result, seat);
      } catch (error) {
        setRequest({ kind: 'error', message: messageForError(error, t) });
      }
    },
    [
      branch.branchId,
      busy,
      canEnroll,
      defaultZone,
      installClient,
      onEnrolled,
      requiresSeat,
      role,
      t,
      trimmedDisplayName,
    ],
  );

  const titleKey: MessageKey = requiresSeat
    ? 'setup.wizard.device.gaming.title'
    : 'setup.wizard.device.manager.title';
  const subtitleKey: MessageKey = requiresSeat
    ? 'setup.wizard.device.gaming.subtitle'
    : 'setup.wizard.device.manager.subtitle';

  return (
    <section className="wizard-screen is-framed is-narrow">
      <div className="wizard-screen-head">
        <span className="wizard-screen-context">{ownerName} · {branch.branchName}</span>
        <div className="wizard-screen-title-row">
          <span className="wizard-screen-step" aria-hidden>4</span>
          <h1>{t(titleKey)}</h1>
        </div>
        <p>{t(subtitleKey)}</p>
      </div>

      <form className="wizard-form" onSubmit={handleSubmit} noValidate>
        <label className="wizard-field">
          <span className="wizard-field-label">{t('setup.wizard.device.field.name')}</span>
          <input
            type="text"
            value={displayName}
            autoComplete="off"
            spellCheck={false}
            minLength={3}
            maxLength={32}
            autoFocus
            onChange={(event) => setDisplayName(event.target.value)}
            placeholder={defaultDisplayName}
            aria-invalid={trimmedDisplayName.length > 0 && !displayNameValid}
          />
        </label>

        {request.kind === 'error' && (
          <div role="alert" className="wizard-alert">
            {request.message}
          </div>
        )}

        <div className="wizard-actions">
          <button type="button" className="wizard-secondary" onClick={onBack} disabled={busy}>
            <ArrowLeft aria-hidden />
            <span>{t('setup.wizard.common.back')}</span>
          </button>
          <button type="submit" className="wizard-primary" disabled={!canEnroll || busy}>
            {busy ? (
              <>
                <Loader2 className="wizard-spinner" aria-hidden />
                <span>{t('setup.wizard.device.action.enrolling')}</span>
              </>
            ) : (
              <>
                <span>{t('setup.wizard.device.action.enroll')}</span>
                <ArrowRight aria-hidden />
              </>
            )}
          </button>
        </div>
      </form>
    </section>
  );
}

function messageForError(error: unknown, t: (key: MessageKey) => string): string {
  if (isHostBridgeUnavailableError(error)) {
    return t('setup.wizard.device.error.bridgeMissing');
  }
  if (error instanceof Error && error.message) {
    return error.message;
  }
  return t('setup.wizard.device.error.generic');
}
