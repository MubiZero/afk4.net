import { useEffect, useMemo, useState } from 'react';
import QRCode from 'qrcode';
import { useI18n } from '@afk4/i18n';
import type { StaffTelegramLinkDto, StaffTelegramStatusDto } from '@afk4/contracts';
import { authenticatedPlatformApi, formatDateTime } from './operatorHelpers';
import { createOperatorApiClients } from './operatorApiClients';
import { DeferredSkeleton, SkeletonLine } from './LoadingSkeleton';
import type { PhoneVerificationBackend } from './PhoneVerificationCard';

/** Пока на экране QR, Панель спрашивает, привязал ли человек чат, — чтобы «Подключено» появилось само. */
const LINK_POLL_MS = 3000;

/**
 * «Отчёты в Telegram»: итоги смен, сводка за день и тревоги кассы приходят в чат с ботом AFK4.NET.
 * QR, а не кнопка «Открыть»: Панель стоит на ПК у стойки, а Telegram у владельца — в телефоне.
 */
export function TelegramReportsCard({ backend }: { backend: PhoneVerificationBackend }) {
  const { t } = useI18n();
  const api = useMemo(
    () => createOperatorApiClients(
      authenticatedPlatformApi(backend.config.platformBaseUrl, backend.session),
      backend.session.organizationId).account,
    [backend.config.platformBaseUrl, backend.session.accessToken, backend.session.organizationId]
  );

  const [status, setStatus] = useState<StaffTelegramStatusDto | null>(null);
  const [loadFailed, setLoadFailed] = useState(false);
  const [link, setLink] = useState<StaffTelegramLinkDto | null>(null);
  const [qr, setQr] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [reloadKey, setReloadKey] = useState(0);

  useEffect(() => {
    let disposed = false;
    setLoadFailed(false);
    api.getMyTelegram()
      .then((next) => { if (!disposed) setStatus(next); })
      .catch(() => { if (!disposed) setLoadFailed(true); });
    return () => { disposed = true; };
  }, [api, reloadKey]);

  useEffect(() => {
    if (link === null) return undefined;
    let disposed = false;
    void QRCode.toDataURL(link.deepLink, { margin: 1, width: 176 }).then((url) => { if (!disposed) setQr(url); });
    const timer = window.setInterval(() => {
      if (Date.parse(link.expiresAtUtc) <= Date.now()) {
        setLink(null);
        return;
      }
      void api.getMyTelegram().then((next) => {
        if (disposed || !next.linked) return;
        setStatus(next);
        setLink(null);
      }).catch(() => undefined);
    }, LINK_POLL_MS);
    return () => { disposed = true; window.clearInterval(timer); };
  }, [api, link]);

  // Без бота у платформы подключать нечего — карточку не показываем вовсе.
  if (status !== null && !status.botConfigured) return null;

  const connect = async () => {
    setBusy(true);
    setError(null);
    try {
      setQr(null);
      setLink(await api.createTelegramLink());
    } catch {
      setError(t('account.telegram.err.generic'));
    } finally {
      setBusy(false);
    }
  };

  const disconnect = async () => {
    setBusy(true);
    setError(null);
    try {
      await api.unlinkTelegram();
      setStatus((current) => current && { ...current, linked: false, linkedAtUtc: null });
    } catch {
      setError(t('account.telegram.err.generic'));
    } finally {
      setBusy(false);
    }
  };

  return (
    <section className="account-phone account-telegram">
      <h3>{t('account.telegram.title')}</h3>
      {error !== null && <p className="account-phone-error" role="alert">{error}</p>}

      {status === null && !loadFailed && (
        <DeferredSkeleton>
          <div className="account-phone-verified" aria-hidden="true">
            <strong><SkeletonLine width="9em" /></strong>
            <button type="button" className="secondary" tabIndex={-1}><SkeletonLine width="5em" /></button>
          </div>
        </DeferredSkeleton>
      )}

      {loadFailed && (
        <div className="account-phone-load-error">
          <p className="account-phone-error">{t('account.telegram.err.generic')}</p>
          <button type="button" onClick={() => setReloadKey((k) => k + 1)}>{t('state.retry')}</button>
        </div>
      )}

      {status?.linked === true && (
        <div className="account-phone-verified">
          <strong>{t('account.telegram.linked', { since: formatDateTime(status.linkedAtUtc) })}</strong>
          <button type="button" className="secondary" disabled={busy} onClick={() => void disconnect()}>
            {t('account.telegram.disconnect')}
          </button>
        </div>
      )}

      {status?.linked === false && link === null && (
        <div className="account-phone-form">
          <p className="account-phone-hint">{t('account.telegram.hint')}</p>
          <button type="button" disabled={busy} onClick={() => void connect()}>{t('account.telegram.connect')}</button>
        </div>
      )}

      {status?.linked === false && link !== null && (
        <div className="account-telegram-qr">
          {qr === null
            ? <div className="account-telegram-qr-image" aria-hidden="true" />
            : <img className="account-telegram-qr-image" src={qr} alt={t('account.telegram.qrAlt')} />}
          <div className="account-telegram-qr-text">
            <p className="account-phone-hint">{t('account.telegram.scan')}</p>
            <a href={link.deepLink} target="_blank" rel="noreferrer">{t('account.telegram.openLink')}</a>
          </div>
        </div>
      )}
    </section>
  );
}
