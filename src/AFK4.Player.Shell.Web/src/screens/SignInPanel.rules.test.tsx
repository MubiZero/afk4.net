import { afterEach, describe, expect, it } from 'bun:test';
import { cleanup, render, screen } from '@testing-library/react';
import { ShellI18nProvider } from '../i18n/ShellI18nProvider';
import { SignInPanel } from './SignInPanel';
import { devScenarioState } from '../host/devHost';

afterEach(cleanup);

describe('SignInPanel — club rules', () => {
  it('shows the club rules beside the sign-in when the club wrote them', () => {
    render(
      <ShellI18nProvider>
        <SignInPanel state={{ ...devScenarioState('idle'), clubRules: 'Не есть за ПК.\\nНаушники — у администратора.' }} onClose={() => {}} />
      </ShellI18nProvider>
    );

    expect(screen.getByText('Правила клуба')).toBeInTheDocument();
    expect(screen.getByText(/Не есть за ПК/)).toBeInTheDocument();
  });

  it('says nothing about rules when there are none', () => {
    render(
      <ShellI18nProvider>
        <SignInPanel state={devScenarioState('idle')} onClose={() => {}} />
      </ShellI18nProvider>
    );

    expect(screen.queryByText('Правила клуба')).toBeNull();
  });
});

describe('IdleScreen — idle shutdown', () => {
  it('counts down to the idle shutdown and says how to keep the PC on', async () => {
    const { IdleScreen } = await import('./IdleScreen');
    const at = new Date(Date.now() + 45_000).toISOString();
    render(
      <ShellI18nProvider>
        <IdleScreen state={{ ...devScenarioState('idle'), idleShutdownAtUtc: at }} />
      </ShellI18nProvider>
    );

    expect(screen.getByRole('status').textContent).toMatch(/выключится через 4[45] с/);
  });
});
