import { describe, expect, it } from 'bun:test';
import { PlatformApiError } from '../platformApi';
import { reputationLookupPhone, reputationTone, reputationErrorKey } from './reputationModel';

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

describe('reputationTone', () => {
  const asOf = '2026-08-20T00:00:00Z';

  it('сетевой запрет перевешивает любые числа', () => {
    expect(reputationTone({ networkVisits: 40, networkNoShows: 0, networkBanned: true, calculatedAtUtc: asOf })).toBe('banned');
  });

  it('неявки — повод присмотреться, их отсутствие — спокойный тон', () => {
    expect(reputationTone({ networkVisits: 3, networkNoShows: 1, networkBanned: false, calculatedAtUtc: asOf })).toBe('watch');
    expect(reputationTone({ networkVisits: 14, networkNoShows: 0, networkBanned: false, calculatedAtUtc: asOf })).toBe('clean');
  });

  it('человек без единого визита — не подозреваемый: сеть его просто не знает', () => {
    expect(reputationTone({ networkVisits: 0, networkNoShows: 0, networkBanned: false, calculatedAtUtc: asOf })).toBe('clean');
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
