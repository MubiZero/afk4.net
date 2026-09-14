import { describe, expect, it } from 'bun:test';
import { correctionApprovalRequest, refundApprovalRequest } from './approvalDraft';

const base = {
  organizationId: 'org-1',
  playerAccountId: 'p-1',
  currencyCode: 'TJS',
  reason: 'Ошибка кассира',
  idempotencyKey: 'key-1'
};

describe('correctionApprovalRequest', () => {
  it('переносит знак направления как есть', () => {
    const credit = correctionApprovalRequest({
      ...base, accountType: 'wallet', signedAmountMinorUnits: 50_000, quantitySeconds: 0
    });
    const debit = correctionApprovalRequest({
      ...base, accountType: 'wallet', signedAmountMinorUnits: -50_000, quantitySeconds: 0
    });

    expect(credit.signedAmountMinorUnits).toBe(50_000);
    expect(debit.signedAmountMinorUnits).toBe(-50_000);
    expect(credit.actionType).toBe('manual_correction');
    expect(credit.ledgerEntryId).toBeNull();
  });

  it('сохраняет время для счетов, где мерят секундами', () => {
    const request = correctionApprovalRequest({
      ...base, accountType: 'bonus_time', signedAmountMinorUnits: 0, quantitySeconds: 3600
    });

    expect(request.quantitySeconds).toBe(3600);
  });
});

describe('refundApprovalRequest', () => {
  // Возврат зеркалит исходную запись. Ошибка знака здесь означает, что старший одобряет
  // возврат, а исполняется повторное начисление — и видно это только по леджеру задним числом.
  it('возврат начисления списывает', () => {
    const request = refundApprovalRequest({
      ...base, ledgerEntryId: 'l-1', accountType: 'wallet',
      originalSignedMinorUnits: 30_000, refundMinorUnits: 30_000
    });

    expect(request.signedAmountMinorUnits).toBe(-30_000);
    expect(request.actionType).toBe('refund');
    expect(request.ledgerEntryId).toBe('l-1');
  });

  it('возврат списания начисляет', () => {
    const request = refundApprovalRequest({
      ...base, ledgerEntryId: 'l-2', accountType: 'wallet',
      originalSignedMinorUnits: -30_000, refundMinorUnits: 30_000
    });

    expect(request.signedAmountMinorUnits).toBe(30_000);
  });

  it('частичный возврат уменьшает сумму, не трогая знак', () => {
    const request = refundApprovalRequest({
      ...base, ledgerEntryId: 'l-3', accountType: 'wallet',
      originalSignedMinorUnits: 30_000, refundMinorUnits: 10_000
    });

    expect(request.signedAmountMinorUnits).toBe(-10_000);
  });

  // Сумма приходит из поля «сколько вернуть» и по смыслу положительна; отрицательный ввод не
  // должен переворачивать направление возврата.
  it('знак ввода не переворачивает направление', () => {
    const request = refundApprovalRequest({
      ...base, ledgerEntryId: 'l-4', accountType: 'wallet',
      originalSignedMinorUnits: 30_000, refundMinorUnits: -10_000
    });

    expect(request.signedAmountMinorUnits).toBe(-10_000);
  });

  it('возврат не несёт времени: время возвращают корректировкой', () => {
    const request = refundApprovalRequest({
      ...base, ledgerEntryId: 'l-5', accountType: 'wallet',
      originalSignedMinorUnits: 30_000, refundMinorUnits: 30_000
    });

    expect(request.quantitySeconds).toBe(0);
  });
});
