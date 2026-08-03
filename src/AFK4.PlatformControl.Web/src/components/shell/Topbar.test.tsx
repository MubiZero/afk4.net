import { render, screen } from '@testing-library/react';
import { expect, it } from 'bun:test';
import { Topbar } from './Topbar';

it('places global search in the operational top bar', () => {
  render(<Topbar subtitle="" screenTitle="Обзор" menuLabel="Открыть меню" onOpenSidebar={() => {}} search={<input aria-label="Поиск по платформе" />} />);
  expect(screen.getByText('Обзор')).toBeVisible();
  expect(screen.getByRole('button', { name: 'Открыть меню' })).toBeInTheDocument();
  expect(screen.getByRole('textbox', { name: 'Поиск по платформе' })).toBeVisible();
});
