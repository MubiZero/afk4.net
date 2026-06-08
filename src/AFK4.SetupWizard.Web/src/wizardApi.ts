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

export interface WizardClubChoice {
  organizationId: string;
  name: string;
}

export interface WizardLoginResult {
  displayName: string | null;
  requiresClubChoice: boolean;
  clubs: WizardClubChoice[];
}

/** Sign in by email or username. When the same login matches several clubs the result carries
 *  `requiresClubChoice` + `clubs`; the caller picks one and re-submits via {@link signInToClub}. */
export function signInByLogin(login: string, password: string): Promise<WizardLoginResult> {
  return postHostRequest<WizardLoginResult>('wizard:signInByLogin', { login, password });
}

export function signInToClub(
  organizationId: string,
  login: string,
  password: string,
): Promise<WizardPhoneSignInResult> {
  return postHostRequest<WizardPhoneSignInResult>('wizard:signInToClub', { organizationId, login, password });
}

export function discoverAuthenticated(): Promise<WizardDiscoverResponse> {
  return postHostRequest<WizardDiscoverResponse>('wizard:discoverAuth');
}

/** Email channel, step 1: the backend emails a 6-digit code. */
export function forgotPasswordByEmail(userNameOrEmail: string): Promise<void> {
  return postHostRequest<void>('wizard:forgotByEmail', { userNameOrEmail });
}

/** Email channel, step 2: complete the reset inline with the emailed code + new password. */
export function resetPasswordByEmail(
  userNameOrEmail: string,
  code: string,
  newPassword: string,
): Promise<void> {
  return postHostRequest<void>('wizard:resetByEmail', { userNameOrEmail, code, newPassword });
}

/** Phone channel, step 1: the backend texts a one-time code. */
export function forgotPasswordByPhone(phoneNumber: string): Promise<void> {
  return postHostRequest<void>('wizard:forgotByPhone', { phoneNumber });
}

/** Phone channel, step 2: complete the reset inline with the SMS code + new password. */
export function resetPasswordByPhone(
  phoneNumber: string,
  code: string,
  newPassword: string,
): Promise<void> {
  return postHostRequest<void>('wizard:resetByPhone', { phoneNumber, code, newPassword });
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
