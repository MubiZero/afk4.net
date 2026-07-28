import { useRef, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { PackageOptionDto, PlayerPackageDto } from './operatorApiClients';
import type { OperatorBackendContext } from './operatorTypes';
import type { PlayerClientItem } from './operatorHelpers';
import { createAuthenticatedOperatorClients, createIdempotencyKey, formatMinorUnits } from './operatorHelpers';
import { hasPermission, permissionNames } from './operatorPermissions';
import { PlatformApiError } from './platformApi';
import { projectOperatorError } from './apiErrors';

type PurchasePackage = ReturnType<typeof createAuthenticatedOperatorClients>['players']['purchasePackage'];

export function PackagePurchasePanel({ backend, player, options, shiftOpen, onPurchased, purchasePackage }: {
  backend: OperatorBackendContext;
  player: PlayerClientItem & { playerAccountId: string };
  options: PackageOptionDto[];
  shiftOpen: boolean;
  onPurchased: (result: PlayerPackageDto) => void | Promise<void>;
  purchasePackage?: PurchasePackage;
}) {
  const { t } = useI18n();
  const [selectedId, setSelectedId] = useState(options[0]?.packageDefinitionId ?? '');
  const [busy, setBusy] = useState(false);
  const [errorDetail, setErrorDetail] = useState<string | null>(null);
  const attemptKeyRef = useRef<string | null>(null);
  const selected = options.find((option) => option.packageDefinitionId === selectedId) ?? options[0] ?? null;
  const allowed = shiftOpen && player.isActive && selected !== null && hasPermission(backend.session, permissionNames.purchasePackage);

  const submit = async () => {
    if (!allowed || selected === null || busy) return;
    setBusy(true);
    setErrorDetail(null);
    const key = attemptKeyRef.current ?? createIdempotencyKey('package-purchase');
    attemptKeyRef.current = key;
    try {
      const call = purchasePackage ?? createAuthenticatedOperatorClients(backend.config, backend.session).players.purchasePackage;
      const result = await call(player.playerAccountId, {
        organizationId: backend.session.organizationId,
        packageDefinitionId: selected.packageDefinitionId,
        idempotencyKey: key
      });
      attemptKeyRef.current = null;
      await onPurchased(result);
    } catch (error) {
      if (error instanceof PlatformApiError && error.status >= 400 && error.status < 500 && error.status !== 409) attemptKeyRef.current = null;
      setErrorDetail(projectOperatorError(error, t).detail);
    } finally {
      setBusy(false);
    }
  };

  return <section className="pos-package-purchase">
    <strong>{t('op.pos.packages.title')}</strong>
    <select aria-label={t('op.pos.packages.selectLabel')} value={selected?.packageDefinitionId ?? ''} disabled={busy || options.length === 0} onChange={(event) => setSelectedId(event.currentTarget.value)}>
      {options.map((option) => <option key={option.packageDefinitionId} value={option.packageDefinitionId}>{option.name} · {formatMinorUnits(option.priceMinorUnits, option.currencyCode)}</option>)}
    </select>
    {!shiftOpen && <p>{t('op.pos.packages.shiftRequired')}</p>}
    {errorDetail && <p role="alert">{errorDetail}</p>}
    <button type="button" className="ui-btn ui-btn--primary" disabled={!allowed || busy} onClick={() => void submit()}>{t('op.pos.packages.purchase')}</button>
  </section>;
}
