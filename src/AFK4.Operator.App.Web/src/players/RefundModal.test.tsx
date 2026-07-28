import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import type { LedgerEntryDto } from '../operatorApiClients';
import { RefundModal } from './RefundModal';

afterEach(cleanup);

const entry = (over: Partial<LedgerEntryDto> = {}): LedgerEntryDto => ({
  ledgerEntryId: 'le-1',
  organizationId: 'org-1',
  branchId: 'br-1',
  playerAccountId: 'pl-1',
  sessionId: null,
  playerPackageId: null,
  entryType: 'top_up',
  accountType: 'wallet',
  amount: { currencyCode: 'TJS', minorUnits: 50000 },
  quantitySeconds: 0,
  description: 'Пополнение кошелька',
  reason: 'Касса',
  reversesLedgerEntryId: null,
  createdByStaffUserId: 'st-1',
  createdAtUtc: '2026-05-13T10:00:00Z',
  ...over,
});

const renderModal = (over: Partial<Parameters<typeof RefundModal>[0]> = {}) => {
  const onConfirm = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <RefundModal
        entry={entry({ amount: { currencyCode: 'TJS', minorUnits: 2000 } })}
        currencyCode="TJS"
        reason="возврат"
        onChangeReason={() => {}}
        onClose={() => {}}
        onConfirm={onConfirm}
        busy={false}
        {...over}
      />
    </I18nProvider>
  );
  return { onConfirm };
};

describe('RefundModal', () => {
  it('shows the entry type label and reason field', () => {
    renderModal();
    expect(screen.getByText('Пополнение')).toBeInTheDocument();
    expect(screen.getByLabelText('Причина возврата')).toBeInTheDocument();
  });

  it('fires onConfirm on submit', () => {
    const { onConfirm } = renderModal();
    fireEvent.change(screen.getByLabelText('Сумма возврата'), { target: { value: '12.50' } });
    fireEvent.click(screen.getByRole('button', { name: /Вернуть операцию/ }));
    expect(onConfirm).toHaveBeenCalledWith(1250);
  });

  it('rejects an amount larger than the original operation', () => {
    const { onConfirm } = renderModal();
    fireEvent.change(screen.getByLabelText('Сумма возврата'), { target: { value: '20.01' } });
    fireEvent.click(screen.getByRole('button', { name: /Вернуть операцию/ }));
    expect(onConfirm).not.toHaveBeenCalled();
  });

  it('disables confirm while busy', () => {
    renderModal({ busy: true });
    expect(screen.getByRole('button', { name: /Вернуть операцию/ })).toBeDisabled();
  });
});
