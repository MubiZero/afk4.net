import { describe, expect, it } from 'bun:test';
import { createTranslator } from '@afk4/i18n';
import { billingLabel, criticalAlertSources, matchesLifecycleScope, matchesMapFilter } from './operatorHelpers';
import type { OperatorAuthSession } from './authClient';
import type { SeatSummary, SeatTone } from './operatorData';
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

describe('criticalAlertSources', () => {
  const toneSeat = (tone: SeatTone): SeatSummary => ({
    id: tone, zone: 'Зал A', name: tone, tone, stateLabel: tone, player: '—',
    remaining: '—', billing: 'N/A', device: 'Device', command: 'Idle', app: 'Shell'
  });
  const seats: SeatSummary[] = [
    toneSeat('offline'), toneSeat('offline'),
    toneSeat('blocking'),
    toneSeat('service'),
    toneSeat('ready'), toneSeat('active'), toneSeat('warning')
  ];

  it('counts only the hard critical tones, each routed to a precise map filter', () => {
    const sources = criticalAlertSources(seats, t);
    const byId = Object.fromEntries(sources.map((source) => [source.id, source]));
    expect(byId.offline.count).toBe(2);
    expect(byId.offline.filterId).toBe('offline');
    expect(byId.blocking.count).toBe(1);
    expect(byId.blocking.filterId).toBe('blocking');
    expect(byId.service.count).toBe(1);
    expect(byId.service.filterId).toBe('service');
  });

  it('omits a critical type with no seats instead of showing a fake zero', () => {
    const sources = criticalAlertSources([toneSeat('ready'), toneSeat('offline')], t);
    expect(sources.map((source) => source.id)).toEqual(['offline']);
  });

  it('each source filter actually selects its own tone on the map', () => {
    const sources = criticalAlertSources(seats, t);
    for (const source of sources) {
      const matched = seats.filter((seat) => matchesMapFilter(seat, source.filterId));
      expect(matched.length).toBe(source.count);
    }
  });
});
