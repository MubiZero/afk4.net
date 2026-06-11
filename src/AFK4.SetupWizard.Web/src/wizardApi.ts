import { postHostRequest, postHostWindowCommand } from './hostBridge';

// Enroll + shell provisioning run msiexec /i synchronously on the host, which can take several
// minutes on a fresh PC. Give these commands a long timeout so the JS promise doesn't reject (and
// the user retry, duplicate-enrolling) while the install is still running.
const ENROLL_TIMEOUT_MS = 300_000;

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
  shell: WizardShellOutcome;
}

export interface WizardShellOutcome {
  status: 'installed' | 'already_present' | 'skipped' | 'failed';
  exitCode: number | null;
  message: string | null;
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

/** Install operations carrying no credentials in the payload — the bearer token is held
 *  by the native host after `signInByPhone` / `signInByLogin`. */
export interface WizardInstallClient {
  createSeat(draft: WizardSeatDraft): Promise<WizardSeat>;
  enrollDevice(draft: WizardEnrollDraft): Promise<WizardEnrollResult>;
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

export function authenticatedInstallClient(): WizardInstallClient {
  return {
    createSeat: (draft) => postHostRequest<WizardSeat>('wizard:createSeatAuth', draft),
    enrollDevice: (draft) =>
      postHostRequest<WizardEnrollResult>('wizard:enrollAuth', draft, ENROLL_TIMEOUT_MS),
  };
}

/** Retry installing the role's app (Player Shell for gaming_pc, Operator App for
 *  manager_workstation) after a failed attempt. */
export function provisionShell(role: WizardRole): Promise<WizardShellOutcome> {
  return postHostRequest<WizardShellOutcome>('wizard:provisionShell', { role }, ENROLL_TIMEOUT_MS);
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
