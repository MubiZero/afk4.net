import { useRef, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { PackageOptionDto, PlayerPackageDto } from './operatorApiClients';
import type { OperatorBackendContext } from './operatorTypes';
import type { PlayerClientItem } from './operatorHelpers';
import { createAuthenticatedOperatorClients, createIdempotencyKey, formatMinorUnits } from './operatorHelpers';
import { hasPermission, permissionNames } from './operatorPermissions';
import { PlatformApiError } from './platformApi';
import { projectOperatorError } from './apiErrors';
import { SkeletonControl } from './LoadingSkeleton';

type PurchasePackage = ReturnType<typeof createAuthenticatedOperatorClients>['players']['purchasePackage'];

export function PackagePurchasePanel({ backend, player, options, shiftOpen, onPurchased, purchasePackage }: {
  backend: OperatorBackendContext;
  player: PlayerClientItem & { playerAccountId: string };
  options: PackageOptionDto[];
  // null — неизвестно: запрос смены не ответил. Тогда продавать нельзя, но и говорить
  // «откройте смену» тоже нельзя — причину называет тот, кто спрашивал.
  shiftOpen: boolean | null;
  onPurchased: (result: PlayerPackageDto) => void | Promise<void>;
  purchasePackage?: PurchasePackage;
}) {
  const { t } = useI18n();
  const [selectedId, setSelectedId] = useState(options[0]?.packageDefinitionId ?? '');
  const [busy, setBusy] = useState(false);
  const [errorDetail, setErrorDetail] = useState<string | null>(null);
  const [purchasedName, setPurchasedName] = useState<string | null>(null);
  const [refreshFailure, setRefreshFailure] = useState<string | null>(null);
  const attemptKeyRef = useRef<string | null>(null);
  const selected = options.find((option) => option.packageDefinitionId === selectedId) ?? options[0] ?? null;
  const allowed = shiftOpen === true && player.isActive && selected !== null && hasPermission(backend.session, permissionNames.purchasePackage);

  const submit = async () => {
    if (!allowed || selected === null || busy) return;
    setBusy(true);
    setErrorDetail(null);
    setPurchasedName(null);
    setRefreshFailure(null);
    const key = attemptKeyRef.current ?? createIdempotencyKey('package-purchase');
    attemptKeyRef.current = key;
    let result: PlayerPackageDto;
    try {
      const call = purchasePackage ?? createAuthenticatedOperatorClients(backend.config, backend.session).players.purchasePackage;
      result = await call(player.playerAccountId, {
        organizationId: backend.session.organizationId,
        packageDefinitionId: selected.packageDefinitionId,
        idempotencyKey: key
      });
    } catch (error) {
      if (error instanceof PlatformApiError && error.status >= 400 && error.status < 500 && error.status !== 409) attemptKeyRef.current = null;
      setErrorDetail(projectOperatorError(error, t).detail);
      setBusy(false);
      return;
    }
    // Покупка прошла, деньги списаны. Всё, что дальше, — обновление экрана, и его отказ не должен
    // выглядеть отказом покупки: ключ попытки уже сброшен, и кассир, поверив красной строке,
    // купил бы пакет второй раз.
    attemptKeyRef.current = null;
    setPurchasedName(result.name);
    try {
      await onPurchased(result);
    } catch (error) {
      setRefreshFailure(projectOperatorError(error, t).detail);
    } finally {
      setBusy(false);
    }
  };

  return <section className="pos-package-purchase">
    <strong>{t('op.pos.packages.title')}</strong>
    <select className="ui-select" aria-label={t('op.pos.packages.selectLabel')} value={selected?.packageDefinitionId ?? ''} disabled={busy || options.length === 0} onChange={(event) => setSelectedId(event.currentTarget.value)}>
      {options.map((option) => <option key={option.packageDefinitionId} value={option.packageDefinitionId}>{option.name} · {formatMinorUnits(option.priceMinorUnits, option.currencyCode)}</option>)}
    </select>
    {shiftOpen === false && <p>{t('op.pos.packages.shiftRequired')}</p>}
    {errorDetail && <p role="alert">{errorDetail}</p>}
    {purchasedName !== null && <p role="status">{refreshFailure === null
      ? t('op.pos.packages.purchased', { name: purchasedName })
      : t('op.pos.packages.purchasedRefreshFailed', { name: purchasedName, reason: refreshFailure })}</p>}
    <button type="button" className="ui-btn ui-btn--primary" disabled={!allowed || busy} onClick={() => void submit()}>{t('op.pos.packages.purchase')}</button>
  </section>;
}

// Та же панель, пока варианты пакетов в пути: заголовок от ответа не зависит и стоит настоящим
// текстом, выбор пакета и кнопка покупки — полосами высоты контрола, в той же строке, что и
// настоящая панель (.pos-package-purchase в 11-pos.css).
export function PackagePurchaseSkeleton() {
  const { t } = useI18n();
  return (
    <section className="pos-package-purchase" data-skeleton="form" aria-hidden="true">
      <strong>{t('op.pos.packages.title')}</strong>
      <SkeletonControl width="8rem" size="sm" />
      <SkeletonControl width="6rem" />
    </section>
  );
}
