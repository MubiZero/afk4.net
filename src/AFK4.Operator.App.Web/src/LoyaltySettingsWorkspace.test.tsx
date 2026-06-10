import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { LoyaltySettingsWorkspace } from './LoyaltySettingsWorkspace';

function client(initial = { topUpEnabled: false, topUpPercentBasisPoints: 0, shopEnabled: false, shopPercentBasisPoints: 0 }) {
  const saved: unknown[] = [];
  return { saved, get: async () => initial, update: async (req: unknown) => { saved.push(req); return req as typeof initial; } };
}

function renderWorkspace(c: ReturnType<typeof client>) {
  render(<I18nProvider><LoyaltySettingsWorkspace client={c as never} /></I18nProvider>);
}

describe('LoyaltySettingsWorkspace', () => {
  afterEach(() => {
    cleanup();
  });

  it('loads settings and saves toggles + percent as basis points', async () => {
    const c = client();
    renderWorkspace(c);
    await waitFor(() => screen.getByLabelText(/кэшбэк с пополнений/i));
    fireEvent.click(screen.getByLabelText(/кэшбэк с пополнений/i));
    fireEvent.change(screen.getByLabelText(/процент с пополнений/i), { target: { value: '5' } });
    fireEvent.click(screen.getByRole('button', { name: /сохранить/i }));
    await waitFor(() => expect(c.saved).toEqual([
      { topUpEnabled: true, topUpPercentBasisPoints: 500, shopEnabled: false, shopPercentBasisPoints: 0 }
    ]));
  });

  it('rejects a percent above 100', async () => {
    const c = client();
    renderWorkspace(c);
    await waitFor(() => screen.getByLabelText(/процент с пополнений/i));
    fireEvent.change(screen.getByLabelText(/процент с пополнений/i), { target: { value: '150' } });
    fireEvent.click(screen.getByRole('button', { name: /сохранить/i }));
    await waitFor(() => screen.getByText(/процент должен быть от 0 до 100/i));
    expect(c.saved).toEqual([]);
  });
});
