// src/components/ui/switch.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { Switch } from './switch';

it('toggles via onCheckedChange when clicked', () => {
  const onCheckedChange = mock();
  render(<Switch aria-label="approval" checked={false} onCheckedChange={onCheckedChange} />);
  fireEvent.click(screen.getByRole('switch', { name: 'approval' }));
  expect(onCheckedChange).toHaveBeenCalledWith(true);
});

it('is disabled when disabled prop is set', () => {
  render(<Switch aria-label="approval" checked={false} disabled onCheckedChange={mock()} />);
  expect(screen.getByRole('switch', { name: 'approval' })).toBeDisabled();
});
