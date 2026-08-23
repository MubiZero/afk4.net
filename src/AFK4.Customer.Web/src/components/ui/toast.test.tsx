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
  // Запасы здесь не про ожидаемое время (таймеру хватает пятидесяти миллисекунд), а про
  // занятую машину: когда рядом идут сборки, таймеры happy-dom голодают секундами. Сломанное
  // автоскрытие упрётся в тот же потолок и покраснеет, просто не по случайности.
  await waitFor(
    () => expect(screen.queryByText('Заявка отправлена')).not.toBeInTheDocument(),
    { timeout: 10_000 });
}, 20_000);
