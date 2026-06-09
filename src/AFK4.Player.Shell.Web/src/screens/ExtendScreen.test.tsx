import { render, screen, waitFor } from '@testing-library/react';
import { describe, expect, it } from 'bun:test';
import { ExtendScreen } from './ExtendScreen';
import { ApiError, type ShellApi } from '../shellApi';

const tariff = { tariffId: 't', tariffVersionId: 'tv', name: 'Standard', tariffRuleVersionId: 'trv1',
  versionNumber: 1, currencyCode: 'TJS', pricePerMinuteMinorUnits: 100, minimumBillableMinutes: 1,
  roundingIncrementMinutes: 1, effectiveFromUtc: '' };

function api(extend: ShellApi['extendSession']): ShellApi {
  return { listTariffs: async () => [tariff], listPackages: async () => [], createTopUpIntent: async () => ({} as any),
    getTopUpIntents: async () => [], extendSession: extend } as ShellApi;
}

describe('ExtendScreen', () => {
  it('extends with the selected tariff and minutes', async () => {
    let captured: any;
    render(<ExtendScreen api={api(async (s, req) => { captured = { s, req }; return {}; })}
      branchId="b" sessionId="s1" onExtended={() => {}} onConflict={() => {}} />);
    const { fireEvent } = await import('@testing-library/react');
    await waitFor(() => screen.getByText('Standard'));
    fireEvent.click(screen.getByText('Standard'));
    fireEvent.change(screen.getByLabelText(/minutes|минут/i), { target: { value: '30' } });
    fireEvent.click(screen.getByRole('button', { name: /extend|продлить/i }));
    await waitFor(() => expect(captured.req).toMatchObject({ additionalMinutes: 30, tariffRuleVersionId: 'trv1' }));
  });

  it('on 409 calls onConflict', async () => {
    let conflicted = false;
    render(<ExtendScreen api={api(async () => { throw new ApiError(409, 'conflict'); })}
      branchId="b" sessionId="s1" onExtended={() => {}} onConflict={() => { conflicted = true; }} />);
    const { fireEvent } = await import('@testing-library/react');
    await waitFor(() => screen.getByText('Standard'));
    fireEvent.click(screen.getByText('Standard'));
    fireEvent.change(screen.getByLabelText(/minutes|минут/i), { target: { value: '30' } });
    fireEvent.click(screen.getByRole('button', { name: /extend|продлить/i }));
    await waitFor(() => expect(conflicted).toBe(true));
  });
});
