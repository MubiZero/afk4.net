import { it, expect } from 'bun:test';
import { render, screen, fireEvent } from '@testing-library/react';
import { useState } from 'react';
import { Input } from './input';

it('renders and is controllable', () => {
  function Host() {
    const [v, setV] = useState('');
    return <Input aria-label="name" value={v} onChange={e => setV(e.target.value)} />;
  }
  render(<Host />);
  const input = screen.getByLabelText('name') as HTMLInputElement;
  fireEvent.change(input, { target: { value: 'ПК-7' } });
  expect(input.value).toBe('ПК-7');
});
