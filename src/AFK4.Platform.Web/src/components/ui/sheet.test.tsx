import { it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Sheet, SheetContent, SheetTitle } from './sheet';

it('renders the drawer content when open', () => {
  render(
    <Sheet open onOpenChange={() => {}}>
      <SheetContent><SheetTitle>ПК-3</SheetTitle></SheetContent>
    </Sheet>
  );
  expect(screen.getByText('ПК-3')).toBeInTheDocument();
});

it('renders nothing when closed', () => {
  render(
    <Sheet open={false} onOpenChange={() => {}}>
      <SheetContent><SheetTitle>ПК-3</SheetTitle></SheetContent>
    </Sheet>
  );
  expect(screen.queryByText('ПК-3')).toBeNull();
});
