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

  // Проверка появления — синхронная: тост рисуется тем же кликом, а `findByText` уступает
  // событийный цикл, и на загруженной машине таймер успевал убрать тост до первого опроса.
  expect(screen.getByText('Заявка отправлена')).toBeInTheDocument();
  // Запас к пятидесяти миллисекундам таймера — не про ожидаемое время, а про занятую машину:
  // потолок теста в bun всё равно пять секунд, и сломанное автоскрытие упрётся в него, а не
  // проскочит по случайности.
  await waitFor(
    () => expect(screen.queryByText('Заявка отправлена')).not.toBeInTheDocument(),
    { timeout: 2000 });
});
