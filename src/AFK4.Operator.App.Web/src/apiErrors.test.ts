import { describe, expect, it } from 'bun:test';
import { createTranslator } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';

const t = createTranslator('ru');

describe('projectOperatorError', () => {
  it('keeps actionable platform error text', () => {
    expect(projectOperatorError(new Error('Platform API returned 401 Unauthorized.'), t)).toEqual({
      title: 'Действие не выполнено',
      detail: 'Platform API returned 401 Unauthorized.'
    });
  });

  it('uses a stable fallback when the failure has no details', () => {
    expect(projectOperatorError(undefined, t)).toEqual({
      title: 'Действие не выполнено',
      detail: 'Сервер не вернул подробности. Повторите действие или проверьте связь.'
    });
  });
});
