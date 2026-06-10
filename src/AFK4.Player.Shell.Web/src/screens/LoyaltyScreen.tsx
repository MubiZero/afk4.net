import { useEffect, useState } from 'react';
import type { ShellApi } from '../shellApi';
import type { PlayerLoyaltyDto } from '../apiTypes';

function formatMoney(minorUnits: number, currencyCode: string): string {
  return `${(minorUnits / 100).toFixed(2)} ${currencyCode}`;
}

function formatPercent(basisPoints: number): string {
  return `${(basisPoints / 100).toFixed(basisPoints % 100 === 0 ? 0 : 2)}%`;
}

export function LoyaltyScreen({ api, onDone }: { api: ShellApi; onDone: () => void }) {
  const [data, setData] = useState<PlayerLoyaltyDto | null>(null);
  const [error, setError] = useState(false);

  useEffect(() => {
    let active = true;
    api.getLoyalty().then(
      (d) => { if (active) setData(d); },
      () => { if (active) setError(true); }
    );
    return () => { active = false; };
  }, [api]);

  if (error) {
    return (
      <section>
        <h2>Кэшбэк</h2>
        <p>Не удалось загрузить лояльность. Попробуйте позже.</p>
        <button type="button" onClick={onDone}>Назад</button>
      </section>
    );
  }

  if (!data) {
    return <section><h2>Кэшбэк</h2><p>Загрузка…</p></section>;
  }

  const anyEnabled = data.topUpEnabled || data.shopEnabled;

  return (
    <section>
      <h2>Кэшбэк</h2>
      {!anyEnabled && <p>Кэшбэк пока недоступен в этом клубе.</p>}
      {anyEnabled && (
        <>
          <p>Кэшбэк падает прямо в кошелёк и тратится как обычные деньги.</p>
          <ul>
            {data.topUpEnabled && <li>Пополнение: {formatPercent(data.topUpPercentBasisPoints)} кэшбэка</li>}
            {data.shopEnabled && <li>Магазин: {formatPercent(data.shopPercentBasisPoints)} кэшбэка</li>}
          </ul>
        </>
      )}
      <p>Всего начислено: <strong>{formatMoney(data.totalEarned.minorUnits, data.totalEarned.currencyCode)}</strong></p>
      {data.recent.length > 0 && (
        <ul>
          {data.recent.map((entry, index) => (
            <li key={index}>{formatMoney(entry.amountMinorUnits, entry.currencyCode)} — {new Date(entry.createdAtUtc).toLocaleDateString('ru-RU')}</li>
          ))}
        </ul>
      )}
      <button type="button" onClick={onDone}>Назад</button>
    </section>
  );
}
