import { useEffect, useState } from 'react';
import {
  ShellBridgeRequestTypeNames,
  ShellKeyboardLayoutNames,
  type ShellBridgeRequestTypeName,
  type ShellKeyboardLayoutName,
  type ShellSystemStateDto
} from '@afk4/contracts';
import { useI18n } from '@afk4/i18n';
import { Keyboard, Mic, MicOff, Volume2, VolumeX } from 'lucide-react';
import { requestHost } from '../host/shellHost';

function isSystemState(value: unknown): value is ShellSystemStateDto {
  return typeof value === 'object' && value !== null && ('volume' in value || 'micMuted' in value || 'layout' in value);
}

const LAYOUTS: ShellKeyboardLayoutName[] = [
  ShellKeyboardLayoutNames.Russian,
  ShellKeyboardLayoutNames.English,
  ShellKeyboardLayoutNames.Tajik
];

interface SystemControlsProps {
  /** Что хост знает о ПК; null — хост ещё не сказал, и кнопок нет. */
  system: ShellSystemStateDto | null;
}

/**
 * Громкость, микрофон и раскладка. Без проводника у игрока нет ни значка громкости, ни языка в
 * углу — только эти кнопки. Нажатие меняет вид сразу, до ответа хоста: переключатель, который
 * думает полсекунды, нажимают второй раз. Отказ возвращает как было и говорит почему.
 */
export function SystemControls({ system }: SystemControlsProps) {
  const { t } = useI18n();
  const [shown, setShown] = useState(system);
  const [failed, setFailed] = useState(false);

  useEffect(() => setShown(system), [system]);

  if (!shown) return null;

  const change = async (next: ShellSystemStateDto, type: ShellBridgeRequestTypeName, payload: unknown) => {
    const before = shown;
    setShown(next);
    setFailed(false);
    try {
      const confirmed = await requestHost<ShellSystemStateDto | null>(type, payload);
      // Хост отвечает тем, что стало на ПК. Ответ без этих полей — не состояние, им нельзя затирать показанное.
      if (isSystemState(confirmed)) setShown(confirmed);
    } catch {
      setShown(before);
      setFailed(true);
    }
  };

  const nextLayout = (current: ShellKeyboardLayoutName) =>
    LAYOUTS[(LAYOUTS.indexOf(current) + 1) % LAYOUTS.length];

  return (
    <div className="system-controls">
      {shown.layout ? (
        <button
          type="button"
          className="system-controls__button mono"
          aria-label={`${t('playerShell.system.layout')}: ${shown.layout}`}
          onClick={() => {
            const layout = nextLayout(shown.layout!);
            change({ ...shown, layout }, ShellBridgeRequestTypeNames.SystemSetLayout, { layout });
          }}
        >
          <Keyboard aria-hidden="true" />
          {shown.layout}
        </button>
      ) : null}

      {shown.micMuted != null ? (
        <button
          type="button"
          className="system-controls__button"
          aria-pressed={shown.micMuted}
          aria-label={shown.micMuted ? t('playerShell.system.micOff') : t('playerShell.system.micOn')}
          onClick={() => {
            const micMuted = !shown.micMuted;
            change({ ...shown, micMuted }, ShellBridgeRequestTypeNames.SystemSetMicMuted, { micMuted });
          }}
        >
          {shown.micMuted ? <MicOff aria-hidden="true" /> : <Mic aria-hidden="true" />}
        </button>
      ) : null}

      {shown.volume != null ? (
        <label className="system-controls__volume">
          {shown.volume === 0 ? <VolumeX aria-hidden="true" /> : <Volume2 aria-hidden="true" />}
          <input
            type="range"
            min={0}
            max={100}
            step={5}
            value={shown.volume}
            aria-label={t('playerShell.system.volume')}
            onChange={(event) => {
              const volume = Number(event.currentTarget.value);
              change({ ...shown, volume }, ShellBridgeRequestTypeNames.SystemSetVolume, { volume });
            }}
          />
        </label>
      ) : null}

      {failed ? (
        <span className="system-controls__error" role="status">{t('playerShell.system.unavailable')}</span>
      ) : null}
    </div>
  );
}
