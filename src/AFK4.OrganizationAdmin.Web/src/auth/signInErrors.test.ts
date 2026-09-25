import { describe, expect, it } from 'bun:test';
import type { MessageKey } from '@afk4/i18n';
import { projectFirstSignInError, projectNextStepError, projectPinSignInError } from './signInErrors';
import { StaffAuthApiError } from './staffAuthApi';

// Ключ вместо текста: проверяется выбор слов, а не сами слова.
const t = (key: MessageKey, values?: Record<string, string | number>) =>
  values ? `${key}(${JSON.stringify(values)})` : key;

describe('projectPinSignInError', () => {
  it('blames only the PIN once the number is known, and login or PIN otherwise', () => {
    expect(projectPinSignInError(new StaffAuthApiError(401, null), 'phone', t)).toBe('op.auth.error.wrongPin');
    expect(projectPinSignInError(new StaffAuthApiError(401, null), 'login', t)).toBe('auth.error.invalid');
  });

  it('tells a locked account apart from an address that asked too often', () => {
    expect(projectPinSignInError(new StaffAuthApiError(429, { code: 'too_many_password_attempts' }), 'phone', t)).toBe('auth.error.lockedOut');
    expect(projectPinSignInError(new StaffAuthApiError(429, null), 'phone', t)).toBe('op.auth.error.tooFast');
  });

  it('names another club’s number on a connected Panel', () => {
    expect(projectPinSignInError(new StaffAuthApiError(403, null), 'phone', t)).toBe('op.auth.error.otherClub');
  });

  it('keeps the generic words for a failure it cannot name', () => {
    expect(projectPinSignInError(new TypeError('Failed to fetch'), 'phone', t)).toBe('auth.error.generic');
  });
});

describe('projectNextStepError', () => {
  it('sends a dead code and an unknown number to the manager with different words', () => {
    expect(projectNextStepError('invite-expired', t)).toBe('op.auth.firstSignIn.expired');
    expect(projectNextStepError('unknown', t)).toBe('op.auth.phone.unknown');
  });
});

describe('projectFirstSignInError', () => {
  it('counts the tries left, and calls the last miss a dead code', () => {
    expect(projectFirstSignInError(new StaffAuthApiError(400, { error: 'invalid_code', remainingAttempts: 2 }), t))
      .toBe('op.auth.firstSignIn.wrongCode({"count":2})');
    expect(projectFirstSignInError(new StaffAuthApiError(400, { error: 'invalid_code', remainingAttempts: 0 }), t))
      .toBe('op.auth.firstSignIn.expired');
  });

  it('treats an expired and a spent code alike', () => {
    expect(projectFirstSignInError(new StaffAuthApiError(410, { error: 'code_expired' }), t)).toBe('op.auth.firstSignIn.expired');
    expect(projectFirstSignInError(new StaffAuthApiError(429, { error: 'too_many_attempts' }), t)).toBe('op.auth.firstSignIn.expired');
  });

  it('names a full staff plan', () => {
    expect(projectFirstSignInError(new StaffAuthApiError(409, { code: 'plan_limit' }), t)).toBe('op.auth.firstSignIn.planLimit');
  });
});
