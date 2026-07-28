import type { ReactNode } from 'react';

export type StockHeroTone = 'neutral' | 'ok' | 'warning' | 'attention' | 'muted';

export function StockHero({
  label,
  value,
  sub,
  tone,
}: {
  label: string;
  value: ReactNode;
  sub?: ReactNode;
  tone: StockHeroTone;
}) {
  return (
    <div className={`stock-hero stock-hero--${tone}`}>
      <span className="stock-hero-label">{label}</span>
      <strong className="stock-hero-value">{value}</strong>
      {sub !== undefined && sub !== null && <span className="stock-hero-sub">{sub}</span>}
    </div>
  );
}
