import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect } from 'vitest';
import { Textarea } from './textarea';

it('renders a textarea and forwards value/onChange', () => {
  const values: string[] = [];
  render(<Textarea aria-label="note" value="" onChange={e => values.push(e.target.value)} />);
  const el = screen.getByRole('textbox', { name: 'note' });
  expect(el.tagName).toBe('TEXTAREA');
  fireEvent.change(el, { target: { value: 'hello' } });
  expect(values).toEqual(['hello']);
});

it('merges custom className', () => {
  render(<Textarea aria-label="note" className="custom-x" />);
  expect(screen.getByRole('textbox', { name: 'note' }).className).toContain('custom-x');
});
