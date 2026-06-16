import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ContextPanel } from './ContextPanel';

describe('ContextPanel', () => {
  it('renders the panel body and optional title', () => {
    render(
      <I18nProvider>
        <ContextPanel title="Details">
          <div>PANEL BODY</div>
        </ContextPanel>
      </I18nProvider>
    );

    expect(screen.getByText('PANEL BODY')).toBeDefined();
    expect(screen.getByText('Details')).toBeDefined();
  });

  it('renders without a title', () => {
    render(
      <I18nProvider>
        <ContextPanel>
          <div>PANEL BODY</div>
        </ContextPanel>
      </I18nProvider>
    );

    expect(screen.getByText('PANEL BODY')).toBeDefined();
  });
});
