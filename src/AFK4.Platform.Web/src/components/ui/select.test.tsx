import { it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { Select, SelectTrigger, SelectValue, SelectContent, SelectItem } from './select';

it('renders the trigger with a placeholder', () => {
  render(
    <Select>
      <SelectTrigger aria-label="seat"><SelectValue placeholder="Выберите место" /></SelectTrigger>
      <SelectContent>
        <SelectItem value="s1">Зона A · Место 1</SelectItem>
      </SelectContent>
    </Select>
  );
  expect(screen.getByLabelText('seat')).toBeInTheDocument();
  expect(screen.getByText('Выберите место')).toBeInTheDocument();
});
