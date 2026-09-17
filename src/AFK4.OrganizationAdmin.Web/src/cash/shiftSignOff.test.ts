import { describe, expect, it } from 'bun:test';
import { isSignOffRequired, signOffCandidates } from './shiftSignOff';
import { PlatformApiError } from '../platformApi';

const staff = [
  { staffUserId: 's1', displayName: 'Мадина К.', isActive: true },
  { staffUserId: 's2', displayName: 'Азиз П.', isActive: true },
  { staffUserId: 's3', displayName: 'Уволенный', isActive: false }
] as never[];

describe('signOffCandidates', () => {
  it('не предлагает того, кто открыл смену, и того, кто её закрывает', () => {
    expect(signOffCandidates(staff, 's1', 's2')).toEqual([]);
  });

  it('оставляет активных сотрудников по алфавиту', () => {
    expect(signOffCandidates(staff, null, null).map((candidate) => candidate.displayName))
      .toEqual(['Азиз П.', 'Мадина К.']);
  });
});

// Допуск филиала мог не подгрузиться, и тогда клиенту нечем посчитать, нужна ли подпись. Тогда
// правду знает только сервер, и его отказ надо услышать — иначе поле подписи не появится
// никогда, а смену с недостачей не закрыть.
describe('isSignOffRequired', () => {
  const error = (body: string) => new PlatformApiError('failed', 400, 'Bad Request', body);

  it('узнаёт отказ по коду', () => {
    expect(isSignOffRequired(error(JSON.stringify({ code: 'shift_sign_off_required' })))).toBe(true);
    expect(isSignOffRequired(error(JSON.stringify({ error: 'shift_sign_off_required' })))).toBe(true);
  });

  it('не путает его с другими отказами', () => {
    expect(isSignOffRequired(error(JSON.stringify({ code: 'shift_already_closed' })))).toBe(false);
    expect(isSignOffRequired(error('not json'))).toBe(false);
    expect(isSignOffRequired(new Error('boom'))).toBe(false);
    expect(isSignOffRequired(undefined)).toBe(false);
  });
});
