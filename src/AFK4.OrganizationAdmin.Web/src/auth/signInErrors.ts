import { StaffAuthErrorCodeNames, StaffSignInStepNames, type StaffSignInStepName } from '@afk4/contracts';
import { StaffAuthApiError } from './staffAuthApi';
import { isRecord, type TFunc } from '../operatorHelpers';

/**
 * Слова для отказов входа. Каждый отказ назван тем, что человеку делать дальше: «ПИН-код не тот» —
 * набрать заново, «заперто» — подождать, «номер не заведён» — идти к руководителю. Общее «не
 * удалось войти» остаётся только для сбоя, о котором сказать больше нечего.
 */
export function projectPinSignInError(cause: unknown, via: 'phone' | 'login', t: TFunc): string {
  if (!(cause instanceof StaffAuthApiError)) {
    return t('auth.error.generic');
  }

  if (cause.status === 401) {
    // По номеру сервер уже сказал, что номер заведён, — не так может быть только ПИН-код.
    return via === 'phone' ? t('op.auth.error.wrongPin') : t('auth.error.invalid');
  }
  if (cause.status === 429 && errorCode(cause) === StaffAuthErrorCodeNames.TooManyPasswordAttempts) {
    return t('auth.error.lockedOut');
  }
  if (cause.status === 429) {
    return t('op.auth.error.tooFast');
  }
  if (cause.status === 403) {
    return t('op.auth.error.otherClub');
  }
  return t('auth.error.generic');
}

/** Второй шаг по номеру не ПИН-код и не код первого входа — объяснить, почему. */
export function projectNextStepError(step: StaffSignInStepName, t: TFunc): string {
  return step === StaffSignInStepNames.InviteExpired ? t('op.auth.firstSignIn.expired') : t('op.auth.phone.unknown');
}

/** Отказы кода первого входа и приёма приглашения. */
export function projectFirstSignInError(cause: unknown, t: TFunc): string {
  if (!(cause instanceof StaffAuthApiError)) {
    return t('op.auth.firstSignIn.generic');
  }

  const code = errorCode(cause);
  if (code === 'invalid_code') {
    const remaining = isRecord(cause.body) && typeof cause.body.remainingAttempts === 'number'
      ? cause.body.remainingAttempts
      : null;
    // Последняя попытка сгорела — код мёртв, и «осталось 0» ничего не подсказывает.
    return remaining === 0
      ? t('op.auth.firstSignIn.expired')
      : remaining === null
        ? t('op.auth.firstSignIn.wrongCodeNoCount')
        : t('op.auth.firstSignIn.wrongCode', { count: remaining });
  }
  if (code === 'code_expired' || code === 'too_many_attempts') {
    return t('op.auth.firstSignIn.expired');
  }
  if (cause.status === 409) {
    return t('op.auth.firstSignIn.planLimit');
  }
  if (cause.status === 429) {
    return t('op.auth.error.tooFast');
  }
  return t('op.auth.firstSignIn.generic');
}

function errorCode(cause: StaffAuthApiError): string | null {
  const body = cause.body;
  if (!isRecord(body)) return null;
  const value = body.code ?? body.Code ?? body.error;
  return typeof value === 'string' ? value : null;
}
