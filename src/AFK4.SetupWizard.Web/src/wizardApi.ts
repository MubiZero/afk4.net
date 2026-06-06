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

export interface WizardPhoneSignInResult {
  displayName: string;
}

export interface WizardSeatDraft {
  branchId: string;
  zoneId: string;
  zoneName: string;
  name: string;
}

export interface WizardEnrollDraft {
  branchId: string;
  seatId: string | null;
  role: WizardRole;
  displayName: string;
}

/** Install operations bound to an authentication mode. The owner-code path threads
 *  the code into every payload; the authenticated path carries no code — the bearer
 *  token is held by the native host after `signInByPhone`. */
export interface WizardInstallClient {
  createSeat(draft: WizardSeatDraft): Promise<WizardSeat>;
  enrollDevice(draft: WizardEnrollDraft): Promise<WizardEnrollResult>;
}

export function discoverOwner(ownerCode: string): Promise<WizardDiscoverResponse> {
  return postHostRequest<WizardDiscoverResponse>('wizard:discover', { ownerCode });
}

export function signInByPhone(phone: string, password: string): Promise<WizardPhoneSignInResult> {
  return postHostRequest<WizardPhoneSignInResult>('wizard:phoneSignIn', { phone, password });
}

export function discoverAuthenticated(): Promise<WizardDiscoverResponse> {
  return postHostRequest<WizardDiscoverResponse>('wizard:discoverAuth');
}

export function ownerCodeInstallClient(ownerCode: string): WizardInstallClient {
  return {
    createSeat: (draft) =>
      postHostRequest<WizardSeat>('wizard:createSeat', { ownerCode, ...draft }),
    enrollDevice: (draft) =>
      postHostRequest<WizardEnrollResult>('wizard:enroll', { ownerCode, ...draft }),
  };
}

export function authenticatedInstallClient(): WizardInstallClient {
  return {
    createSeat: (draft) => postHostRequest<WizardSeat>('wizard:createSeatAuth', draft),
    enrollDevice: (draft) => postHostRequest<WizardEnrollResult>('wizard:enrollAuth', draft),
  };
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
