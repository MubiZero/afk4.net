import { describe, expect, it } from 'bun:test';
import { installerFileName, parseDeviceCount, silentInstallCommand } from './installCodesModel';

describe('installCodesModel', () => {
  // Команда пишется с тем же именем файла, что скачается по ссылке: иначе техник вставит её в
  // скрипт, и скрипт не найдёт установщик.
  it('takes the installer file name from its link', () => {
    expect(installerFileName('https://dl.afk4.net/client/afk4-client-1.4.0-stable.exe')).toBe('afk4-client-1.4.0-stable.exe');
    expect(installerFileName('https://dl.afk4.net/client/latest')).toBe('afk4-client.exe');
    expect(installerFileName('не ссылка')).toBe('afk4-client.exe');
    expect(installerFileName(null)).toBe('afk4-client.exe');
  });

  it('builds the quiet install command with the code', () => {
    expect(silentInstallCommand('afk4-client.exe', '7KQ2-M9XD-4TPV-HB3R'))
      .toBe('afk4-client.exe /quiet AFK4_INSTALL_CODE=7KQ2-M9XD-4TPV-HB3R');
  });

  it('accepts only a whole number of PCs within the limits', () => {
    expect(parseDeviceCount('30')).toBe(30);
    expect(parseDeviceCount(' 200 ')).toBe(200);
    expect(parseDeviceCount('0')).toBeNull();
    expect(parseDeviceCount('201')).toBeNull();
    expect(parseDeviceCount('2.5')).toBeNull();
    expect(parseDeviceCount('')).toBeNull();
  });
});
