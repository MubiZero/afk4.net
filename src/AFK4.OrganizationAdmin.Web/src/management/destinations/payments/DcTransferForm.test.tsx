import { describe, it, expect, mock, afterEach, afterAll } from 'bun:test';
import { render, screen, fireEvent, cleanup, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../../operatorToast';
import type { DcPayLinkConfigDto } from '../../../operatorApiClients';

let stored: DcPayLinkConfigDto = { cardSet: false, cardLast4: '', commentTemplate: 'AFK4-{ref}', isActive: false };
const get = mock(async (): Promise<DcPayLinkConfigDto> => stored);
const update = mock(async (req: { cardNumber: string | null; commentTemplate: string; isActive: boolean }): Promise<DcPayLinkConfigDto> => {
  stored = {
    cardSet: stored.cardSet || !!req.cardNumber,
    cardLast4: req.cardNumber ? req.cardNumber.slice(-4) : stored.cardLast4,
    commentTemplate: req.commentTemplate,
    isActive: req.isActive
  };
  return stored;
});

const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../../../operatorHelpers', () => ({
  ...actual,
  createAuthenticatedOperatorClients: () => ({ dcConfig: { get, update } })
}));

const { DcTransferForm } = await import('./DcTransferForm');

const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o1' }, branchId: 'b1' } as never;

const view = () =>
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <DcTransferForm backend={backend} />
      </ToastProvider>
    </I18nProvider>
  );

afterEach(() => {
  stored = { cardSet: false, cardLast4: '', commentTemplate: 'AFK4-{ref}', isActive: false };
  get.mockClear();
  update.mockClear();
  cleanup();
});
afterAll(() => mock.restore());

describe('DcTransferForm', () => {
  it('keeps Save disabled until the card and comment template are valid, then saves', async () => {
    view();
    fireEvent.click(await screen.findByRole('button', { name: /настроить/i }));
    const save = await screen.findByRole('button', { name: /сохранить/i });
    expect(save).toBeDisabled();

    fireEvent.change(screen.getByLabelText(/карт/i), { target: { value: '1234567890123456' } });
    expect(save).not.toBeDisabled();

    fireEvent.click(save);
    await waitFor(() => expect(update).toHaveBeenCalledWith({
      cardNumber: '1234567890123456',
      commentTemplate: 'AFK4-{ref}',
      isActive: false
    }));
  });

  it('disables Save when the comment template loses the {ref} placeholder', async () => {
    stored = { cardSet: true, cardLast4: '3456', commentTemplate: 'AFK4-{ref}', isActive: true };
    view();
    fireEvent.click(await screen.findByRole('button', { name: /настроить/i }));
    const save = await screen.findByRole('button', { name: /сохранить/i });
    await waitFor(() => expect(save).not.toBeDisabled());

    fireEvent.change(screen.getByLabelText(/шаблон/i), { target: { value: 'AFK4-no-placeholder' } });
    expect(save).toBeDisabled();
  });

  it('shows the ••••last4 placeholder and allows saving without re-entering the card number', async () => {
    stored = { cardSet: true, cardLast4: '3456', commentTemplate: 'AFK4-{ref}', isActive: true };
    view();
    fireEvent.click(await screen.findByRole('button', { name: /настроить/i }));

    const cardInput = await screen.findByLabelText(/карт/i);
    await waitFor(() => expect((cardInput as HTMLInputElement).placeholder).toMatch(/3456/));

    const save = await screen.findByRole('button', { name: /сохранить/i });
    await waitFor(() => expect(save).not.toBeDisabled());
    fireEvent.click(save);
    await waitFor(() => expect(update).toHaveBeenCalledWith({
      cardNumber: null,
      commentTemplate: 'AFK4-{ref}',
      isActive: true
    }));
  });
});
