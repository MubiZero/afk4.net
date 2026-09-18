import { describe, expect, it } from 'bun:test';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { PIN_LENGTH, isWellFormedPin, keepPinDigits } from './pinPolicy';

const csharp = (relativePath: string): string =>
  readFileSync(resolve(import.meta.dir, '../../../src', relativePath), 'utf8');

describe('ПИН-код входа', () => {
  it('длиной ровно в то, что проверяет сервер', () => {
    const match = csharp('AFK4.Shared.Contracts/Identity/PinContracts.cs').match(
      /public const int Length\s*=\s*(?<length>\d+)/,
    );

    expect(match).not.toBeNull();
    expect(Number(match!.groups!.length)).toBe(PIN_LENGTH);
  });

  // Раньше порогов было три — у сотрудника, у владельца клуба и у администратора платформы, — и
  // совпадали они только на бумаге. Теперь каждая из этих дверей обязана спрашивать PinFormat.
  it.each([
    ['AFK4.Platform.Api/Endpoints/EndpointHelpers.Validation.cs', 'сотрудник клуба'],
    ['AFK4.Platform.Api/Platform/Tenancy/EfPlatformOrganizationService.cs', 'владелец клуба'],
    ['AFK4.Platform.Api/Platform/Identity/PlatformAdminDirectoryService.cs', 'администратор платформы'],
  ])('%s не заводит своего порога (%s)', (path) => {
    const source = csharp(path);

    expect(source).toContain('PinFormat.IsWellFormed');
    expect(source).not.toMatch(/MinP(assword|in)Length|MinimumStaffPasswordLength/);
  });

  it('принимает шесть цифр и отвергает всё остальное', () => {
    expect(isWellFormedPin('123456')).toBe(true);
    expect(isWellFormedPin('12345')).toBe(false);
    expect(isWellFormedPin('1234567')).toBe(false);
    expect(isWellFormedPin('12345a')).toBe(false);
    expect(isWellFormedPin('1234 6')).toBe(false);
    expect(isWellFormedPin('')).toBe(false);
    // Арабо-индийские цифры выглядят цифрами, но сервер их не примет — и поле не должно.
    expect(isWellFormedPin('٦٦٦٦٦٦')).toBe(false);
  });

  it('оставляет в поле только цифры и только шесть', () => {
    expect(keepPinDigits('12ab34cd56ef78')).toBe('123456');
    expect(keepPinDigits('Passw0rd!')).toBe('0');
    expect(keepPinDigits('1 2 3')).toBe('123');
    expect(keepPinDigits('٦٦٦')).toBe('');
  });
});
