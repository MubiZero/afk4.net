import { describe, expect, it } from 'bun:test';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { MIN_PLATFORM_ADMIN_PASSWORD_LENGTH, MIN_STAFF_PASSWORD_LENGTH } from './passwordPolicy';

// Экран, который требует больше сервера, зря не пускает человека; экран, который требует
// меньше, отправляет его за отказом. Число читается из самого сервера.
describe('минимальная длина пароля сотрудника', () => {
  it('совпадает с тем, что проверяет сервер', () => {
    const source = readFileSync(
      resolve(import.meta.dir, '../../../src/AFK4.Platform.Api/Endpoints/EndpointHelpers.Validation.cs'),
      'utf8',
    );
    const match = source.match(/MinimumStaffPasswordLength\s*=\s*(?<min>\d+)/);

    expect(match).not.toBeNull();
    expect(Number(match!.groups!.min)).toBe(MIN_STAFF_PASSWORD_LENGTH);
  });

  it('у администратора платформы своё, и оно строже', () => {
    const source = readFileSync(
      resolve(
        import.meta.dir,
        '../../../src/AFK4.Platform.Api/Platform/Identity/PlatformAdminDirectoryService.cs',
      ),
      'utf8',
    );
    const match = source.match(/MinPasswordLength\s*=\s*(?<min>\d+)/);

    expect(match).not.toBeNull();
    expect(Number(match!.groups!.min)).toBe(MIN_PLATFORM_ADMIN_PASSWORD_LENGTH);
    expect(MIN_PLATFORM_ADMIN_PASSWORD_LENGTH).toBeGreaterThan(MIN_STAFF_PASSWORD_LENGTH);
  });
});
