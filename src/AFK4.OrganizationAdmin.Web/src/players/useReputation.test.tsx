import { afterEach, describe, expect, it, mock } from 'bun:test';
import { act, cleanup, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { PlatformApiError } from '../platformApi';

const lookupReputation = mock(async (_branchId: string, _phone: string) => ({
  networkVisits: 14, networkNoShows: 1, networkBanned: false, calculatedAtUtc: '2026-08-20T00:00:00Z'
}));

const reputationForPerson = mock(async (_branchId: string, _platformPersonId: string) => ({
  networkVisits: 3, networkNoShows: 0, networkBanned: false, calculatedAtUtc: '2026-08-20T00:00:00Z'
}));

const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../operatorHelpers', () => ({
  ...actual,
  createAuthenticatedOperatorClients: () => ({ players: { lookupReputation, reputationForPerson } })
}));

const { useReputation, clearReputationAnswers } = await import('./useReputation');
const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o1' }, branchId: 'b1' } as never;

function Probe({ phone, personId = null }: { phone: string; personId?: string | null }) {
  const { state, ask } = useReputation(backend, phone, personId);
  return (
    <div>
      <span data-testid="status">{state.status}</span>
      <span data-testid="visits">{state.status === 'ready' ? state.reputation.networkVisits : ''}</span>
      <span data-testid="detail">{state.status === 'failed' ? state.detail : ''}</span>
      <button type="button" onClick={ask}>ask</button>
    </div>
  );
}

const renderProbe = (phone: string, personId: string | null = null) =>
  render(<I18nProvider initialLocale="ru"><Probe phone={phone} personId={personId} /></I18nProvider>);

afterEach(() => {
  clearReputationAnswers();
  lookupReputation.mockClear();
  reputationForPerson.mockClear();
  cleanup();
});

describe('useReputation', () => {
  it('не спрашивает сеть, пока карточку просто открыли: аудит не должен полниться пролистанными', async () => {
    renderProbe('93 738 00 70');
    expect(screen.getByTestId('status')).toHaveTextContent('idle');
    await act(async () => {});
    expect(lookupReputation).not.toHaveBeenCalled();
  });

  it('спрашивает точный номер по нажатию', async () => {
    renderProbe('93 738 00 70');
    act(() => { screen.getByRole('button').click(); });
    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('ready'));
    expect(lookupReputation).toHaveBeenCalledWith('b1', '+992937380070');
    expect(screen.getByTestId('visits')).toHaveTextContent('14');
  });

  it('второй раз тот же номер сеть не тревожит: числа суточные, а запись в аудите была бы новая', async () => {
    const { unmount } = renderProbe('93 738 00 70');
    act(() => { screen.getByRole('button').click(); });
    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('ready'));
    unmount();

    renderProbe('93 738 00 70');
    expect(screen.getByTestId('status')).toHaveTextContent('ready');
    expect(lookupReputation).toHaveBeenCalledTimes(1);
  });

  it('ответ на прошлого человека не садится в карточку следующего', async () => {
    let release: (() => void) | null = null;
    lookupReputation.mockImplementationOnce(async () => {
      await new Promise<void>((resolve) => { release = resolve; });
      return { networkVisits: 99, networkNoShows: 7, networkBanned: true, calculatedAtUtc: '2026-08-20T00:00:00Z' };
    });

    const view = render(<I18nProvider initialLocale="ru"><Probe phone="93 738 00 70" /></I18nProvider>);
    act(() => { screen.getByRole('button').click(); });
    expect(screen.getByTestId('status')).toHaveTextContent('loading');

    // Админ перещёлкнул на другого клиента, пока сеть ещё думает.
    view.rerender(<I18nProvider initialLocale="ru"><Probe phone="90 111 22 33" /></I18nProvider>);
    expect(screen.getByTestId('status')).toHaveTextContent('idle');

    await act(async () => { release?.(); });
    expect(screen.getByTestId('status')).toHaveTextContent('idle');
    expect(screen.getByTestId('visits')).toHaveTextContent('');
  });

  it('неполный номер спрашивать нечем', async () => {
    renderProbe('93 738');
    expect(screen.getByTestId('status')).toHaveTextContent('noPhone');
    act(() => { screen.getByRole('button').click(); });
    await act(async () => {});
    expect(lookupReputation).not.toHaveBeenCalled();
    expect(reputationForPerson).not.toHaveBeenCalled();
  });

  it('без номера спрашивает по личности платформы: у карточки из приложения номера может не быть', async () => {
    renderProbe('', 'p-1');
    expect(screen.getByTestId('status')).toHaveTextContent('idle');
    act(() => { screen.getByRole('button').click(); });
    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('ready'));
    expect(reputationForPerson).toHaveBeenCalledWith('b1', 'p-1');
    expect(lookupReputation).not.toHaveBeenCalled();
    expect(screen.getByTestId('visits')).toHaveTextContent('3');
  });

  it('номер сильнее личности: он же единственный ключ к карточке, заведённой стойкой до общего котла', async () => {
    renderProbe('93 738 00 70', 'p-1');
    act(() => { screen.getByRole('button').click(); });
    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('ready'));
    expect(lookupReputation).toHaveBeenCalledWith('b1', '+992937380070');
    expect(reputationForPerson).not.toHaveBeenCalled();
  });

  it('ни номера, ни личности — спрашивать нечего', async () => {
    renderProbe('', null);
    expect(screen.getByTestId('status')).toHaveTextContent('noPhone');
    act(() => { screen.getByRole('button').click(); });
    await act(async () => {});
    expect(reputationForPerson).not.toHaveBeenCalled();
  });

  it('упёршийся в лимит маршрута отказ объясняется человеческими словами', async () => {
    lookupReputation.mockImplementationOnce(async () => { throw new PlatformApiError('too many', 429, 'Too Many Requests', ''); });
    renderProbe('93 738 00 70');
    act(() => { screen.getByRole('button').click(); });
    await waitFor(() => expect(screen.getByTestId('status')).toHaveTextContent('failed'));
    expect(screen.getByTestId('detail')).toHaveTextContent('Слишком много запросов подряд — подождите минуту.');
  });
});
