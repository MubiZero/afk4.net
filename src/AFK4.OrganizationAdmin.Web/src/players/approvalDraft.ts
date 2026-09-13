import type { MoneyActionSubmitRequest } from '../api/clients/moneyActions';

/**
 * Сборка заявки на одобрение из операции, которая упёрлась в порог сотрудника.
 *
 * Вынесено из экрана и покрыто тестами по одной причине: одобренную заявку сервер исполняет
 * САМ, по тому, что в ней лежит. Ошибка в знаке или в типе операции здесь — это не «диалог
 * показал не то», а «старший одобрил одно, а исполнилось другое», и заметить это можно только
 * по расхождению в леджере задним числом.
 */

/// Ручная корректировка. Знак берётся как есть: он уже посчитан направлением (начислить/списать).
export function correctionApprovalRequest(input: {
  organizationId: string;
  playerAccountId: string;
  accountType: string;
  signedAmountMinorUnits: number;
  quantitySeconds: number;
  currencyCode: string;
  reason: string;
  idempotencyKey: string;
}): MoneyActionSubmitRequest {
  return {
    organizationId: input.organizationId,
    actionType: 'manual_correction',
    playerAccountId: input.playerAccountId,
    ledgerEntryId: null,
    accountType: input.accountType,
    signedAmountMinorUnits: input.signedAmountMinorUnits,
    currencyCode: input.currencyCode,
    quantitySeconds: input.quantitySeconds,
    reason: input.reason,
    idempotencyKey: input.idempotencyKey
  };
}

/// Возврат по записи леджера. Возврат зеркалит исходную запись, поэтому знак у него ОБРАТНЫЙ
/// её знаку: возврат начисления списывает, возврат списания начисляет. Сумма приходит
/// положительной (частичный возврат вводится как «сколько вернуть»), знак ставится здесь.
export function refundApprovalRequest(input: {
  organizationId: string;
  playerAccountId: string;
  ledgerEntryId: string;
  accountType: string;
  /// Знак ИСХОДНОЙ записи леджера, которую возвращают.
  originalSignedMinorUnits: number;
  /// Сколько возвращают, всегда положительное.
  refundMinorUnits: number;
  currencyCode: string;
  reason: string;
  idempotencyKey: string;
}): MoneyActionSubmitRequest {
  const originalSign = input.originalSignedMinorUnits < 0 ? -1 : 1;
  return {
    organizationId: input.organizationId,
    actionType: 'refund',
    playerAccountId: input.playerAccountId,
    ledgerEntryId: input.ledgerEntryId,
    accountType: input.accountType,
    signedAmountMinorUnits: -originalSign * Math.abs(input.refundMinorUnits),
    currencyCode: input.currencyCode,
    quantitySeconds: 0,
    reason: input.reason,
    idempotencyKey: input.idempotencyKey
  };
}
