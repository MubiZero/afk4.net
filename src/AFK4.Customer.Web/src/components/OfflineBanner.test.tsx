import { it, expect } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { OfflineBanner } from './OfflineBanner';

it('renders nothing when online', () => {
  const { container } = render(<OfflineBanner online={true} />);
  expect(container).toBeEmptyDOMElement();
});

it('shows an offline message when offline', () => {
  render(<OfflineBanner online={false} />);
  expect(screen.getByRole('status')).toHaveTextContent(/офлайн/i);
});
