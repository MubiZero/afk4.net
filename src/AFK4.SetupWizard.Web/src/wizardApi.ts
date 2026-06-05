import { postHostRequest, postHostWindowCommand } from './hostBridge';

export interface WizardBranch {
  branchId: string;
  branchSlug: string;
  branchName: string;
  zones: WizardZone[];
  seats: WizardSeat[];
  freeSeatIds: string[];
}

export interface WizardZone {
  zoneId: string;
  name: string;
  sortOrder: number;
}

export interface WizardSeat {
  seatId: string;
  pcName: string;
  zoneId: string;
  zoneName: string;
  sortOrder: number;
  status: string;
  deviceId: string | null;
  deviceName: string | null;
  isOnline: boolean | null;
}

export interface WizardDiscoverResponse {
  ownerName: string;
  branches: WizardBranch[];
}

export interface WizardEnrollResult {
  organizationId: string;
  branchId: string;
  deviceId: string;
  role: WizardRole;
  displayName: string;
  machineName: string;
  enrollmentState: string;
  apiBaseUrl: string;
  updateChannel: string;
}

export type WizardRole = 'gaming_pc' | 'manager_workstation';

export interface WizardCreateSeatRequest {
  ownerCode: string;
  branchId: string;
  zoneId: string;
  zoneName: string;
  name: string;
}

export interface WizardEnrollRequest {
  ownerCode: string;
  branchId: string;
  seatId: string | null;
  role: WizardRole;
  displayName: string;
}

export function discoverOwner(ownerCode: string): Promise<WizardDiscoverResponse> {
  return postHostRequest<WizardDiscoverResponse>('wizard:discover', { ownerCode });
}

export function createSeat(request: WizardCreateSeatRequest): Promise<WizardSeat> {
  return postHostRequest<WizardSeat>('wizard:createSeat', request);
}

export function enrollDevice(request: WizardEnrollRequest): Promise<WizardEnrollResult> {
  return postHostRequest<WizardEnrollResult>('wizard:enroll', request);
}

export function closeWizard(): void {
  postHostWindowCommand('close');
}

export interface WizardBootstrapConfig {
  runtime: 'webview2';
  shellMode: string;
  machineName: string;
  isPreview: boolean;
  platformBaseUrl: string;
}

export function getBootstrapConfig(): WizardBootstrapConfig | null {
  return window.__AFK4_SETUP_WIZARD_CONFIG__ ?? null;
}

declare global {
  interface Window {
    __AFK4_SETUP_WIZARD_CONFIG__?: WizardBootstrapConfig;
  }
}
