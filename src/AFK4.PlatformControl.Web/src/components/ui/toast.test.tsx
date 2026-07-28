import { render, screen, fireEvent, act } from '@testing-library/react';
import { it, expect, beforeEach, afterEach, jest } from 'bun:test';
import { ToastProvider, useToast } from './toast';
import { I18nProvider } from '@/i18n/I18nProvider';

beforeEach(() => { jest.useFakeTimers(); });
afterEach(() => { jest.useRealTimers(); });

function Trigger() {
  const { toast } = useToast();
  return <button onClick={() => toast({ title: 'Сохранено', variant: 'success' })}>fire</button>;
}

it('shows a toast when fired and dismisses after the delay', () => {
  render(<I18nProvider><ToastProvider autoDismissMs={1000}><Trigger /></ToastProvider></I18nProvider>);
  expect(screen.queryByText('Сохранено')).toBeNull();
  fireEvent.click(screen.getByText('fire'));
  expect(screen.getByText('Сохранено')).toBeInTheDocument();
  act(() => { jest.advanceTimersByTime(1000); });
  expect(screen.queryByText('Сохранено')).toBeNull();
});

it('throws when useToast is used outside the provider', () => {
  function Orphan() { useToast(); return null; }
  expect(() => render(<Orphan />)).toThrow();
});
