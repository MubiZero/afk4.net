import { it, expect } from 'bun:test';
import { render, screen, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { LanguageToggle } from './LanguageToggle';

function renderToggle() {
  return render(<I18nProvider><LanguageToggle /></I18nProvider>);
}

it('shows the current locale and cycles ru → en → tg → ru', () => {
  renderToggle();
  const btn = screen.getByRole('button');
  expect(btn).toHaveTextContent('RU');
  fireEvent.click(btn);
  expect(btn).toHaveTextContent('EN');
  fireEvent.click(btn);
  expect(btn).toHaveTextContent('TG');
  fireEvent.click(btn);
  expect(btn).toHaveTextContent('RU');
});

it('persists the chosen locale to localStorage', () => {
  renderToggle();
  fireEvent.click(screen.getByRole('button')); // ru → en
  expect(localStorage.getItem('afk4.locale')).toBe('en');
});
