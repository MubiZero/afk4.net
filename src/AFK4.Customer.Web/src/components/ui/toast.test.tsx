import { it, expect, jest, afterEach } from 'bun:test';
import { act, render, screen, fireEvent } from '@testing-library/react';
import { ToastProvider, useToast } from './toast';

function Trigger() {
  const { toast } = useToast();
  return <button onClick={() => toast({ title: 'Заявка отправлена', variant: 'success' })}>go</button>;
}

afterEach(() => {
  jest.useRealTimers();
});

it('shows a toast and auto-dismisses it', () => {
  // Время двигает тест, а не машина. Раньше здесь ждали настоящие пятьдесят миллисекунд, и на
  // загруженной машине (три дорожки ворот разом) таймеры happy-dom голодали по двадцать секунд —
  // тест краснел, ничего не сломав. Сломанное автоскрытие всё так же покраснеет: часы двигаются,
  // а тост не исчезает.
  jest.useFakeTimers();
  render(
    <ToastProvider autoDismissMs={50}>
      <Trigger />
    </ToastProvider>
  );
  fireEvent.click(screen.getByText('go'));

  expect(screen.getByText('Заявка отправлена')).toBeInTheDocument();

  act(() => { jest.advanceTimersByTime(50); });

  expect(screen.queryByText('Заявка отправлена')).not.toBeInTheDocument();
});
