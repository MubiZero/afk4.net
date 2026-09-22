// src/components/ui/dialog.test.tsx
import { render, screen, fireEvent, cleanup } from '@testing-library/react';
import { afterEach, it, expect, mock } from 'bun:test';
import { useState } from 'react';
import { Dialog } from './dialog';
import { I18nProvider } from '@/i18n/I18nProvider';

afterEach(cleanup);

// Сравниваем имя сфокусированного элемента, а не сам узел: при падении узел печатается
// целиком и отчёт становится нечитаемым.
const focusedName = () => {
  const el = document.activeElement as HTMLElement | null;
  return el?.getAttribute('aria-label') ?? el?.textContent ?? null;
};

function Harness() {
  const [open, setOpen] = useState(false);
  return (
    <I18nProvider>
      <button type="button" onClick={() => setOpen(true)}>Открыть</button>
      <Dialog open={open} title="Смена тарифа" onClose={() => setOpen(false)}>
        <input aria-label="Название" />
        <button type="button">Сохранить</button>
      </Dialog>
    </I18nProvider>
  );
}

// Окно забирает экран целиком. Пока фокус остаётся снаружи, человек с клавиатурой ходит табом
// по кнопкам, которые перекрыты окном и которых он не видит.
it('moves focus into the dialog content, not onto the close button', () => {
  render(<Harness />);
  fireEvent.click(screen.getByRole('button', { name: 'Открыть' }));
  expect(focusedName()).toBe('Название');
});

// Таб с последнего элемента уходил на страницу под окном; теперь он замыкается на первый
// элемент самого окна — им оказывается кнопка закрытия в шапке.
it('keeps Tab inside the dialog', () => {
  render(<Harness />);
  fireEvent.click(screen.getByRole('button', { name: 'Открыть' }));
  const save = screen.getByRole('button', { name: 'Сохранить' });
  save.focus();
  fireEvent.keyDown(document, { key: 'Tab' });
  expect(focusedName()).toBe('Закрыть');
});

// Без возврата фокус улетал в начало документа, и до места, где человек работал, приходилось
// добираться табом заново — после каждого подтверждения.
it('returns focus to the control that opened it', () => {
  render(<Harness />);
  const opener = screen.getByRole('button', { name: 'Открыть' });
  fireEvent.click(opener);
  fireEvent.keyDown(document, { key: 'Escape' });
  expect(focusedName()).toBe('Открыть');
});

it('still closes on Escape', () => {
  const onClose = mock();
  render(
    <I18nProvider>
      <Dialog open title="Смена тарифа" onClose={onClose}>
        <input aria-label="Название" />
      </Dialog>
    </I18nProvider>
  );
  fireEvent.keyDown(document, { key: 'Escape' });
  expect(onClose).toHaveBeenCalled();
});
