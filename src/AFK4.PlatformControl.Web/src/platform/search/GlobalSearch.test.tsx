import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { expect, it, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { GlobalSearch } from './GlobalSearch';

const result = { kind: 'organization', id: 'org-orion', title: 'Orion Gaming', context: 'orion', href: '/admin/organizations/org-orion' } as const;

it('searches after the minimum query and exposes canonical result links', async () => {
  const search = mock().mockResolvedValue([result]);
  render(<I18nProvider><GlobalSearch client={{ search }} onNavigate={() => {}} /></I18nProvider>);
  const input = screen.getByRole('combobox', { name: 'Поиск по платформе' });
  fireEvent.change(input, { target: { value: 'o' } });
  await new Promise(resolve => setTimeout(resolve, 220));
  expect(search).not.toHaveBeenCalled();
  fireEvent.change(input, { target: { value: 'orion' } });
  const option = await screen.findByRole('option', { name: /Orion Gaming.*организация/i });
  expect(option).toHaveAttribute('data-href', '/admin/organizations/org-orion');
});

it('supports keyboard selection and Escape', async () => {
  const onNavigate = mock();
  render(<I18nProvider><GlobalSearch client={{ search: mock().mockResolvedValue([result]) }} onNavigate={onNavigate} /></I18nProvider>);
  const input = screen.getByRole('combobox');
  fireEvent.change(input, { target: { value: 'orion' } });
  await screen.findByRole('option');
  fireEvent.keyDown(input, { key: 'ArrowDown' });
  fireEvent.keyDown(input, { key: 'Enter' });
  expect(onNavigate).toHaveBeenCalledWith('/admin/organizations/org-orion');
  expect(input).toHaveValue('');
  fireEvent.change(input, { target: { value: 'orion' } });
  await waitFor(() => expect(input).toHaveValue('orion'));
  fireEvent.keyDown(input, { key: 'Escape' });
  expect(input).toHaveValue('');
});

it('shows transport failure without stale results', async () => {
  render(<I18nProvider><GlobalSearch client={{ search: mock().mockRejectedValue(new Error('offline')) }} onNavigate={() => {}} /></I18nProvider>);
  fireEvent.change(screen.getByRole('combobox'), { target: { value: 'orion' } });
  expect(await screen.findByText('Поиск временно недоступен.')).toBeVisible();
});
