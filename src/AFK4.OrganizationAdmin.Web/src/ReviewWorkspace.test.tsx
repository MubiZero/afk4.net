import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from './operatorToast';

const pendingRequest = {
  moneyActionRequestId: 'ma-1', organizationId: 'o', branchId: 'b1', shiftId: 'sh-1',
  actionType: 'refund', requestedByStaffUserId: 'u1', amountMinorUnits: 12000,
  currencyCode: 'TJS', reason: 'Ошибочный чек', state: 'pending',
  createdAtUtc: new Date(Date.now() - 10 * 60_000).toISOString(),
  expiresAtUtc: new Date(Date.now() + 60 * 60_000).toISOString()
};
const listPending = mock(async () => ({ requests: [pendingRequest] }));
const approve = mock(async () => ({}));
const reject = mock(async () => ({}));
const search = mock(async () => ({ records: [] }));
const getStaffUsers = mock(async () => [{ staffUserId: 'u1', displayName: 'Фаррух' }]);

const actualHelpers = await import('./operatorHelpers');
mock.module('./operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({
    moneyActions: { listPending, approve, reject },
    settings: { getStaffUsers },
    audit: { search }
  })
}));
const { ReviewWorkspace } = await import('./ReviewWorkspace');

afterAll(() => {
  mock.module('./operatorHelpers', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorHelpers: typeof import('./operatorHelpers');
  }).__afk4RealOperatorHelpers);
});

afterEach(() => {
  cleanup();
  listPending.mockClear();
  approve.mockClear();
  reject.mockClear();
  search.mockClear();
  getStaffUsers.mockClear();
  mock.restore();
});

const backend = { config: { platformBaseUrl: 'http://test' }, session: { accessToken: 't' }, branchId: 'b1' } as never;
function renderReview() {
  render(<I18nProvider initialLocale="ru"><ToastProvider><ReviewWorkspace currencyCode="TJS" backend={backend} embedded /></ToastProvider></I18nProvider>);
}

describe('ReviewWorkspace', () => {
  it('выбирает заявку в инспектор риска', async () => {
    renderReview();
    fireEvent.click(await screen.findByRole('row', { name: /Возврат.*120/ }));
    const inspector = screen.getByLabelText('Детали выбранной записи');
    expect(inspector).toHaveTextContent('Истекает');
    expect(inspector).toHaveTextContent('Ошибочный чек');
  });

  it('после ошибки backend сохраняет причину отклонения', async () => {
    reject.mockRejectedValueOnce(new Error('network'));
    renderReview();
    fireEvent.click(await screen.findByRole('row', { name: /Возврат.*120/ }));
    fireEvent.click(screen.getByRole('button', { name: 'Отклонить' }));
    fireEvent.change(screen.getByLabelText('Причина отклонения'), { target: { value: 'Нет подтверждения клиента' } });
    fireEvent.click(screen.getByRole('button', { name: 'Подтвердить отклонение' }));
    expect(await screen.findByDisplayValue('Нет подтверждения клиента')).toBeInTheDocument();
    await waitFor(() => expect(reject).toHaveBeenCalled());
  });
});
