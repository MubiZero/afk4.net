import { useCallback, useEffect, useState } from 'react';
import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { Check, Copy } from 'lucide-react';
import { EmptyState, PartialLoadFailure, Skeleton } from '../../operatorPrimitives';
import { projectOperatorError } from '../../apiErrors';
import type { InstallCodeDto } from '../../api/clients/installCodes';
import type { createAuthenticatedOperatorClients } from '../../operatorHelpers';
import {
  DEFAULT_INSTALL_CODE_DEVICES,
  INSTALL_CODE_LIFETIMES,
  MAX_INSTALL_CODE_DEVICES,
  installerFileName,
  parseDeviceCount,
  silentInstallCommand
} from './installCodesModel';

type Clients = ReturnType<typeof createAuthenticatedOperatorClients>;

type CodesState =
  | { status: 'loading' }
  | { status: 'ready'; codes: InstallCodeDto[] }
  | { status: 'failed'; error: unknown };

/**
 * Тихая установка на весь зал (план P5f-2): код вместо мастера на каждом ПК. Сам код виден один
 * раз — сразу после выдачи; в списке остаются срок и счёт поставленных ПК.
 */
export function InstallCodesSection({
  clients,
  branches,
  preferredBranchId,
  installerUrl
}: {
  clients: Clients | null;
  branches: { branchId: string; name: string }[];
  preferredBranchId: string | null;
  installerUrl: string | null;
}): JSX.Element {
  const { t, formatDate } = useI18n();
  const [branchId, setBranchId] = useState<string | null>(null);
  const [lifetimeHours, setLifetimeHours] = useState(INSTALL_CODE_LIFETIMES[0].hours);
  const [deviceCount, setDeviceCount] = useState(String(DEFAULT_INSTALL_CODE_DEVICES));
  const [issued, setIssued] = useState<InstallCodeDto | null>(null);
  const [codes, setCodes] = useState<CodesState>({ status: 'loading' });
  const [busy, setBusy] = useState(false);
  const [actionError, setActionError] = useState<string | null>(null);
  const [copied, setCopied] = useState<'code' | 'command' | null>(null);

  // Филиал по умолчанию — тот, в котором открыта Панель; нет его в списке — первый.
  useEffect(() => {
    if (branchId !== null || branches.length === 0) return;
    setBranchId(branches.some((branch) => branch.branchId === preferredBranchId) ? preferredBranchId : branches[0].branchId);
  }, [branches, branchId, preferredBranchId]);

  const load = useCallback(async () => {
    if (clients === null || branchId === null) return;
    setCodes({ status: 'loading' });
    try {
      setCodes({ status: 'ready', codes: await clients.installCodes.list(branchId) });
    } catch (error) {
      setCodes({ status: 'failed', error });
    }
  }, [clients, branchId]);

  useEffect(() => { void load(); }, [load]);

  const devices = parseDeviceCount(deviceCount);

  const issue = async () => {
    if (clients === null || branchId === null || devices === null) return;
    setBusy(true);
    setActionError(null);
    try {
      setIssued(await clients.installCodes.issue(branchId, { lifetimeHours, maxDevices: devices }));
      await load();
    } catch (error) {
      setActionError(projectOperatorError(error, t).detail);
    } finally {
      setBusy(false);
    }
  };

  const revoke = async (code: InstallCodeDto) => {
    if (clients === null || branchId === null) return;
    setBusy(true);
    setActionError(null);
    try {
      await clients.installCodes.revoke(branchId, code.installCodeId);
      if (issued?.installCodeId === code.installCodeId) setIssued(null);
      await load();
    } catch (error) {
      setActionError(projectOperatorError(error, t).detail);
    } finally {
      setBusy(false);
    }
  };

  const copy = (what: 'code' | 'command', text: string) => {
    // Буфер обмена бывает закрыт — тогда текст остаётся выделяемым, и его копируют руками.
    navigator.clipboard?.writeText(text).then(
      () => {
        setCopied(what);
        setTimeout(() => setCopied(null), 1500);
      },
      () => undefined
    );
  };

  const selectBranch = (next: string) => {
    setBranchId(next);
    setIssued(null);
    setActionError(null);
  };

  const command = issued?.code ? silentInstallCommand(installerFileName(installerUrl), issued.code) : null;
  const loadFailure = codes.status === 'failed' ? projectOperatorError(codes.error, t) : null;

  return (
    <section className="management-panel network-install-codes">
      <h3>{t('op.network.install.codes.title')}</h3>
      <p className="network-install-codes-lead">{t('op.network.install.codes.lead')}</p>

      {branches.length === 0 ? (
        <EmptyState
          inline
          className="network-install-branches-empty"
          title={t('op.network.install.branches.empty')}
          next={{ kind: 'elsewhere', hint: t('op.network.branches.add.viaPlatform') }}
        />
      ) : (
        <>
          <div className="mgmt-form network-install-codes-form">
            <label>
              {t('op.network.install.codes.branch')}
              <select value={branchId ?? ''} disabled={busy} onChange={(event) => selectBranch(event.target.value)}>
                {branches.map((branch) => (
                  <option key={branch.branchId} value={branch.branchId}>{branch.name}</option>
                ))}
              </select>
            </label>
            <label>
              {t('op.network.install.codes.lifetime')}
              <select value={lifetimeHours} disabled={busy} onChange={(event) => setLifetimeHours(Number(event.target.value))}>
                {INSTALL_CODE_LIFETIMES.map((option) => (
                  <option key={option.hours} value={option.hours}>{t(option.labelKey)}</option>
                ))}
              </select>
            </label>
            <label>
              {t('op.network.install.codes.devices')}
              <input
                type="number"
                inputMode="numeric"
                min={1}
                max={MAX_INSTALL_CODE_DEVICES}
                value={deviceCount}
                disabled={busy}
                aria-invalid={devices === null}
                onChange={(event) => setDeviceCount(event.target.value)}
              />
            </label>
            <div className="mgmt-form-actions">
              <button
                type="button"
                className="ui-btn ui-btn--primary"
                disabled={busy || branchId === null || devices === null}
                onClick={() => void issue()}
              >
                {t('op.network.install.codes.issue')}
              </button>
            </div>
          </div>
          {devices === null && (
            <p className="network-install-codes-hint">
              {t('op.network.install.codes.devicesRange', { max: MAX_INSTALL_CODE_DEVICES })}
            </p>
          )}

          {actionError !== null && <p className="ui-alert ui-alert--spaced" role="alert">{actionError}</p>}

          {issued?.code && command !== null && (
            <div className="network-install-codes-issued" aria-live="polite">
              <p className="network-install-codes-once">{t('op.network.install.codes.shownOnce')}</p>
              <div className="network-install-codes-copy">
                <code className="network-install-codes-value">{issued.code}</code>
                <button type="button" className="ui-btn ui-btn--sm" onClick={() => copy('code', issued.code!)}>
                  {copied === 'code' ? <Check size={13} aria-hidden="true" /> : <Copy size={13} aria-hidden="true" />}
                  {copied === 'code' ? t('op.network.install.codes.copied') : t('op.network.install.codes.copyCode')}
                </button>
              </div>
              <span className="network-install-codes-label">{t('op.network.install.codes.command')}</span>
              <div className="network-install-codes-copy">
                <code className="network-install-codes-command">{command}</code>
                <button type="button" className="ui-btn ui-btn--sm" onClick={() => copy('command', command)}>
                  {copied === 'command' ? <Check size={13} aria-hidden="true" /> : <Copy size={13} aria-hidden="true" />}
                  {copied === 'command' ? t('op.network.install.codes.copied') : t('op.network.install.codes.copyCommand')}
                </button>
              </div>
              <p className="network-install-codes-hint">{t('op.network.install.codes.seatHint')}</p>
              <p className="network-install-codes-hint">{t('op.network.install.codes.afterHint')}</p>
            </div>
          )}

          <h4 className="network-install-codes-subtitle">{t('op.network.install.codes.active')}</h4>
          {codes.status === 'loading' && <Skeleton variant="text" lines={2} />}
          {loadFailure !== null && (
            <PartialLoadFailure
              text={t('op.network.install.codes.failed', { reason: loadFailure.detail })}
              failure={loadFailure}
              onRetry={() => void load()}
            />
          )}
          {codes.status === 'ready' && codes.codes.length === 0 && (
            <EmptyState
              inline
              className="network-install-codes-hint"
              title={t('op.network.install.codes.empty')}
              next={{ kind: 'calm', hint: t('op.network.install.codes.emptyHint') }}
            />
          )}
          {codes.status === 'ready' && codes.codes.length > 0 && (
            <ul className="network-install-codes-list">
              {codes.codes.map((code) => (
                <li key={code.installCodeId}>
                  <span>
                    {t('op.network.install.codes.row', {
                      expires: formatDate(code.expiresAtUtc),
                      used: code.usedDevices,
                      max: code.maxDevices
                    })}
                  </span>
                  <button type="button" className="ui-btn ui-btn--sm" disabled={busy} onClick={() => void revoke(code)}>
                    {t('op.network.install.codes.revoke')}
                  </button>
                </li>
              ))}
            </ul>
          )}
        </>
      )}
    </section>
  );
}
