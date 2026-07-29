import { render, screen } from '@testing-library/react';
import { expect, it } from 'bun:test';
import { Topbar } from './Topbar';

it('places global search in the operational top bar', () => {
  render(<Topbar subtitle="" screenTitle="Обзор" onOpenSidebar={() => {}} search={<input aria-label="Поиск по платформе" />} />);
  expect(screen.getByText('Обзор')).toBeVisible();
  expect(screen.getByRole('textbox', { name: 'Поиск по платформе' })).toBeVisible();
});
