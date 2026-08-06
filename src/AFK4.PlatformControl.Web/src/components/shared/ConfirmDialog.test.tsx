import { it, expect, mock } from 'bun:test';
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ConfirmDialog } from './ConfirmDialog';

it('confirms with the typed reason and disables confirm while pending', () => {
  const onConfirm = mock();
  render(<I18nProvider><ConfirmDialog
      open
      title="Удалить устройство?"
      description="Действие необратимо."
      confirmLabel="Удалить"
      cancelLabel="Отмена"
      reasonLabel="Причина"
      destructive
      pending={false}
      onConfirm={onConfirm}
      onOpenChange={() => {}}
    /></I18nProvider>
  );
  fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'списано' } });
  fireEvent.click(screen.getByRole('button', { name: 'Удалить' }));
  expect(onConfirm).toHaveBeenCalledWith('списано');
});

it('disables the confirm button while pending', () => {
  render(<I18nProvider><ConfirmDialog open title="t" confirmLabel="Удалить" cancelLabel="Отмена"
      pending onConfirm={() => {}} onOpenChange={() => {}} /></I18nProvider>
  );
  expect(screen.getByRole('button', { name: 'Удалить' })).toBeDisabled();
});
