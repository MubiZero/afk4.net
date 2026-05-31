import { describe, expect, it } from 'bun:test';
import { projectOperatorError } from './apiErrors';

describe('projectOperatorError', () => {
  it('keeps actionable platform error text', () => {
    expect(projectOperatorError(new Error('Platform API returned 401 Unauthorized.'))).toEqual({
      title: 'Действие не выполнено',
      detail: 'Platform API returned 401 Unauthorized.'
    });
  });

  it('uses a stable fallback when the failure has no details', () => {
    expect(projectOperatorError(undefined)).toEqual({
      title: 'Действие не выполнено',
      detail: 'Платформа не вернула подробности. Повторите действие или проверьте связь.'
    });
  });
});
