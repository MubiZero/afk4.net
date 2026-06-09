import { describe, expect, it } from 'bun:test';
import { createTranslator } from '@afk4/i18n';
import { billingLabel, matchesLifecycleScope } from './operatorHelpers';
import type { OperatorAuthSession } from './authClient';
import type { SessionLifecycleChangedDto } from './operatorRealtime';

const t = createTranslator('ru');

describe('billingLabel', () => {
  // Every billing token the floor-map data layer can emit must localize to a real
  // label — none may silently fall through to "not set".
  it.each([
    ['Wallet', 'Депозит'],
    ['Package', 'Пакет'],
    ['Постоплата', 'Постоплата'],
    ['Fast guest', 'Гость'],
    ['Открытый счёт', 'Открытый счёт'],
    ['Cash', 'Наличные']
  ])('maps the %s token to its label', (token, expected) => {
    expect(billingLabel(token, t)).toBe(expected);
  });

  it('falls back to "not set" only for the explicit N/A token', () => {
    expect(billingLabel('N/A', t)).toBe('Не задан');
  });
});

describe('matchesLifecycleScope', () => {
  const session = { organizationId: '0C04D6C0-BFA8-4E26-9263-FC0D307D0F08' } as OperatorAuthSession;
  const change = (organizationId: string, branchId: string): SessionLifecycleChangedDto => ({
    organizationId,
    branchId,
    seatId: 'b',
    sessionId: 's',
    kind: 'started',
    state: 'active',
    version: 1,
    observedAtUtc: '2026-05-21T10:00:00Z'
  });

  it('matches case-insensitively on org and branch', () => {
    expect(matchesLifecycleScope(
      change('0c04d6c0-bfa8-4e26-9263-fc0d307d0f08', 'ACFC0212-967F-4D84-94BE-9003387B09C2'),
      session,
      'acfc0212-967f-4d84-94be-9003387b09c2'
    )).toBe(true);
  });

  it('rejects another organization or branch', () => {
    expect(matchesLifecycleScope(change('11111111-1111-1111-1111-111111111111', 'b'), session, 'b')).toBe(false);
    expect(matchesLifecycleScope(
      change('0c04d6c0-bfa8-4e26-9263-fc0d307d0f08', 'other-branch'),
      session,
      'acfc0212-967f-4d84-94be-9003387b09c2'
    )).toBe(false);
  });
});
