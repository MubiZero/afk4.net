import { it, expect } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { App } from './App';

it('renders the app brand mark', () => {
  render(<App />);
  expect(screen.getByText('AFK4')).toBeInTheDocument();
});
