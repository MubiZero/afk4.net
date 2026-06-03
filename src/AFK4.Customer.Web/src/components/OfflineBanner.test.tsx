import { it, expect } from 'bun:test';
import { render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { OfflineBanner } from './OfflineBanner';

it('renders nothing when online', () => {
  const { container } = render(<I18nProvider><OfflineBanner online={true} /></I18nProvider>);
  expect(container).toBeEmptyDOMElement();
});

it('shows an offline message when offline', () => {
  render(<I18nProvider><OfflineBanner online={false} /></I18nProvider>);
  expect(screen.getByRole('status')).toHaveTextContent(/офлайн/i);
});
