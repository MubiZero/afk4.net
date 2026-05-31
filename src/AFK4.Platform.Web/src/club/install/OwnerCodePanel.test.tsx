import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { OwnerCodeSummary, OwnerCodeIssued } from '@/api/types';
import { OwnerCodePanel } from './OwnerCodePanel';

const summary: OwnerCodeSummary = { codeSuffix: '5678', expiresAtUtc: '2026-06-01T00:00:00.000Z', lastUsedAtUtc: null, failedAttemptCount: 0 };

function fakeClient() {
  return {
    getOwnerCode: mock<() => Promise<OwnerCodeSummary | null>>(async () => summary),
    generateOwnerCode: mock<() => Promise<OwnerCodeIssued>>(async () => ({ ownerCode: '99998888', codeSuffix: '8888', expiresAtUtc: '2026-07-01T00:00:00.000Z' })),
    rotateOwnerCode: mock<(reason: string) => Promise<OwnerCodeIssued>>(async () => ({ ownerCode: '77776666', codeSuffix: '6666', expiresAtUtc: '2026-07-01T00:00:00.000Z' }))
  };
}

it('shows the masked code then the full code after generating', async () => {
  const client = fakeClient();
  render(
    <I18nProvider><ToastProvider>
      <OwnerCodePanel client={client as never} canManage />
    </ToastProvider></I18nProvider>
  );
  expect(await screen.findByText('**** 5678')).toBeInTheDocument();
  fireEvent.click(screen.getByRole('button', { name: 'Сгенерировать код' }));
  await waitFor(() => expect(client.generateOwnerCode).toHaveBeenCalled());
  expect(await screen.findByText('99998888')).toBeInTheDocument();
});

it('shows a no-access note when management is not allowed', () => {
  const client = fakeClient();
  render(
    <I18nProvider><ToastProvider>
      <OwnerCodePanel client={client as never} canManage={false} />
    </ToastProvider></I18nProvider>
  );
  expect(screen.getByText('Ваша учётная запись не может генерировать код владельца.')).toBeInTheDocument();
  expect(client.getOwnerCode).not.toHaveBeenCalled();
});
