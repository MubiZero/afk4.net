import { useEffect, useState } from 'react';
import { HardDriveDownload } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { Button } from '@afk4/ui/react';
import { projectOperatorError } from '../../../apiErrors';
import { useToast } from '../../../operatorToast';
import type { BranchGameCacheDto, createGamesClient } from '../../../api/clients/games';

const PRIVATE_IPV4 = /^(10\.\d{1,3}|172\.(1[6-9]|2\d|3[01])|192\.168)\.\d{1,3}\.\d{1,3}$/;
const GIB = 1024 ** 3;

/**
 * Кеш игр клуба (docs/operations/game-cache-lancache.md): по умолчанию встроен в агента ПК администратора —
 * клубу ничего не ставить и не настраивать. Steam и Epic качают игру из интернета один раз на клуб.
 * Карточка показывает, где кеш и со скольких ПК он виден; можно выключить или указать свой сервер LANCache.
 */
export function GameCacheCard({
  client,
  branchId,
  canManage
}: {
  client: ReturnType<typeof createGamesClient>;
  branchId: string;
  canManage: boolean;
}) {
  const { t } = useI18n();
  const toast = useToast();
  const [cache, setCache] = useState<BranchGameCacheDto | null>(null);
  const [address, setAddress] = useState('');
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let disposed = false;
    client.cache(branchId)
      .then((loaded) => { if (!disposed) { setCache(loaded); setAddress(loaded.address ?? ''); } })
      .catch(() => { /* Без статуса карточка просто пустая: библиотека игр ниже работает и без неё. */ });
    return () => { disposed = true; };
  }, [client, branchId]);

  if (cache === null) return null;

  const enabled = cache.enabled ?? true;
  const trimmed = address.trim();
  const valid = trimmed === '' || PRIVATE_IPV4.test(trimmed);
  const dirty = trimmed !== (cache.address ?? '');

  const save = async (next: { address: string | null; enabled: boolean }, message: Parameters<typeof t>[0]) => {
    setSaving(true);
    setError(null);
    try {
      const saved = await client.setCache(branchId, next);
      setCache(saved);
      setAddress(saved.address ?? '');
      toast.success(t(message));
    } catch (failure) {
      setError(projectOperatorError(failure, t).detail);
    } finally {
      setSaving(false);
    }
  };

  const where = !enabled
    ? t('op.games.cache.off')
    : cache.address
      ? t('op.games.cache.ownServer', { address: cache.address })
      : cache.hostName
        ? t('op.games.cache.builtIn', { name: cache.hostName, size: ((cache.storedBytes ?? 0) / GIB).toFixed(1) })
        : t('op.games.cache.noHost');

  return (
    <section className="games-cache" aria-label={t('op.games.cache.title')}>
      <div className="mgmt-section-title"><span><HardDriveDownload aria-hidden="true" size={14} /> {t('op.games.cache.title')}</span></div>
      <p className="ui-field-hint">{t('op.games.cache.hint')}</p>
      <p className="games-cache-status" role="status">
        {where}
        {enabled && cache.effectiveAddress && cache.devicesReporting > 0 && ` · ${t('op.games.cache.seenBy', { seeing: cache.devicesSeeing, reporting: cache.devicesReporting })}`}
      </p>
      {canManage && (
        <>
          <label className="games-cache-toggle">
            <input
              type="checkbox"
              checked={enabled}
              disabled={saving}
              onChange={(event) => void save(
                { address: cache.address ?? null, enabled: event.currentTarget.checked },
                event.currentTarget.checked ? 'op.games.cache.turnedOn' : 'op.games.cache.cleared'
              )}
            />
            {t('op.games.cache.enabled')}
          </label>
          {enabled && (
            <details className="games-cache-advanced" open={cache.address !== null && cache.address !== undefined}>
              <summary>{t('op.games.cache.advanced')}</summary>
              <p className="ui-field-hint">{t('op.games.cache.ownServerHint')}</p>
              <div className="games-cache-form">
                <label htmlFor="games-cache-address">{t('op.games.cache.addressLabel')}</label>
                <input
                  id="games-cache-address"
                  inputMode="decimal"
                  placeholder="192.168.1.10"
                  value={address}
                  disabled={saving}
                  aria-invalid={!valid}
                  onChange={(event) => setAddress(event.currentTarget.value)}
                />
                <Button
                  variant="primary"
                  disabled={saving || !valid || !dirty}
                  onClick={() => void save({ address: trimmed === '' ? null : trimmed, enabled: true }, 'op.games.cache.saved')}
                >
                  {t('op.games.cache.save')}
                </Button>
              </div>
              {!valid && <p className="offline-seat-error" role="alert">{t('op.games.cache.invalid')}</p>}
            </details>
          )}
        </>
      )}
      {error !== null && <p className="offline-seat-error" role="alert">{error}</p>}
    </section>
  );
}
