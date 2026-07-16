import type { OperatorAuthSession } from '../../authClient';
import type { OperatorBackendContext } from '../../operatorTypes';

// Shared prop contract every «Управление» destination wrapper implements. ManagementWorkspace
// mounts exactly one of these at a time and only reads `onDirtyChange` back — the destination
// owns its own load/save state.
export interface DestinationProps {
  backend: OperatorBackendContext | null;
  session: OperatorAuthSession | null;
  currencyCode: string;
  onDirtyChange?: (dirty: boolean) => void;
}
