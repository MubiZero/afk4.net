// src/club/settings/OperatorsTable.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { I18nProvider } from '@/i18n/I18nProvider';
import { OperatorsTable } from './OperatorsTable';
import type { OperatorRow } from './settingsModel';

const rows: OperatorRow[] = [
  { staffUserId: 's1', organizationId: 'org', userName: 'ANN', displayName: 'Анна', isActive: true, roleNames: ['branch_manager'] }
];

function setup(onSelect = vi.fn(), data = rows) {
  render(<I18nProvider><OperatorsTable rows={data} emptyMessage="Пусто" onSelect={onSelect} /></I18nProvider>);
  return { onSelect };
}

it('renders an operator row with localized role and active badge', () => {
  setup();
  expect(screen.getByText('Анна')).toBeInTheDocument();
  expect(screen.getByText('Управляющий')).toBeInTheDocument();
  expect(screen.getByText('Активен')).toBeInTheDocument();
});

it('calls onSelect when a row is clicked', () => {
  const { onSelect } = setup();
  fireEvent.click(screen.getByText('Анна'));
  expect(onSelect).toHaveBeenCalledWith(rows[0]);
});

it('shows the empty message when there are no operators', () => {
  setup(vi.fn(), []);
  expect(screen.getByText('Пусто')).toBeInTheDocument();
});
