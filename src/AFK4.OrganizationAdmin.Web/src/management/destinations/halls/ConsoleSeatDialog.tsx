import { useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { PanelModal } from '../../../PanelModal';

type SeatOption = { seatId: string; label: string };

/**
 * Место для консоли: выбрать свободное место и назвать консоль. Консоль без агента — сессию на
 * ней ведёт администратор; тарифы, касса и отчёты — как у ПК.
 */
export function ConsoleSeatDialog({
  freeSeats,
  busy,
  onSubmit,
  onClose
}: {
  freeSeats: SeatOption[];
  busy: boolean;
  onSubmit: (seatId: string, displayName: string) => void;
  onClose: () => void;
}) {
  const { t } = useI18n();
  const [seatId, setSeatId] = useState(freeSeats[0]?.seatId ?? '');
  const [name, setName] = useState('');
  const canSubmit = !busy && seatId !== '' && name.trim() !== '';

  return (
    <PanelModal title={t('op.settings.devices.console.title')} onClose={onClose} closeDisabled={busy}>
      <form
        className="mgmt-form"
        onSubmit={(event) => { event.preventDefault(); if (canSubmit) onSubmit(seatId, name.trim()); }}
      >
        <p className="mgmt-drawer-hint">{t('op.settings.devices.console.lead')}</p>
        {freeSeats.length === 0 ? (
          <p className="ui-inline-error" role="alert">{t('op.settings.devices.console.noSeats')}</p>
        ) : (
          <div className="mgmt-form-grid">
            <label>{t('op.settings.devices.console.seat')}
              <select value={seatId} disabled={busy} onChange={(event) => setSeatId(event.currentTarget.value)}>
                {freeSeats.map((seat) => <option key={seat.seatId} value={seat.seatId}>{seat.label}</option>)}
              </select>
            </label>
            <label>{t('op.settings.devices.console.name')}
              <input
                value={name}
                maxLength={80}
                placeholder={t('op.settings.devices.console.namePlaceholder')}
                disabled={busy}
                onChange={(event) => setName(event.currentTarget.value)}
                autoFocus
              />
            </label>
          </div>
        )}
        <div className="mgmt-form-actions">
          <button type="button" className="ui-btn" disabled={busy} onClick={onClose}>{t('common.cancel')}</button>
          <button type="submit" className="ui-btn ui-btn--primary" disabled={!canSubmit}>{t('op.settings.devices.console.create')}</button>
        </div>
      </form>
    </PanelModal>
  );
}
