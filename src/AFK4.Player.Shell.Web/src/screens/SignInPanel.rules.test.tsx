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
