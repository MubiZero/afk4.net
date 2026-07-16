import { useEffect, useMemo, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import { createAuthenticatedOperatorClients } from './operatorHelpers';
import { projectOperatorError } from './apiErrors';
import type { OperatorBackendContext } from './operatorTypes';
import type { LoyaltySettingsDto } from './operatorApiClients';

interface LoyaltySettingsClient {
  get(): Promise<LoyaltySettingsDto>;
  update(request: LoyaltySettingsDto): Promise<LoyaltySettingsDto>;
}

function toBasisPoints(percent: string): number | null {
  const value = Number(percent);
  if (!Number.isFinite(value) || value < 0 || value > 100) {
    return null;
  }
  return Math.round(value * 100);
}

export function LoyaltySettingsWorkspace({
  backend,
  client: injectedClient
}: {
  backend: OperatorBackendContext | null;
  client?: LoyaltySettingsClient;
}) {
  const { t } = useI18n();
  const memoizedClient = useMemo(
    () => (backend ? createAuthenticatedOperatorClients(backend.config, backend.session).loyaltySettings : null),
    [backend?.config, backend?.session]
  );
  const client = injectedClient ?? memoizedClient;
  const [topUpEnabled, setTopUpEnabled] = useState(false);
  const [topUpPercent, setTopUpPercent] = useState('0');
  const [shopEnabled, setShopEnabled] = useState(false);
  const [shopPercent, setShopPercent] = useState('0');
  const [error, setError] = useState<string | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [saved, setSaved] = useState(false);
  const [ready, setReady] = useState(false);

  useEffect(() => {
    if (client === null) {
      return undefined;
    }
    let active = true;
    client.get()
      .then((settings) => {
        if (!active) {
          return;
        }
        setTopUpEnabled(settings.topUpEnabled);
        setTopUpPercent(String(settings.topUpPercentBasisPoints / 100));
        setShopEnabled(settings.shopEnabled);
        setShopPercent(String(settings.shopPercentBasisPoints / 100));
        setReady(true);
      })
      .catch((cause) => {
        if (active) {
          setLoadError(projectOperatorError(cause, t).detail);
        }
      });
    return () => {
      active = false;
    };
  }, [client]);

  const save = async () => {
    if (client === null) {
      return;
    }
    setSaved(false);
    const topUpBps = toBasisPoints(topUpPercent);
    const shopBps = toBasisPoints(shopPercent);
    if (topUpBps === null || shopBps === null) {
      setError(t('op.loyalty.percentError'));
      return;
    }
    setError(null);
    await client.update({
      topUpEnabled,
      topUpPercentBasisPoints: topUpBps,
      shopEnabled,
      shopPercentBasisPoints: shopBps
    });
    setSaved(true);
  };

  if (loadError) {
    return (
      <>
        <h2>{t('op.loyalty.title')}</h2>
        <p className="workspace-error" role="alert">{loadError}</p>
      </>
    );
  }

  if (!ready) {
    return (
      <>
        <h2>{t('op.loyalty.title')}</h2>
        <p className="workspace-loading">{t('op.loyalty.loading')}</p>
      </>
    );
  }

  return (
    <>
      {/* h2, not h1 — ManagementScreen already owns the page's single h1 ("Лояльность") now
          that this mounts inside it; this keeps the more descriptive legacy title ("Лояльность
          / кэшбэк") without a second competing top-level heading. */}
      <h2>{t('op.loyalty.title')}</h2>
      <div className="settings-form-grid">
        <label>
          {t('op.loyalty.topUpEnabled')}
          <input type="checkbox" checked={topUpEnabled} onChange={(event) => setTopUpEnabled(event.target.checked)} />
        </label>
        <label>
          {t('op.loyalty.topUpPercent')}
          <input type="number" value={topUpPercent} onChange={(event) => setTopUpPercent(event.target.value)} />
        </label>
        <label>
          {t('op.loyalty.shopEnabled')}
          <input type="checkbox" checked={shopEnabled} onChange={(event) => setShopEnabled(event.target.checked)} />
        </label>
        <label>
          {t('op.loyalty.shopPercent')}
          <input type="number" value={shopPercent} onChange={(event) => setShopPercent(event.target.value)} />
        </label>
        {error && <p className="settings-form-wide" role="alert">{error}</p>}
        {saved && <p className="settings-form-wide">{t('op.loyalty.saved')}</p>}
        <div className="settings-form-wide news-form-actions">
          <button type="button" className="ui-btn ui-btn--primary" onClick={() => void save()}>{t('op.loyalty.save')}</button>
        </div>
      </div>
    </>
  );
}
