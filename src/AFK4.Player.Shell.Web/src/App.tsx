import { ActiveSessionScreen } from './screens/ActiveSessionScreen';
import { LockedScreen } from './screens/LockedScreen';
import { PlayerShellStateNames } from './shellContracts';
import { useShellBridge } from './useShellBridge';

export function App() {
  const { state, launch, requestOperator } = useShellBridge();

  const locked =
    state === null ||
    state.state === PlayerShellStateNames.Locked ||
    state.state === PlayerShellStateNames.Offline ||
    state.state === PlayerShellStateNames.Error;

  if (locked) {
    return <LockedScreen state={state} onRequestOperator={requestOperator} />;
  }

  return <ActiveSessionScreen state={state} onLaunch={launch} onRequestOperator={requestOperator} />;
}
