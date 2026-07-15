import { createTranslator } from '@afk4/i18n';
import { describe, expect, it } from 'bun:test';
import { buildSystemStatusModel } from './systemStatusModel';

const t = createTranslator('ru');

describe('systemStatusModel', () => {
  it('projects real roles and independent realtime/server states', () => {
    const model = buildSystemStatusModel({
      operatorName: 'Иванов И.И.',
      roleNames: ['cashier_operator', 'shift_supervisor'],
      clubName: 'Арена',
      realtimeState: 'connected',
      dataSource: 'backend',
      appVersion: '2.45.1'
    }, t);

    expect(model.left.map((field) => field.value)).toEqual([
      'Иванов И.И.',
      'Кассир-оператор, Старший смены',
      'Арена'
    ]);
    expect(model.connection.tone).toBe('ok');
    expect(model.server.tone).toBe('ok');
    expect(model.version.value).toBe('2.45.1');
  });

  it('keeps an unknown backend role visible and never fabricates missing data', () => {
    const model = buildSystemStatusModel({
      operatorName: '',
      roleNames: ['future_role'],
      clubName: '',
      realtimeState: 'disconnected',
      dataSource: 'fixture',
      appVersion: ''
    }, t);

    expect(model.left.map((field) => field.value)).toEqual(['—', 'future_role', '—']);
    expect(model.connection.value).toBe('Офлайн');
    expect(model.connection.tone).toBe('bad');
    expect(model.server.value).toBe('Недоступен');
    expect(model.server.tone).toBe('bad');
    expect(model.version.value).toBe('—');
  });

  it('uses warning tone while realtime is connecting', () => {
    const model = buildSystemStatusModel({
      operatorName: 'Operator',
      roleNames: [],
      clubName: 'Club',
      realtimeState: 'reconnecting',
      dataSource: 'backend',
      appVersion: '1.0.0'
    }, t);

    expect(model.left[1]?.value).toBe('—');
    expect(model.connection.value).toBe('Переподключение');
    expect(model.connection.tone).toBe('warn');
  });
});
