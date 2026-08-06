import { render, screen } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { SupportModeBanner } from './SupportModeBanner';
import type { SupportSession } from './supportSession';

function session(overrides: Partial<SupportSession> = {}): SupportSession {
  return {
    sessionToken: 's1',
    organizationId: 'o1',
    organizationName: 'Кибер Арена',
    reason: 'Смена не открывается',
    expiresAtUtc: new Date(Date.now() + 5 * 60_000).toISOString(),
    writableAreas: ['branch-settings'],
    branches: [],
    ...overrides
  };
}

it('показывает клуб, причину и остаток времени', () => {
  render(
    <I18nProvider>
      <SupportModeBanner session={session()} onExit={mock()} />
    </I18nProvider>
  );

  expect(screen.getByText(/Кибер Арена/)).toBeDefined();
  expect(screen.getByText(/Смена не открывается/)).toBeDefined();
  expect(screen.getByRole('button', { name: 'Выйти из режима' })).toBeDefined();
});

it('зовёт onExit по клику на кнопку выхода', () => {
  const onExit = mock();
  render(
    <I18nProvider>
      <SupportModeBanner session={session()} onExit={onExit} />
    </I18nProvider>
  );

  screen.getByRole('button', { name: 'Выйти из режима' }).click();

  expect(onExit).toHaveBeenCalledTimes(1);
});

it('считает истёкший грант нулём вместо NaN (fail-safe)', () => {
  render(
    <I18nProvider>
      <SupportModeBanner session={session({ expiresAtUtc: 'not-a-date' })} onExit={mock()} />
    </I18nProvider>
  );

  expect(screen.getByText(/00:00/)).toBeDefined();
});
