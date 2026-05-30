// src/components/ui/checkbox.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { Checkbox } from './checkbox';

it('emits onCheckedChange(true) when an unchecked box is clicked', () => {
  const onCheckedChange = vi.fn();
  render(<Checkbox aria-label="role" checked={false} onCheckedChange={onCheckedChange} />);
  fireEvent.click(screen.getByRole('checkbox', { name: 'role' }));
  expect(onCheckedChange).toHaveBeenCalledWith(true);
});
