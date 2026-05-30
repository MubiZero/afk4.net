// src/components/ui/switch.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, vi } from 'vitest';
import { Switch } from './switch';

it('toggles via onCheckedChange when clicked', () => {
  const onCheckedChange = vi.fn();
  render(<Switch aria-label="approval" checked={false} onCheckedChange={onCheckedChange} />);
  fireEvent.click(screen.getByRole('switch', { name: 'approval' }));
  expect(onCheckedChange).toHaveBeenCalledWith(true);
});

it('is disabled when disabled prop is set', () => {
  render(<Switch aria-label="approval" checked={false} disabled onCheckedChange={vi.fn()} />);
  expect(screen.getByRole('switch', { name: 'approval' })).toBeDisabled();
});
