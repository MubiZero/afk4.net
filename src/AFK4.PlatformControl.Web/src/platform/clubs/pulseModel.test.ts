import { describe, expect, it } from 'bun:test';
import { alertRank, resolveDensity, selectView } from './pulseModel';
import type { PulseOrganization } from '@/api/types';

const org = (over: Partial<PulseOrganization>): PulseOrganization => ({
  organizationId: 'o1',
  name: 'Cyber Zone',
  status: 'active',
  planCode: 'pro',
  subscriptionStatus: 'active',
  alertLevel: 'normal',
  outstandingMinorUnits: 0,
  currencyCode: 'TJS',
  alerts: [],
  clubs: [],
  ...over
});

describe('pulseModel', () => {
  it('ranks critical above attention above normal', () => {
    expect(alertRank('critical')).toBeGreaterThan(alertRank('attention'));
    expect(alertRank('attention')).toBeGreaterThan(alertRank('normal'));
  });

  it('puts the loudest alert first in the "now" view', () => {
    const list = [
      org({ organizationId: 'quiet', alertLevel: 'normal', name: 'A' }),
      org({ organizationId: 'loud', alertLevel: 'critical', name: 'Z' })
    ];
    expect(selectView(list, 'now').map(item => item.organizationId)).toEqual(['loud', 'quiet']);
  });

  it('sorts the "all" view alphabetically regardless of alerts', () => {
    const list = [
      org({ organizationId: 'z', name: 'Ярд', alertLevel: 'critical' }),
      org({ organizationId: 'a', name: 'Арена' })
    ];
    expect(selectView(list, 'all').map(item => item.organizationId)).toEqual(['a', 'z']);
  });

  it('keeps only debtors in the "debt" view', () => {
    const list = [
      org({ organizationId: 'paid' }),
      org({ organizationId: 'owing', outstandingMinorUnits: 140000 })
    ];
    expect(selectView(list, 'debt').map(item => item.organizationId)).toEqual(['owing']);
  });

  it('switches to dense rows once there are more than five clients', () => {
    expect(resolveDensity(5)).toBe('roomy');
    expect(resolveDensity(6)).toBe('dense');
  });
});
