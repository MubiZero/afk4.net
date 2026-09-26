import { useMemo, useState, type ChangeEvent } from 'react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import type { GuestImportResultDto } from '@afk4/contracts';
import { PanelModal } from '../PanelModal';
import { Money } from '../operatorPrimitives';
import { projectOperatorError } from '../apiErrors';
import { parseGuestFile, type ParsedGuestFile } from './guestImportCsv';

export interface GuestImportClient {
  importGuests(request: {
    organizationId: string;
    currencyCode: string;
    source: string;
    rows: ParsedGuestFile['rows'];
    dryRun: boolean;
    idempotencyKey: string;
  }): Promise<GuestImportResultDto>;
}

const ISSUE_KEYS: Record<string, MessageKey> = {
  invalid_phone: 'op.players.import.issue.invalidPhone',
  missing_name: 'op.players.import.issue.missingName',
  negative_amount: 'op.players.import.issue.negativeAmount',
  duplicate_in_file: 'op.players.import.issue.duplicateInFile',
  already_imported: 'op.players.import.issue.alreadyImported'
};

/**
 * Перенос гостей из прежней программы: файл выгрузки → проверка без записи → перенос. Деньги
 * переносятся начальными остатками; гостю, которому уже переносили, второй раз не перенесётся.
 */
export function GuestImportModal({
  client,
  organizationId,
  currencyCode,
  onClose,
  onImported
}: {
  client: GuestImportClient;
  organizationId: string;
  currencyCode: string;
  onClose: () => void;
  onImported: () => void;
}) {
  const { t } = useI18n();
  const [source, setSource] = useState('');
  const [parsed, setParsed] = useState<ParsedGuestFile | null>(null);
  const [preview, setPreview] = useState<GuestImportResultDto | null>(null);
  const [done, setDone] = useState<GuestImportResultDto | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  // Один ключ на один файл: повтор после обрыва связи не перенесёт второй раз.
  const idempotencyKey = useMemo(() => crypto.randomUUID(), [parsed]);

  const readFile = async (event: ChangeEvent<HTMLInputElement>) => {
    const file = event.currentTarget.files?.[0];
    if (!file) return;
    setPreview(null);
    setDone(null);
    setError(null);
    setParsed(parseGuestFile(await file.text()));
  };

  const run = async (dryRun: boolean) => {
    if (parsed === null || parsed.rows.length === 0) return;
    setBusy(true);
    setError(null);
    try {
      const result = await client.importGuests({
        organizationId, currencyCode, source: source.trim(), rows: parsed.rows, dryRun, idempotencyKey
      });
      if (dryRun) setPreview(result);
      else { setDone(result); onImported(); }
    } catch (failure) {
      setError(projectOperatorError(failure, t).detail);
    } finally {
      setBusy(false);
    }
  };

  const summary = done ?? preview;
  return (
    <PanelModal title={t('op.players.import.title')} subtitle={t('op.players.import.subtitle')} onClose={onClose} closeDisabled={busy}>
      <div className="mgmt-form guest-import">
        <p className="mgmt-drawer-hint">{t('op.players.import.format')}</p>
        <div className="mgmt-form-grid">
          <label>{t('op.players.import.source')}
            <input value={source} maxLength={60} placeholder="SmartShell" disabled={busy || done !== null} onChange={(event) => setSource(event.currentTarget.value)} />
          </label>
          <label>{t('op.players.import.file')}
            <input type="file" accept=".csv,.txt,text/csv,text/plain" disabled={busy || done !== null} onChange={(event) => void readFile(event)} />
          </label>
        </div>

        {parsed !== null ? (
          <p className="guest-import-read">
            {t('op.players.import.read', { count: parsed.rows.length })}
            {parsed.unreadable.length > 0 ? ` ${t('op.players.import.unreadable', { rows: parsed.unreadable.slice(0, 10).join(', ') })}` : ''}
          </p>
        ) : null}

        {summary !== null ? (
          <div className="guest-import-summary" role="status">
            <strong>{t(done ? 'op.players.import.done' : 'op.players.import.preview')}</strong>
            <ul>
              <li>{t('op.players.import.created', { count: summary.created })}</li>
              <li>{t('op.players.import.matched', { count: summary.matched })}</li>
              <li>{t('op.players.import.skipped', { count: summary.skipped })}</li>
              <li>{t('op.players.import.balance')} <Money minorUnits={summary.balanceTotal.minorUnits} currencyCode={summary.balanceTotal.currencyCode} /></li>
              <li>{t('op.players.import.bonus')} <Money minorUnits={summary.bonusTotal.minorUnits} currencyCode={summary.bonusTotal.currencyCode} /></li>
            </ul>
            {summary.issues.length > 0 ? (
              <ul className="guest-import-issues">
                {summary.issues.slice(0, 20).map((issue) => (
                  <li key={`${issue.row}-${issue.code}`}>{t('op.players.import.issueRow', { row: issue.row })} {t(ISSUE_KEYS[issue.code] ?? 'op.players.import.issue.invalidPhone')}</li>
                ))}
              </ul>
            ) : null}
          </div>
        ) : null}

        {error && <p className="ui-inline-error" role="alert">{error}</p>}

        <div className="mgmt-form-actions">
          <button type="button" className="ui-btn" disabled={busy} onClick={onClose}>{t(done ? 'common.close' : 'common.cancel')}</button>
          {done === null ? (
            preview === null ? (
              <button type="button" className="ui-btn ui-btn--primary" disabled={busy || parsed === null || parsed.rows.length === 0} onClick={() => void run(true)}>
                {t('op.players.import.check')}
              </button>
            ) : (
              <button type="button" className="ui-btn ui-btn--primary" disabled={busy || preview.created + preview.matched === 0} onClick={() => void run(false)}>
                {t('op.players.import.commit', { count: preview.created + preview.matched })}
              </button>
            )
          ) : null}
        </div>
      </div>
    </PanelModal>
  );
}
