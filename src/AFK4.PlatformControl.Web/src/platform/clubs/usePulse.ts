import type { PulseApi } from '@/api/platformClients/pulse';
import type { PlatformPulse } from '@/api/types';
import { useLoadable, type Loadable } from '../useLoadable';

export type PulseState = Loadable<PlatformPulse>;

type Client = Pick<PulseApi, 'getPulse'>;

/// Обзор сети — дежурный экран: его держат открытым и по нему решают, куда бежать. Минута —
/// компромисс между свежестью и нагрузкой: тревоги пульса (молчащий агент, застрявшая смена)
/// живут десятками минут, и чаще смотреть незачем.
const PULSE_REFRESH_MS = 60_000;

export function usePulse(client: Client): PulseState {
  return useLoadable(() => client.getPulse(), [], { refreshMs: PULSE_REFRESH_MS });
}
