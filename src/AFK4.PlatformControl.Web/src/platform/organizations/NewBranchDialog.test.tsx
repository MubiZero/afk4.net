import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { PlatformApiError } from '@/api/platformTransport';
import { NewBranchDialog } from './NewBranchDialog';
import type { OrganizationBranch } from '@/api/types';

function branch(overrides: Partial<OrganizationBranch> = {}): OrganizationBranch {
  return {
    branchId: 'branch-2',
    slug: 'north',
    name: 'Северный',
    city: 'Душанбе',
    createdAtUtc: '2026-08-01T00:00:00Z',
    ...overrides
  };
}

function fillForm() {
  fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'Северный' } });
  fireEvent.change(screen.getByLabelText('Город'), { target: { value: 'Душанбе' } });
  fireEvent.change(screen.getByLabelText('Короткий адрес'), { target: { value: 'north' } });
}

it('создаёт филиал и отдаёт его наверх', async () => {
  const created = branch();
  const client = { createBranch: mock().mockResolvedValue(created) };
  const onCreated = mock();
  render(
    <I18nProvider><ToastProvider>
      <NewBranchDialog client={client} organizationId="org-1" onClose={mock()} onCreated={onCreated} />
    </ToastProvider></I18nProvider>
  );

  fillForm();
  fireEvent.click(screen.getByRole('button', { name: 'Создать' }));

  await waitFor(() => expect(client.createBranch).toHaveBeenCalledWith('org-1', {
    slug: 'north', name: 'Северный', city: 'Душанбе', preferredTimeZone: 'Asia/Dushanbe'
  }));
  expect(onCreated).toHaveBeenCalledWith(created);
});

it('показывает занятость тарифа при отказе по лимиту', async () => {
  const rejection = new PlatformApiError(
    409,
    'Plan branch limit has been reached.',
    'plan_limit_reached',
    null,
    JSON.stringify({
      error: 'Plan branch limit has been reached.',
      code: 'plan_limit_reached',
      planLimit: { code: 'plan_limit_reached', limitName: 'max_branches', limit: 1, current: 1, planCode: 'starter' }
    })
  );
  const client = { createBranch: mock().mockRejectedValue(rejection) };
  const onCreated = mock();
  render(
    <I18nProvider><ToastProvider>
      <NewBranchDialog client={client} organizationId="org-1" onClose={mock()} onCreated={onCreated} />
    </ToastProvider></I18nProvider>
  );

  fillForm();
  fireEvent.click(screen.getByRole('button', { name: 'Создать' }));

  await waitFor(() => expect(screen.getByText('Тариф starter: филиалов 1 из 1. Повысьте тариф, чтобы добавить ещё один.')).toBeInTheDocument());
  expect(onCreated).not.toHaveBeenCalled();
  // Диалог остаётся смонтированным (открытым) — заголовок и форма всё ещё на экране.
  expect(screen.getByText('Новый филиал')).toBeInTheDocument();
  expect(screen.getByLabelText('Короткий адрес')).toBeInTheDocument();
});

it('показывает занятый короткий адрес отдельной ошибкой', async () => {
  const rejection = new PlatformApiError(409, 'Branch slug is already taken.', 'Branch slug is already taken.', null, JSON.stringify({
    error: 'Branch slug is already taken.'
  }));
  const client = { createBranch: mock().mockRejectedValue(rejection) };
  const onCreated = mock();
  render(
    <I18nProvider><ToastProvider>
      <NewBranchDialog client={client} organizationId="org-1" onClose={mock()} onCreated={onCreated} />
    </ToastProvider></I18nProvider>
  );

  fillForm();
  fireEvent.click(screen.getByRole('button', { name: 'Создать' }));

  await waitFor(() => expect(screen.getByText('Такой короткий адрес в этом клубе уже занят.')).toBeInTheDocument());
  expect(screen.queryByText(/Повысьте тариф/)).not.toBeInTheDocument();
  expect(onCreated).not.toHaveBeenCalled();
});
