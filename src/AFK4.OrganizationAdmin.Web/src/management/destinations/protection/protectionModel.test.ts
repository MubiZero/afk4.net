import { describe, expect, it } from 'bun:test';
import { buildProtectionRequest, lines, protectionDefaults, protectionToForm, toggleDrive, toggleTrace } from './protectionModel';

const dto = {
  organizationId: 'o1',
  branchId: 'b1',
  profile: {
    version: 4,
    blockRemovableStorage: true,
    blockBrowserDownloads: false,
    blockBrowserIncognito: true,
    disableRunDialog: false,
    hiddenDrives: ['D', 'E'],
    urlBlocklist: ['casino.example', '*.bet.example'],
    blockedWindows: [
      { titleContains: 'Командная строка', className: null },
      { titleContains: null, className: 'ConsoleWindowClass' }
    ],
    clearAfterSession: ['steam', 'messengers']
  },
  updatedAtUtc: '2026-09-25T10:00:00Z'
};

describe('protectionModel', () => {
  it('round-trips the profile through the form, keeping the version it was opened at', () => {
    const request = buildProtectionRequest('o1', protectionToForm(dto));

    expect(request).toEqual({
      organizationId: 'o1',
      expectedVersion: 4,
      blockRemovableStorage: true,
      blockBrowserDownloads: false,
      blockBrowserIncognito: true,
      disableRunDialog: false,
      hiddenDrives: ['D', 'E'],
      urlBlocklist: ['casino.example', '*.bet.example'],
      blockedWindows: [
        { titleContains: 'Командная строка', className: null },
        { titleContains: null, className: 'ConsoleWindowClass' }
      ],
      clearAfterSession: ['steam', 'messengers']
    });
  });

  // Клуб, который страницу не открывал, всё равно стирает следы — как агент без профиля.
  it('clears every trace by default', () => {
    expect(protectionDefaults.clearAfterSession).toEqual(['steam', 'browsers', 'launchers', 'messengers']);
  });

  // Порядок пунктов — порядок каталога: иначе щелчки туда-обратно давали бы новую версию профиля.
  it('toggles a trace and keeps the catalog order', () => {
    expect(toggleTrace(['messengers'], 'steam', true)).toEqual(['steam', 'messengers']);
    expect(toggleTrace(['steam', 'messengers'], 'steam', false)).toEqual(['messengers']);
  });

  // Пустые строки и пробелы по краям — след ввода, а не адреса.
  it('reads one entry per line, ignoring blanks and edge spaces', () => {
    expect(lines('  casino.example \n\n *.bet.example\n   ')).toEqual(['casino.example', '*.bet.example']);
  });

  it('toggles a drive letter and keeps the letters in order', () => {
    expect(toggleDrive(['E'], 'D')).toEqual(['D', 'E']);
    expect(toggleDrive(['D', 'E'], 'D')).toEqual(['E']);
  });
});
