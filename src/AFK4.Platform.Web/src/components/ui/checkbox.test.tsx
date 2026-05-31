// src/components/ui/checkbox.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { Checkbox } from './checkbox';

it('emits onCheckedChange(true) when an unchecked box is clicked', () => {
  const onCheckedChange = mock();
  render(<Checkbox aria-label="role" checked={false} onCheckedChange={onCheckedChange} />);
  fireEvent.click(screen.getByRole('checkbox', { name: 'role' }));
  expect(onCheckedChange).toHaveBeenCalledWith(true);
});
