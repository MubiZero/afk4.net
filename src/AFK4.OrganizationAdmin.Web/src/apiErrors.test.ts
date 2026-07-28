import { describe, expect, it } from 'bun:test';
import { createTranslator } from '@afk4/i18n';
import { projectOperatorError } from './apiErrors';
import { PlatformApiError } from './platformApi';

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

  it.each([
    ['reservation_not_found', 'Бронь не найдена. Обновите список броней.'],
    ['organization_mismatch', 'Запрос относится к другой организации. Обновите подключение к клубу.'],
    ['invalid_payment_split', 'Проверьте распределение суммы между способами оплаты.'],
    ['mixed_currency', 'Все части оплаты должны быть в одной валюте.'],
    ['out_of_stock', 'Товара недостаточно на складе. Обновите остатки.'],
    ['idempotency_key_required', 'Не удалось безопасно повторить действие. Попробуйте ещё раз.'],
    ['reservation_already_started', 'Для этой брони сессия уже запущена.'],
    ['version_conflict', 'Данные уже изменились. Обновите их и повторите действие.'],
    ['reservation_confirmation_required', 'Сначала подтвердите бронь, затем запускайте сессию.'],
    ['reservation_expired', 'Время брони уже истекло. Создайте новую бронь или запустите гостевую сессию.'],
    ['seat_unavailable', 'Выбранное место недоступно для запуска сессии.'],
    ['player_required_for_wallet', 'Для выбранного способа оплаты нужен клиент клуба.'],
    ['session_start_invalid', 'Проверьте параметры запуска сессии.'],
    ['idempotency_conflict', 'Этот запрос уже использован с другими параметрами. Обновите данные.'],
    ['open_shift_required', 'Чтобы принять оплату, сначала откройте смену.'],
    ['insufficient_funds', 'На балансе клиента недостаточно средств.'],
    ['session_start_conflict', 'Сессию нельзя запустить из-за конфликта. Обновите данные.']
  ])('localizes reservation start error %s', (code, expectedDetail) => {
    const error = new PlatformApiError(
      'request failed',
      409,
      'Conflict',
      JSON.stringify({ code, error: 'backend detail', currentVersion: 4 })
    );

    expect(projectOperatorError(error, t)).toEqual({
      title: 'Действие не выполнено',
      detail: expectedDetail
    });
  });

  it('provides localized copy for an early reservation start warning', () => {
    expect(t('op.booking.start.earlyWarning', { time: '18:30' })).toBe(
      'Бронь начинается в 18:30. При раннем запуске оплата начнётся сейчас.'
    );
  });
});
