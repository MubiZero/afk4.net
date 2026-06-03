import { it, expect } from 'bun:test';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { ToastProvider, useToast } from './toast';

function Trigger() {
  const { toast } = useToast();
  return <button onClick={() => toast({ title: 'Заявка отправлена', variant: 'success' })}>go</button>;
}

it('shows a toast and auto-dismisses it', async () => {
  render(
    <ToastProvider autoDismissMs={50}>
      <Trigger />
    </ToastProvider>
  );
  fireEvent.click(screen.getByText('go'));
  expect(await screen.findByText('Заявка отправлена')).toBeInTheDocument();
  await waitFor(() => expect(screen.queryByText('Заявка отправлена')).not.toBeInTheDocument());
});
