import type { OperatorBackendContext } from '../operatorTypes';
import type { OperatorAuthSession } from '../authClient';

export function StockWorkspace(_props: {
  currencyCode: string;
  backend: OperatorBackendContext | null;
  session: OperatorAuthSession | null;
}) {
  return <main className="workspace-screen stock-screen" />;
}
