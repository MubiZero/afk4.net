import { it, expect } from 'vitest';
import { vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';
import { ConfirmDialog } from './ConfirmDialog';

it('confirms with the typed reason and disables confirm while pending', () => {
  const onConfirm = vi.fn();
  render(
    <ConfirmDialog
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
    />
  );
  fireEvent.change(screen.getByLabelText('Причина'), { target: { value: 'списано' } });
  fireEvent.click(screen.getByRole('button', { name: 'Удалить' }));
  expect(onConfirm).toHaveBeenCalledWith('списано');
});

it('disables the confirm button while pending', () => {
  render(
    <ConfirmDialog open title="t" confirmLabel="Удалить" cancelLabel="Отмена"
      pending onConfirm={() => {}} onOpenChange={() => {}} />
  );
  expect(screen.getByRole('button', { name: 'Удалить' })).toBeDisabled();
});
