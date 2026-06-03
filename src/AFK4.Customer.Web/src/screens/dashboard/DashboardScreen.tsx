// TEMP STUB — replaced in Task 7 with the real dashboard + live session card.
import type { PlayerApiClient } from '@/api/playerApi';

export function DashboardScreen({ api, displayName }: { api: PlayerApiClient; displayName: string }) {
  void api;
  return <div>{displayName}</div>;
}
