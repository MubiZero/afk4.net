import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { CategoryCreateDialog } from './CategoryCreateDialog';

it('creates a category and reports it via onCreated', async () => {
  const onCreated = vi.fn();
  const client = {
    createProductCategory: vi.fn(async () => ({
      categoryId: 'c9', organizationId: 'org', branchId: 'b1', name: 'Снеки', isActive: true, createdAtUtc: '2026-01-01T00:00:00.000Z'
    }))
  };
  render(
    <I18nProvider><ToastProvider>
      <CategoryCreateDialog open branchId="b1" organizationId="org" client={client as never} onCreated={onCreated} onOpenChange={() => {}} />
    </ToastProvider></I18nProvider>
  );
  fireEvent.change(screen.getByLabelText('Название категории'), { target: { value: 'Снеки' } });
  fireEvent.click(screen.getByRole('button', { name: 'Создать' }));
  await waitFor(() => expect(client.createProductCategory).toHaveBeenCalledWith('b1', expect.objectContaining({ organizationId: 'org', name: 'Снеки' })));
  await waitFor(() => expect(onCreated).toHaveBeenCalledWith({ categoryId: 'c9', name: 'Снеки' }));
});
