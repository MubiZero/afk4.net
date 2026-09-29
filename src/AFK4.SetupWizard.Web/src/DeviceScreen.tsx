import { useCallback, useEffect, useMemo, useState, type FormEvent } from 'react';
import { ArrowRight, Loader2 } from 'lucide-react';
import { Button } from '@afk4/ui/react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { wizardErrorMessage } from './wizardErrors';
import { WizardStepLayout } from './WizardStepLayout';
import {
  type WizardBranch,
  type WizardEnrollResult,
  type WizardInstallClient,
  type WizardRole,
  type WizardSeat,
} from './wizardApi';

/// Введённое на экране. Живёт в App: экран монтируется заново на каждом шаге, и после «Назад» и
/// обратно исправленное имя ПК снова становилось именем машины.
export interface DeviceDraft {
  displayName: string;
  /// Выбор места — только когда его спрашивали (игровой ПК). На рабочем месте управляющего
  /// списка нет, и «новое место» там не выбор человека, а умолчание, которое не должно
  /// перебить догадку по имени машины, если роль потом сменят.
  seatChoice: string | null;
}

interface DeviceScreenProps {
  /// Номер шага в ЭТОМ прогоне мастера: шаги пропускаются, зашитая цифра врала.
  stepNumber: number;
  installClient: WizardInstallClient;
  ownerName: string;
  branch: WizardBranch;
  role: WizardRole;
  defaultDisplayName: string;
  /// Что было введено при прошлом заходе на шаг; null — заход первый.
  initialDraft?: DeviceDraft | null;
  onEnrolled(result: WizardEnrollResult, selectedSeat: WizardSeat | null): void;
  onBusyChange?(installing: boolean): void;
  onBack(draft: DeviceDraft): void;
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
  initialDraft = null,
  onEnrolled,
  onBusyChange,
  onBack,
}: DeviceScreenProps) {
  const { t } = useI18n();
  const [displayName, setDisplayName] = useState(initialDraft?.displayName ?? defaultDisplayName);
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
    const previous = initialDraft?.seatChoice;
    if (previous != null && (previous === NEW_SEAT || freeSeats.some((seat) => seat.seatId === previous))) {
      return previous;
    }
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
        // Словарь отказов общий на весь мастер: те же коды приходят и на шагах настройки клуба,
        // и держать для них второй список значило бы объяснять одну и ту же причину дважды.
        setRequest({ kind: 'error', message: wizardErrorMessage(error, t, 'setup.wizard.device.error.generic') });
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
    <WizardStepLayout
      stepNumber={stepNumber}
      context={`${ownerName} · ${branch.branchName}`}
      title={t(titleKey)}
      subtitle={t(subtitleKey)}
      onSubmit={handleSubmit}
      onBack={() => onBack({ displayName, seatChoice: freeSeats.length > 0 ? seatChoice : null })}
      backDisabled={busy}
      primary={(
        <Button type="submit" variant="primary" disabled={!canEnroll || busy}>
          {busy ? (
            <>
              <Loader2 size={16} className="ui-spinner" aria-hidden />
              {t('setup.wizard.device.action.enrolling')}
            </>
          ) : (
            <>
              {t('setup.wizard.device.action.enroll')}
              <ArrowRight size={16} aria-hidden />
            </>
          )}
        </Button>
      )}
    >
      <div className="wizard-form">
        {/* Свободных мест нет — место заведётся само, и сказать об этом надо до нажатия:
            иначе в зале молча появляется ещё одна строка, о которой человек узнаёт из панели. */}
        {requiresSeat && freeSeats.length === 0 && defaultZone !== null && (
          <p className="ui-field-hint">
            {t('setup.wizard.device.seat.willCreate', { name: trimmedDisplayName || defaultDisplayName })}
          </p>
        )}

        {/* Зала нет вовсе — место создавать негде. Раньше кнопка просто не нажималась, и
            причину человек у ПК не узнавал ниоткуда. */}
        {requiresSeat && defaultZone === null && (
          <div role="alert" className="ui-alert">
            {t('setup.wizard.device.seat.noZone')}
          </div>
        )}

        {freeSeats.length > 0 && (
          <label className="ui-field">
            <span className="ui-field-label">{t('setup.wizard.device.seat.label')}</span>
            <select value={seatChoice} onChange={(event) => setSeatChoice(event.target.value)}>
              {freeSeats.map((seat) => (
                <option key={seat.seatId} value={seat.seatId}>
                  {seat.zoneName} · {seat.pcName}
                </option>
              ))}
              <option value={NEW_SEAT}>{t('setup.wizard.device.seat.new')}</option>
            </select>
            <span className="ui-field-hint">{t('setup.wizard.device.seat.hint')}</span>
          </label>
        )}

        <label className="ui-field">
          <span className="ui-field-label">{t('setup.wizard.device.field.name')}</span>
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
          <div role="alert" className="ui-alert">
            {request.message}
          </div>
        )}

      </div>
    </WizardStepLayout>
  );
}
