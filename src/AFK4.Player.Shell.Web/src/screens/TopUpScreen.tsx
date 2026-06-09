import { useEffect, useRef, useState } from 'react';
import QRCode from 'qrcode';
import type { PlayerTopUpIntentDto } from '../apiTypes';
import type { ShellApi } from '../shellApi';
import { toPaymentStatus, type PaymentStatus } from '../paymentStatus';

export interface TopUpScreenProps {
  api: ShellApi;
  amountMinorUnits: number;
  pollIntervalMs?: number;
}

export function TopUpScreen({ api, amountMinorUnits, pollIntervalMs = 3000 }: TopUpScreenProps) {
  const [intent, setIntent] = useState<PlayerTopUpIntentDto | null>(null);
  const [status, setStatus] = useState<PaymentStatus>('pending');
  const [qr, setQr] = useState<string | null>(null);
  const [offline, setOffline] = useState(false);
  const created = useRef(false);

  useEffect(() => {
    if (created.current) return;
    created.current = true;
    api.createTopUpIntent(amountMinorUnits)
      .then(setIntent)
      .catch(() => setOffline(true));
  }, [api, amountMinorUnits]);

  useEffect(() => {
    if (!intent?.payUrl) return;
    QRCode.toDataURL(intent.payUrl).then(setQr).catch(() => setQr(null));
  }, [intent?.payUrl]);

  useEffect(() => {
    if (!intent || status !== 'pending') return;
    const timer = setInterval(async () => {
      try {
        const all = await api.getTopUpIntents();
        const mine = all.find((i) => i.paymentIntentId === intent.paymentIntentId);
        if (mine) setStatus(toPaymentStatus(mine));
      } catch { setOffline(true); }
    }, pollIntervalMs);
    return () => clearInterval(timer);
  }, [api, intent, status, pollIntervalMs]);

  if (offline) return <p role="alert">Временно недоступно — обратитесь к оператору</p>;
  if (!intent) return <p>Создаём платёж…</p>;

  if (status === 'fulfilled') return <p>Оплата успешно зачислена</p>;
  if (status === 'expired') return <p role="alert">Срок истёк — начните заново</p>;
  if (status === 'disputed') return <p role="alert">Платёж на проверке — обратитесь к оператору</p>;

  return (
    <section>
      <h1>Пополнение</h1>
      {qr && <img data-testid="topup-qr" src={qr} alt="QR" />}
      <p>Комментарий: <strong>{intent.comment}</strong></p>
      <p>Ожидаем подтверждение оплаты…</p>
    </section>
  );
}
