import type { ReactNode } from 'react';
import { formatMoney } from '@afk4/money';
import { cx } from './cx';

// Сумма денег. Правило записи одно на продукт (@afk4/money): 1200 TJS → «12 с.», целые без дробей.
// `signed` — движение, а не остаток: «+12 с.» зелёным, «−12 с.» красным, с настоящим минусом, а не
// дефисом. `null` — сумма неизвестна (себестоимость не заведена): прочерк, а не «0 с.», который
// читается как «бесплатно».
export function Money({ minorUnits, currencyCode, signed = false, locale = 'ru-RU', className }: {
  minorUnits: number | null;
  currencyCode: string;
  signed?: boolean;
  locale?: string;
  className?: string;
}) {
  if (minorUnits === null) {
    return <span className={cx('ui-money', 'ui-money--muted', className)}>—</span>;
  }
  if (!signed) {
    return <span className={cx('ui-money', className)}>{formatMoney(minorUnits, currencyCode, locale)}</span>;
  }
  const positive = minorUnits >= 0;
  return (
    <span className={cx('ui-money', positive ? 'ui-money--pos' : 'ui-money--neg', className)}>
      {positive ? '+' : '−'}{formatMoney(Math.abs(minorUnits), currencyCode, locale)}
    </span>
  );
}

// Любое другое число — количество, минуты, время, «3 из 10»: основным шрифтом с цифрами одной
// ширины, чтобы колонка стояла ровно, а таймер не дрожал. Форматирует вызывающий — у него локаль
// и единицы.
export function Num({ children, className }: { children: ReactNode; className?: string }) {
  return <span className={cx('ui-num', className)}>{children}</span>;
}
