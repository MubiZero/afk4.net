import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'bun:test';
import { LoyaltyScreen } from './LoyaltyScreen';
import type { ShellApi } from '../shellApi';

function api(over: Partial<ShellApi>): ShellApi {
  return { getLoyalty: async () => ({
    topUpEnabled: true, topUpPercentBasisPoints: 500, shopEnabled: false, shopPercentBasisPoints: 0,
    totalEarned: { currencyCode: 'TJS', minorUnits: 12345 }, recent: []
  }), ...over } as unknown as ShellApi;
}

describe('LoyaltyScreen', () => {
  it('shows the enabled top-up rate and total earned, hides the disabled shop rate', async () => {
    render(<LoyaltyScreen api={api({})} onDone={() => {}} />);
    await waitFor(() => screen.getByText(/5%/));
    expect(screen.getByText(/123[.,]45/)).toBeInTheDocument(); // 12345 minor -> 123.45
    expect(screen.queryByText(/магазин/i)).not.toBeInTheDocument();
  });

  it('shows an unavailable message when both sources are disabled', async () => {
    render(<LoyaltyScreen api={api({ getLoyalty: async () => ({
      topUpEnabled: false, topUpPercentBasisPoints: 0, shopEnabled: false, shopPercentBasisPoints: 0,
      totalEarned: { currencyCode: 'TJS', minorUnits: 0 }, recent: []
    }) })} onDone={() => {}} />);
    await waitFor(() => screen.getByText(/кэшбэк пока недоступен/i));
  });
});
