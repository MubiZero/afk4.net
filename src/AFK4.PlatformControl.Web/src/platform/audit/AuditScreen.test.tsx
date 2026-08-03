import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { expect, it, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { AuditScreen } from './AuditScreen';

it('loads server-filtered audit and reports filters to the route owner', async () => {
  const search = mock().mockResolvedValue({ records: [], limit: 100 });
  const onFiltersChange = mock();
  render(<I18nProvider><AuditScreen client={{ search }} filters={{ organizationId: '', action: '', outcome: '', from: '', to: '' }} onFiltersChange={onFiltersChange} /></I18nProvider>);
  await waitFor(() => expect(search).toHaveBeenCalled());
  fireEvent.change(screen.getByLabelText('Действие'), { target: { value: 'updates.rollout.create' } });
  fireEvent.click(screen.getByRole('button', { name: 'Применить фильтры' }));
  expect(onFiltersChange).toHaveBeenCalledWith(expect.objectContaining({ action: 'updates.rollout.create' }));
  expect(screen.getByText('Записей аудита не найдено.')).toBeVisible();
});
