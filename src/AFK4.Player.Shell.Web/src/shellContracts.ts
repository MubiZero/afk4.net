// Hand-mirrored from AFK4.Shared.Contracts/Shell. No codegen exists; keep in sync.

export const PlayerShellStateNames = {
  Locked: 'Locked',
  Active: 'Active',
  Grace: 'Grace',
  Ending: 'Ending',
  Maintenance: 'Maintenance',
  Offline: 'Offline',
  Error: 'Error'
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
}
