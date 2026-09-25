import { SessionTraceNames } from '@afk4/contracts';
import type {
  BlockedWindowRuleDto,
  BranchProtectionProfileDto,
  UpdateBranchProtectionProfileRequest
} from '@afk4/contracts';

/** Что стирается после сессии, в том порядке, в каком пункты стоят на странице. */
export const sessionTraces = [
  SessionTraceNames.Steam,
  SessionTraceNames.Browsers,
  SessionTraceNames.Launchers,
  SessionTraceNames.Messengers
] as const;

/**
 * Форма профиля защиты. Списки — текстом, строка на пункт: адресов и окон у клуба десяток, и
 * поле с построчным вводом понятнее редактора строк с кнопками «добавить».
 */
export interface ProtectionForm {
  version: number;
  blockRemovableStorage: boolean;
  blockBrowserDownloads: boolean;
  blockBrowserIncognito: boolean;
  disableRunDialog: boolean;
  hiddenDrives: string[];
  urlBlocklist: string;
  blockedTitles: string;
  blockedClasses: string;
  clearAfterSession: string[];
}

/** Диски, которые предлагаем скрыть. A и B — дисководы, которых давно нет. */
export const hideableDrives = 'CDEFGHIJKLMNOPQRSTUVWXYZ'.split('');

export const protectionDefaults: ProtectionForm = {
  version: 0,
  blockRemovableStorage: false,
  blockBrowserDownloads: false,
  blockBrowserIncognito: false,
  disableRunDialog: false,
  hiddenDrives: [],
  urlBlocklist: '',
  blockedTitles: '',
  blockedClasses: '',
  // Как на сервере без профиля: следующий игрок не входит в чужой Steam потому, что клуб не
  // открыл эту страницу.
  clearAfterSession: [...sessionTraces]
};

export function protectionToForm(dto: BranchProtectionProfileDto): ProtectionForm {
  const profile = dto.profile;
  return {
    version: profile.version,
    blockRemovableStorage: profile.blockRemovableStorage,
    blockBrowserDownloads: profile.blockBrowserDownloads,
    blockBrowserIncognito: profile.blockBrowserIncognito,
    disableRunDialog: profile.disableRunDialog,
    hiddenDrives: [...profile.hiddenDrives],
    urlBlocklist: profile.urlBlocklist.join('\n'),
    // Панель пишет правила по одному признаку; правило с обоими признаками (например, из API)
    // показываем заголовком — класс в нём сузил бы совпадение, а не расширил.
    blockedTitles: profile.blockedWindows.filter((rule) => rule.titleContains).map((rule) => rule.titleContains).join('\n'),
    blockedClasses: profile.blockedWindows.filter((rule) => !rule.titleContains && rule.className).map((rule) => rule.className).join('\n'),
    clearAfterSession: [...profile.clearAfterSession]
  };
}

export function lines(text: string): string[] {
  return text.split('\n').map((line) => line.trim()).filter((line) => line.length > 0);
}

export function buildProtectionRequest(organizationId: string, form: ProtectionForm): UpdateBranchProtectionProfileRequest {
  const windows: BlockedWindowRuleDto[] = [
    ...lines(form.blockedTitles).map((title) => ({ titleContains: title, className: null })),
    ...lines(form.blockedClasses).map((className) => ({ titleContains: null, className }))
  ];
  return {
    organizationId,
    expectedVersion: form.version,
    blockRemovableStorage: form.blockRemovableStorage,
    blockBrowserDownloads: form.blockBrowserDownloads,
    blockBrowserIncognito: form.blockBrowserIncognito,
    disableRunDialog: form.disableRunDialog,
    hiddenDrives: [...form.hiddenDrives].sort(),
    urlBlocklist: lines(form.urlBlocklist),
    blockedWindows: windows,
    clearAfterSession: sessionTraces.filter((item) => form.clearAfterSession.includes(item))
  };
}

export function toggleTrace(items: string[], item: string, on: boolean): string[] {
  const next = on ? [...items, item] : items.filter((current) => current !== item);
  return sessionTraces.filter((trace) => next.includes(trace));
}

export function toggleDrive(drives: string[], drive: string): string[] {
  return drives.includes(drive) ? drives.filter((item) => item !== drive) : [...drives, drive].sort();
}
