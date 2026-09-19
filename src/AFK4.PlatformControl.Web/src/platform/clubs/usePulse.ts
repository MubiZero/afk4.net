import type { PulseApi } from '@/api/platformClients/pulse';
import type { PlatformPulse } from '@/api/types';
import { useLoadable, type Loadable } from '../useLoadable';

export type PulseState = Loadable<PlatformPulse>;

type Client = Pick<PulseApi, 'getPulse'>;

export function usePulse(client: Client): PulseState {
  return useLoadable(() => client.getPulse());
}
