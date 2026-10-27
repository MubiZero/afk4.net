import { useEffect, useState } from 'react';
import { HardDriveDownload } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { Button } from '@afk4/ui/react';
import { projectOperatorError } from '../../../apiErrors';
import { useToast } from '../../../operatorToast';
import type { BranchGameCacheDto, createGamesClient } from '../../../api/clients/games';

const PRIVATE_IPV4 = /^(10\.\d{1,3}|172\.(1[6-9]|2\d|3[01])|192\.168)\.\d{1,3}\.\d{1,3}$/;

/**
 * Кеш игр клуба (docs/operations/game-cache-lancache.md): адрес LANCache в сети клуба. ПК качают Steam и
 * Epic через него — игра приходит из интернета один раз на клуб. Агенты сами правят hosts и сообщают,
 * видят ли кеш; карточка показывает, со скольких ПК.
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

  const trimmed = address.trim();
  const valid = trimmed === '' || PRIVATE_IPV4.test(trimmed);
  const dirty = trimmed !== (cache?.address ?? '');

  const save = async () => {
    setSaving(true);
    setError(null);
    try {
      const saved = await client.setCache(branchId, { address: trimmed === '' ? null : trimmed });
      setCache(saved);
      setAddress(saved.address ?? '');
      toast.success(t(saved.address ? 'op.games.cache.saved' : 'op.games.cache.cleared'));
    } catch (failure) {
      setError(projectOperatorError(failure, t).detail);
    } finally {
      setSaving(false);
    }
  };

  return (
    <section className="games-cache" aria-label={t('op.games.cache.title')}>
      <div className="mgmt-section-title"><span><HardDriveDownload aria-hidden="true" size={14} /> {t('op.games.cache.title')}</span></div>
      <p className="ui-field-hint">{t('op.games.cache.hint')}</p>
      {cache?.address && (
        <p className="games-cache-status" role="status">
          {cache.devicesReporting === 0
            ? t('op.games.cache.checking')
            : t('op.games.cache.seenBy', { seeing: cache.devicesSeeing, reporting: cache.devicesReporting })}
        </p>
      )}
      {canManage && (
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
          <Button variant="primary" disabled={saving || !valid || !dirty} onClick={() => void save()}>{t('op.games.cache.save')}</Button>
        </div>
      )}
      {!valid && <p className="offline-seat-error" role="alert">{t('op.games.cache.invalid')}</p>}
      {error !== null && <p className="offline-seat-error" role="alert">{error}</p>}
    </section>
  );
}
