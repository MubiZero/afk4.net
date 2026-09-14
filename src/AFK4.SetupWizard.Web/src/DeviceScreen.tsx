import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react';
import { ArrowLeft, ArrowRight, Loader2 } from 'lucide-react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { HostBridgeRequestError, isHostBridgeUnavailableError } from './hostBridge';
import {
  type WizardBranch,
  type WizardEnrollResult,
  type WizardInstallClient,
  type WizardRole,
  type WizardSeat,
} from './wizardApi';

interface DeviceScreenProps {
  /// Номер шага в ЭТОМ прогоне мастера: шаги пропускаются, зашитая цифра врала.
  stepNumber: number;
  installClient: WizardInstallClient;
  ownerName: string;
  branch: WizardBranch;
  role: WizardRole;
  defaultDisplayName: string;
  onEnrolled(result: WizardEnrollResult, selectedSeat: WizardSeat | null): void;
  onBusyChange?(installing: boolean): void;
  onBack(): void;
}

/// Значение «завести новое место» в списке — отдельное от любого seatId.
const NEW_SEAT = '__new__';

type RequestState =
  | { kind: 'idle' }
  | { kind: 'creating' }
  | { kind: 'enrolling' }
  | { kind: 'error'; message: string };

export function DeviceScreen({
  stepNumber,
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

  // Свободные места, которые в этом зале уже заведены. Раньше экран их игнорировал и ВСЕГДА
  // создавал новое: на собственном счастливом пути мастера зал заводится на рабочем месте
  // управляющего, а потом каждый игровой ПК добавлял к нему ещё одно лишнее место.
  const freeSeats = useMemo(() => {
    if (!requiresSeat) return [];
    const free = new Set(branch.freeSeatIds);
    return branch.seats.filter((seat) => free.has(seat.seatId));
  }, [branch.freeSeatIds, branch.seats, requiresSeat]);

  // Умолчание: место, чьё имя совпадает с именем машины, иначе первое свободное. Человек
  // подтверждает догадку вместо того, чтобы искать себя в списке.
  const [seatChoice, setSeatChoice] = useState<string>(() => {
    if (freeSeats.length === 0) return NEW_SEAT;
    const machineName = defaultDisplayName.trim().toLowerCase();
    const matched = freeSeats.find((seat) => seat.pcName.trim().toLowerCase() === machineName);
    return (matched ?? freeSeats[0]).seatId;
  });

  const selectedExistingSeat = freeSeats.find((seat) => seat.seatId === seatChoice) ?? null;
  const createsSeat = requiresSeat && seatChoice === NEW_SEAT;
  const canEnroll =
    displayNameValid
    && (!requiresSeat || selectedExistingSeat !== null || defaultZone !== null);

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
        let seat: WizardSeat | null = selectedExistingSeat;
        if (createsSeat) {
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
      createsSeat,
      defaultZone,
      installClient,
      onEnrolled,
      role,
      selectedExistingSeat,
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
          <span className="wizard-screen-step" aria-hidden>{stepNumber}</span>
          <h1>{t(titleKey)}</h1>
        </div>
        <p>{t(subtitleKey)}</p>
      </div>

      <form className="wizard-form" onSubmit={handleSubmit} noValidate>
        {freeSeats.length > 0 && (
          <label className="wizard-field">
            <span className="wizard-field-label">{t('setup.wizard.device.seat.label')}</span>
            <select value={seatChoice} onChange={(event) => setSeatChoice(event.target.value)}>
              {freeSeats.map((seat) => (
                <option key={seat.seatId} value={seat.seatId}>
                  {seat.zoneName} · {seat.pcName}
                </option>
              ))}
              <option value={NEW_SEAT}>{t('setup.wizard.device.seat.new')}</option>
            </select>
            <span className="wizard-field-hint">{t('setup.wizard.device.seat.hint')}</span>
          </label>
        )}

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

/**
 * Отказ словами, понятными тому, кто ставит клуб.
 *
 * Раньше здесь стояло `return error.message`, и в русский мастер приезжали английские строки
 * из .NET: «Seat name is required.», «Role must be GamingPc or ManagerWorkstation.», «No such
 * host is known.», «Platform API returned 500 for /api/...». Мост при этом с самого начала нёс
 * КОД ошибки рядом с текстом — им и пользуемся, а текст остаётся в журнале, где он и нужен.
 */
function messageForError(error: unknown, t: (key: MessageKey) => string): string {
  if (isHostBridgeUnavailableError(error)) {
    return t('setup.wizard.device.error.bridgeMissing');
  }
  if (error instanceof HostBridgeRequestError) {
    return t(DEVICE_ERROR_KEYS[error.code] ?? 'setup.wizard.device.error.generic');
  }
  return t('setup.wizard.device.error.generic');
}

// Коды моста (SetupWizardWebHostBridge.ErrorCodeFor) — человеческим языком. Незнакомый код
// попадает в общую надпись: выдумывать объяснение коду, которого мы не знаем, хуже, чем сказать
// «не получилось, попробуйте ещё раз».
const DEVICE_ERROR_KEYS: Record<string, MessageKey> = {
  wizard_create_seat_failed: 'setup.wizard.device.error.createSeat',
  wizard_enroll_failed: 'setup.wizard.device.error.enroll',
  wizard_request_failed: 'setup.wizard.device.error.generic',
  host_timeout: 'setup.wizard.device.error.timedOut'
};
