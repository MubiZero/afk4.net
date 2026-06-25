import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';

// Заглушка — реализация в Task 6/7. Здесь только каркас с правильной сигнатурой пропсов.
export function StockLevelsWorkspace(_props: {
  backend: OperatorBackendContext | null;
  currencyCode: string;
  session: OperatorAuthSession | null;
}) {
  return <section className="stock-levels-placeholder" />;
}
