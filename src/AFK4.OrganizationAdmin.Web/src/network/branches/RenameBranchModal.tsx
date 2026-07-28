import { useState } from 'react';
import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { PanelModal } from '../../PanelModal';

// Which branch is being renamed lives in the caller's closure (it already has renameTarget.branchId
// to pass to updateBranchProfile) — this modal only edits the name/city fields, so it doesn't need
// the id itself.
export function RenameBranchModal({ organizationId, initialName, initialCity, onClose, onSave }: {
  organizationId: string;
  initialName: string;
  initialCity: string;
  onClose: () => void;
  onSave: (request: { organizationId: string; name: string; city: string }) => Promise<void>;
}): JSX.Element {
  const { t } = useI18n();
  const [name, setName] = useState(initialName);
  const [city, setCity] = useState(initialCity);
  const [busy, setBusy] = useState(false);
  const valid = name.trim() !== '' && city.trim() !== '';

  async function submit() {
    setBusy(true);
    try {
      await onSave({ organizationId, name: name.trim(), city: city.trim() });
      onClose();
    } finally {
      setBusy(false);
    }
  }

  return (
    <PanelModal title={t('op.network.branches.rename.title')} onClose={onClose} closeDisabled={busy}>
      <form className="mgmt-form" onSubmit={(e) => { e.preventDefault(); if (valid) void submit(); }}>
        <div className="mgmt-form-grid">
          <label>{t('op.network.branches.field.name')}
            <input value={name} disabled={busy} autoFocus onChange={(e) => setName(e.currentTarget.value)} />
          </label>
          <label>{t('op.network.branches.field.city')}
            <input value={city} disabled={busy} onChange={(e) => setCity(e.currentTarget.value)} />
          </label>
        </div>
        <div className="mgmt-form-actions">
          <button type="button" className="ui-btn" onClick={onClose} disabled={busy}>{t('common.cancel')}</button>
          <button type="submit" className="ui-btn ui-btn--primary" disabled={busy || !valid}>{t('common.save')}</button>
        </div>
      </form>
    </PanelModal>
  );
}
