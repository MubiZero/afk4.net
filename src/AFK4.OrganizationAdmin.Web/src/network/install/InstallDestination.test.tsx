import { describe, it, expect, mock, afterEach } from 'bun:test';
import { render, screen, cleanup, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';

afterEach(() => cleanup());

mock.module('../../operatorHelpers', () => ({
  createAuthenticatedOperatorClients: () => ({
    orgBranches: { getOwnerBranches: mock(async () => [{ branchId: 'b1', name: 'Центр' }]) }
  })
}));

function backend(setupInstallerUrl?: string) {
  return { config: { platformBaseUrl: 'x', currencyCode: 'TJS', setupInstallerUrl }, session: { organizationId: 'org', accessToken: 't' }, branchId: 'b1' };
}

describe('InstallDestination', () => {
  it('enables download when url is configured', async () => {
    const { InstallDestination } = await import('./InstallDestination');
    render(<I18nProvider initialLocale="ru"><InstallDestination backend={backend('https://dl/afk4.exe') as never} /></I18nProvider>);
    const link = await screen.findByRole('link', { name: /скач/i });
    expect(link).toHaveAttribute('href', 'https://dl/afk4.exe');
  });

  it('shows an honest "obtain from IT" note when url is missing', async () => {
    const { InstallDestination } = await import('./InstallDestination');
    render(<I18nProvider initialLocale="ru"><InstallDestination backend={backend(undefined) as never} /></I18nProvider>);
    await waitFor(() => expect(screen.getByText(/IT|релиз/i)).toBeInTheDocument());
    expect(screen.queryByRole('link', { name: /скач/i })).toBeNull();
  });
});
