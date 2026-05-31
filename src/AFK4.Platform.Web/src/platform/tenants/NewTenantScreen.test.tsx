import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi, beforeAll } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { NewTenantScreen } from './NewTenantScreen';

beforeAll(() => {
  window.HTMLElement.prototype.hasPointerCapture = () => false;
  window.HTMLElement.prototype.scrollIntoView = () => {};
  window.HTMLElement.prototype.releasePointerCapture = () => {};
});

const response = {
  tenant: { organizationId: 'org-9' },
  ownerInvite: { ownerInviteId: 'i1', code: 'X' }
} as never;

function renderScreen(client: any, onCreated = vi.fn(), onCancel = vi.fn()) {
  render(
    <I18nProvider><ToastProvider>
      <NewTenantScreen client={client} onCreated={onCreated} onCancel={onCancel} />
    </ToastProvider></I18nProvider>
  );
  return { onCreated, onCancel };
}

function fillRequired() {
  fireEvent.change(screen.getByLabelText('Ключ тенанта'), { target: { value: '  victory  ' } });
  fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'Victory' } });
  fireEvent.change(screen.getByLabelText('Ключ филиала'), { target: { value: 'main' } });
  fireEvent.change(screen.getByLabelText('Название филиала'), { target: { value: 'Main' } });
  fireEvent.change(screen.getByLabelText('Город'), { target: { value: 'Moscow' } });
}

it('submits trimmed values with the default plan/status and calls onCreated', async () => {
  const client = { createTenant: vi.fn().mockResolvedValue(response) };
  const { onCreated } = renderScreen(client);

  fillRequired();
  fireEvent.click(screen.getByRole('button', { name: 'Создать тенант' }));

  await waitFor(() => expect(client.createTenant).toHaveBeenCalled());
  const payload = client.createTenant.mock.calls[0][0];
  expect(payload.organizationSlug).toBe('victory');
  expect(payload.organizationName).toBe('Victory');
  expect(payload.planCode).toBe('starter');
  expect(payload.subscriptionStatus).toBe('trial');
  expect(payload.limits).toBeNull();
  await waitFor(() => expect(onCreated).toHaveBeenCalledWith(response));
});

it('shows an inline error when creation fails', async () => {
  const client = { createTenant: vi.fn().mockRejectedValue(new Error('slug taken')) };
  renderScreen(client);
  fillRequired();
  fireEvent.click(screen.getByRole('button', { name: 'Создать тенант' }));
  expect(await screen.findByText('slug taken')).toBeInTheDocument();
});

it('cancels without submitting', () => {
  const client = { createTenant: vi.fn() };
  const { onCancel } = renderScreen(client);
  fireEvent.click(screen.getByRole('button', { name: 'Отмена' }));
  expect(onCancel).toHaveBeenCalled();
  expect(client.createTenant).not.toHaveBeenCalled();
});

it('auto-fills the organization slug from the name until the slug is edited', () => {
  const client = { createTenant: vi.fn() };
  renderScreen(client);

  // Type into the org-name input — slug should auto-fill
  fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'AFK4 Душанбе' } });
  expect((screen.getByLabelText('Ключ тенанта') as HTMLInputElement).value).toBe('afk4-dushanbe');

  // Edit the slug directly — auto-fill should stop
  fireEvent.change(screen.getByLabelText('Ключ тенанта'), { target: { value: 'custom-slug' } });
  fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'AFK4 Душанбе v2' } });
  expect((screen.getByLabelText('Ключ тенанта') as HTMLInputElement).value).toBe('custom-slug');
});
