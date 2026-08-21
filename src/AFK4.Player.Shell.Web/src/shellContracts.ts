// Hand-mirrored from AFK4.Shared.Contracts/Shell. No codegen exists; keep in sync.

// Values are lowercase to match the C# source of truth
// (AFK4.Shared.Contracts/Shell/PlayerShellStateNames.cs), which is what
// arrives over the wire — NOT the PascalCase member names.
export const PlayerShellStateNames = {
  Locked: 'locked',
  Active: 'active',
  Grace: 'grace',
  Ending: 'ending',
  Maintenance: 'maintenance',
  Offline: 'offline',
  Error: 'error'
} as const;

export type PlayerShellStateName = (typeof PlayerShellStateNames)[keyof typeof PlayerShellStateNames];

export interface LauncherApp {
  appId: string;
  displayName: string;
  category: string;
  iconUri: string | null;
  isAvailable: boolean;
}

export interface PlayerShellState {
  organizationId: string;
  branchId: string;
  deviceId: string;
  state: PlayerShellStateName;
  sessionId: string | null;
  leaseExpiresAtUtc: string | null;
  remainingSeconds: number | null;
  isOnline: boolean;
  isGraceMode: boolean;
  warningThresholdSeconds: number;
  message: string;
  launcherApps: LauncherApp[];
  locale: string;
  warningKind: string;
  /**
   * Код с этого монитора: человек набирает его в приложении и садится именно за эту машину.
   * Пусто у занятой машины и при потере связи — показать старый код значит позвать человека
   * туда, куда сервер его не пустит.
   */
  seatingCode?: string | null;
}
