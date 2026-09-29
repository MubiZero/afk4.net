import { describe, expect, it } from 'bun:test';
import { PlatformApiError } from '../platformApi';
import { reputationLookupPhone, reputationErrorKey } from './reputationModel';

describe('reputationLookupPhone', () => {
  it('собирает точный номер из локальной маски и из уже полного номера', () => {
    expect(reputationLookupPhone('93 738 00 70')).toBe('+992937380070');
    expect(reputationLookupPhone('+992 93 738 00 70')).toBe('+992937380070');
  });

  it('огрызок номера — не номер: спрашивать сеть нечем', () => {
    expect(reputationLookupPhone('93 738')).toBeNull();
    expect(reputationLookupPhone('')).toBeNull();
    expect(reputationLookupPhone('   ')).toBeNull();
  });
});

describe('reputationErrorKey', () => {
  const apiError = (status: number, body = '') => new PlatformApiError('failed', status, 'x', body);

  it('переводит отказы маршрута в понятные оператору причины', () => {
    expect(reputationErrorKey(apiError(400, '{"error":"invalid_phone"}'))).toBe('op.reputation.invalidPhone');
    expect(reputationErrorKey(apiError(429))).toBe('op.reputation.tooManyLookups');
    expect(reputationErrorKey(apiError(404))).toBe('op.reputation.unknown');
  });

  it('остальное отдаёт общей проекции ошибки, а не выдумывает свой текст', () => {
    expect(reputationErrorKey(apiError(500))).toBeNull();
    expect(reputationErrorKey(new Error('offline'))).toBeNull();
  });
});
