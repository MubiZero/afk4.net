import { render, screen, fireEvent, act } from '@testing-library/react';
import { vi, it, expect, beforeEach, afterEach } from 'vitest';
import { ToastProvider, useToast } from './toast';

beforeEach(() => { vi.useFakeTimers(); });
afterEach(() => { vi.useRealTimers(); });

function Trigger() {
  const { toast } = useToast();
  return <button onClick={() => toast({ title: 'Сохранено', variant: 'success' })}>fire</button>;
}

it('shows a toast when fired and dismisses after the delay', () => {
  render(<ToastProvider autoDismissMs={1000}><Trigger /></ToastProvider>);
  expect(screen.queryByText('Сохранено')).toBeNull();
  fireEvent.click(screen.getByText('fire'));
  expect(screen.getByText('Сохранено')).toBeInTheDocument();
  act(() => { vi.advanceTimersByTime(1000); });
  expect(screen.queryByText('Сохранено')).toBeNull();
});

it('throws when useToast is used outside the provider', () => {
  function Orphan() { useToast(); return null; }
  expect(() => render(<Orphan />)).toThrow();
});
