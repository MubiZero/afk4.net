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
  hasTariff: boolean;
  hasStaffBesidesOwner: boolean;
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
  brandingConfigured: boolean;
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
  /// 'agent_start_failed' — приложение встало, а служба AFK4 не запустилась: машина
  /// зарегистрирована и настроена, но на связь не выйдет.
  status: 'installed' | 'already_present' | 'skipped' | 'failed' | 'agent_start_failed';
  exitCode: number | null;
  message: string | null;
  /// Киоск на игровом ПК (спека оболочки, §6.1). Нет поля — киоск не ставили: другая роль или
  /// приложение не встало.
  kiosk?: WizardKioskOutcome;
}

export interface WizardKioskOutcome {
  /// ready — после перезагрузки Windows сама войдёт в учётку игрока; failed — не вышло.
  status: 'ready' | 'failed';
  message: string | null;
}

export interface WizardKioskStatus {
  installed: boolean;
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

export interface WizardBrandingPreset {
  id: string;
  url: string;
}

/** Готовые эмблемы: картинки лежат на платформе, сюда приходят их адреса. */
export function brandingPresets(): Promise<{ presets: WizardBrandingPreset[] }> {
  return postHostRequest<{ presets: WizardBrandingPreset[] }>('wizard:brandingPresets');
}

/** Свой логотип: окно выбора файла открывает нативный хост, сюда возвращается адрес загруженного. */
export function uploadLogo(branchId: string): Promise<{ logoUrl?: string | null }> {
  return postHostRequest<{ logoUrl?: string | null }>('wizard:uploadLogo', { branchId });
}

export function saveBranding(logoUrl: string | null, accentColor: string | null): Promise<{ saved: boolean }> {
  return postHostRequest<{ saved: boolean }>('wizard:saveBranding', { logoUrl, accentColor });
}

export interface WizardStaffInvited {
  displayName: string;
  roleName: string;
  code: string;
  expiresAtUtc: string;
}

/** Приглашение сотрудника: код уходит ему в SMS, пароль он задаёт себе сам. */
export function inviteStaff(
  branchId: string,
  displayName: string,
  phoneNumber: string,
  roleName: string,
): Promise<WizardStaffInvited> {
  return postHostRequest<WizardStaffInvited>('wizard:inviteStaff', { branchId, displayName, phoneNumber, roleName });
}

/** Зал пачкой: «ПК-1»…«ПК-N» в выбранной зоне. */
export function createSeats(
  branchId: string,
  zoneId: string,
  namePrefix: string,
  count: number,
): Promise<{ names: string[] }> {
  return postHostRequest<{ names: string[] }>('wizard:createSeats', { branchId, zoneId, namePrefix, count });
}

export function createTariff(
  branchId: string,
  name: string,
  pricePerHourMinorUnits: number,
): Promise<{ name: string }> {
  return postHostRequest<{ name: string }>('wizard:createTariff', { branchId, name, pricePerHourMinorUnits });
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

/** Retry installing the role's app (Player Shell for gaming_pc, Organization Admin for
 *  manager_workstation) after a failed attempt. */
export function provisionShell(role: WizardRole): Promise<WizardShellOutcome> {
  return postHostRequest<WizardShellOutcome>('wizard:provisionShell', { role }, ENROLL_TIMEOUT_MS);
}

/** Стоит ли на этом ПК киоск — от этого зависит, показывать ли «Снять киоск». */
export function kioskStatus(): Promise<WizardKioskStatus> {
  return postHostRequest<WizardKioskStatus>('wizard:kioskStatus', {});
}

/** «Снять киоск»: учётка игрока, автовход и подмена проводника уходят; агент перезапускается. */
export function removeKiosk(): Promise<WizardKioskStatus> {
  return postHostRequest<WizardKioskStatus>('wizard:removeKiosk', {}, ENROLL_TIMEOUT_MS);
}

/** Перезагрузить ПК: автовход срабатывает только при запуске Windows. */
export function rebootPc(): Promise<void> {
  return postHostRequest<void>('wizard:reboot', {});
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
