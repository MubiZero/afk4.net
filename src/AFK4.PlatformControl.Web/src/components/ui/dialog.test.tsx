import { it, expect } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { Dialog, DialogContent, DialogDescription, DialogTitle } from './dialog';

it('renders content when controlled-open', () => {
  render(
    <Dialog open onOpenChange={() => {}}>
      <DialogContent><DialogTitle>Удалить устройство?</DialogTitle><DialogDescription>Проверка диалога.</DialogDescription></DialogContent>
    </Dialog>
  );
  expect(screen.getByText('Удалить устройство?')).toBeInTheDocument();
});

it('does not render content when closed', () => {
  render(
    <Dialog open={false} onOpenChange={() => {}}>
      <DialogContent><DialogTitle>Удалить устройство?</DialogTitle><DialogDescription>Проверка диалога.</DialogDescription></DialogContent>
    </Dialog>
  );
  expect(screen.queryByText('Удалить устройство?')).toBeNull();
});
