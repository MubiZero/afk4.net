import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, beforeAll, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { NewOrganizationScreen } from './NewOrganizationScreen';

beforeAll(() => {
  window.HTMLElement.prototype.hasPointerCapture = () => false;
  window.HTMLElement.prototype.scrollIntoView = () => {};
  window.HTMLElement.prototype.releasePointerCapture = () => {};
});

const response = {
  organization: { organizationId: 'org-9' },
  organizationOwnerInvite: { organizationOwnerInviteId: 'i1', code: 'X' }
} as never;

function renderScreen(client: any, onCreated = mock(), onCancel = mock()) {
  render(
    <I18nProvider><ToastProvider>
      <NewOrganizationScreen client={client} onCreated={onCreated} onCancel={onCancel} />
    </ToastProvider></I18nProvider>
  );
  return { onCreated, onCancel };
}

function fillRequired() {
  fireEvent.change(screen.getByLabelText('Ключ организации'), { target: { value: '  victory  ' } });
  fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'Victory' } });
  fireEvent.change(screen.getByLabelText('Ключ филиала'), { target: { value: 'main' } });
  fireEvent.change(screen.getByLabelText('Название филиала'), { target: { value: 'Main' } });
  fireEvent.change(screen.getByLabelText('Город'), { target: { value: 'Moscow' } });
}

it('submits trimmed values with the default plan/status and calls onCreated', async () => {
  const client = { createOrganization: mock().mockResolvedValue(response) };
  const { onCreated } = renderScreen(client);

  fillRequired();
  fireEvent.click(screen.getByRole('button', { name: 'Создать организацию' }));

  await waitFor(() => expect(client.createOrganization).toHaveBeenCalled());
  const payload = client.createOrganization.mock.calls[0][0];
  expect(payload.organizationSlug).toBe('victory');
  expect(payload.organizationName).toBe('Victory');
  expect(payload.planCode).toBe('starter');
  expect(payload.subscriptionStatus).toBe('trial');
  expect(payload.limits).toBeNull();
  await waitFor(() => expect(onCreated).toHaveBeenCalledWith(response));
});

it('shows an inline error when creation fails', async () => {
  const client = { createOrganization: mock().mockRejectedValue(new Error('slug taken')) };
  renderScreen(client);
  fillRequired();
  fireEvent.click(screen.getByRole('button', { name: 'Создать организацию' }));
  expect(await screen.findByText('slug taken')).toBeInTheDocument();
});

it('cancels without submitting', () => {
  const client = { createOrganization: mock() };
  const { onCancel } = renderScreen(client);
  fireEvent.click(screen.getByRole('button', { name: 'Отмена' }));
  expect(onCancel).toHaveBeenCalled();
  expect(client.createOrganization).not.toHaveBeenCalled();
});

it('auto-fills the organization slug from the name until the slug is edited', () => {
  const client = { createOrganization: mock() };
  renderScreen(client);

  // Type into the org-name input — slug should auto-fill
  fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'AFK4 Душанбе' } });
  expect((screen.getByLabelText('Ключ организации') as HTMLInputElement).value).toBe('afk4-dushanbe');

  // Edit the slug directly — auto-fill should stop
  fireEvent.change(screen.getByLabelText('Ключ организации'), { target: { value: 'custom-slug' } });
  fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'AFK4 Душанбе v2' } });
  expect((screen.getByLabelText('Ключ организации') as HTMLInputElement).value).toBe('custom-slug');
});
