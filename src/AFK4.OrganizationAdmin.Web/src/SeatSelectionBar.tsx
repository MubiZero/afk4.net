import { X } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import type { SeatSummary } from './operatorData';
import { bulkCommands } from './pc/pcBulk';
import { PC_COMMAND_ICONS, PC_COMMAND_LABELS, type PcCommandOrLock } from './pc/pcCommandCopy';
import type { PcCommandAccess } from './pc/pcCommandOptions';

/**
 * Полоса выбранных мест: сколько выбрано и что с ними сделать разом. Каждая кнопка открывает
 * подтверждение со списком — кому команда уйдёт и кому нет.
 */
export function SeatSelectionBar({
  seats,
  access,
  busy,
  onCommand,
  onClear
}: {
  seats: SeatSummary[];
  access: PcCommandAccess;
  busy: boolean;
  onCommand: (command: PcCommandOrLock) => void;
  onClear: () => void;
}) {
  const { t } = useI18n();
  const commands = bulkCommands(seats, access);

  return (
    <div className="seat-selection-bar" role="toolbar" aria-label={t('op.map.bulk.label')}>
      <span className="seat-selection-count">{t('op.map.bulk.count', { count: seats.length })}</span>
      <div className="seat-selection-actions">
        {commands.map((command) => (
          <button key={command} type="button" className="ui-btn ui-btn--sm" disabled={busy} onClick={() => onCommand(command)}>
            {PC_COMMAND_ICONS[command]}
            <span>{t(PC_COMMAND_LABELS[command])}</span>
          </button>
        ))}
      </div>
      <button type="button" className="ui-btn ui-btn--sm seat-selection-clear" onClick={onClear}>
        <X size={14} aria-hidden="true" />
        <span>{t('op.map.bulk.clear')}</span>
      </button>
    </div>
  );
}
