import { render, screen, fireEvent, cleanup } from '@testing-library/react';
import { afterEach, it, expect } from 'bun:test';
import { useState } from 'react';
import { I18nProvider } from '@afk4/i18n';
import { PanelModal } from './PanelModal';

afterEach(cleanup);

// Сравниваем имя сфокусированного элемента, а не сам узел: при падении узел печатается целиком
// и отчёт становится нечитаемым.
const focusedName = () => {
  const el = document.activeElement as HTMLElement | null;
  return el?.getAttribute('aria-label') ?? el?.textContent ?? null;
};

function Harness() {
  const [open, setOpen] = useState(false);
  return (
    <I18nProvider>
      <button type="button" onClick={() => setOpen(true)}>Рассчитать</button>
      {open && (
        <PanelModal title="Расчёт смены" onClose={() => setOpen(false)}>
          <input aria-label="Сумма" />
          <button type="button">Провести</button>
        </PanelModal>
      )}
    </I18nProvider>
  );
}

// Окно закрывает экран целиком: пока фокус снаружи, кассир табом ходит по кнопкам кассы,
// которые перекрыты окном и которых он не видит.
it('moves focus into the form, not onto the close button', () => {
  render(<Harness />);
  fireEvent.click(screen.getByRole('button', { name: 'Рассчитать' }));
  expect(focusedName()).toBe('Сумма');
});

it('keeps Tab inside the modal', () => {
  render(<Harness />);
  fireEvent.click(screen.getByRole('button', { name: 'Рассчитать' }));
  screen.getByRole('button', { name: 'Провести' }).focus();
  fireEvent.keyDown(document, { key: 'Tab' });
  expect(focusedName()).toBe('Отмена');
});

// Без возврата фокус улетал в начало документа: после каждого окна кассир пробирался табом
// обратно к тому месту, где работал.
it('returns focus to the control that opened it', () => {
  render(<Harness />);
  fireEvent.click(screen.getByRole('button', { name: 'Рассчитать' }));
  fireEvent.keyDown(document, { key: 'Escape' });
  expect(focusedName()).toBe('Рассчитать');
});
