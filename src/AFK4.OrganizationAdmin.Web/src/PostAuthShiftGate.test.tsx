import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { PostAuthShiftGate } from './PostAuthShiftGate';
import type { PostAuthShiftGateController } from './usePostAuthShiftGate';

afterEach(cleanup);

const ORG_ID = '0c04d6c0-bfa8-4e26-9263-fc0d307d0f08';

function createController(
  overrides: Partial<PostAuthShiftGateController> = {}
): PostAuthShiftGateController {
  return {
    status: 'required',
    error: null,
    failureKind: null,
    retry: mock(() => {}),
    openShift: mock(async () => {}),
    ...overrides
  };
}

const backend = { config: { platformBaseUrl: 'x' }, session: { accessToken: 't', organizationId: ORG_ID }, branchId: 'b1' } as never;

function closedShift(countedMinorUnits: number | null) {
  return {
    shifts: [{
      shiftId: 's-prev', state: 'closed',
      cash: { starting: money(0), expected: money(countedMinorUnits ?? 0), counted: countedMinorUnits === null ? null : money(countedMinorUnits), difference: null }
    }],
    limit: 1
  } as never;
}

function money(minorUnits: number) {
  return { currencyCode: 'TJS', minorUnits };
}

function renderGate(
  controller: PostAuthShiftGateController,
  onSignOut = mock(() => {}),
  shiftHistory?: { history: (branchId: string, limit?: number) => Promise<never> }
) {
  render(
    <I18nProvider>
      <PostAuthShiftGate
        controller={controller}
        backend={shiftHistory ? backend : null}
        shiftHistory={shiftHistory}
        organizationId={ORG_ID}
        currencyCode="TJS"
        onSignOut={onSignOut}
      />
    </I18nProvider>
  );
  return { onSignOut };
}

describe('PostAuthShiftGate', () => {
  it('blocks with a dedicated loading state during the authoritative check', () => {
    renderGate(createController({ status: 'checking' }));

    expect(screen.getByRole('status')).toHaveTextContent('Проверяем текущую смену…');
    expect(screen.queryByRole('button', { name: 'Открыть смену' })).not.toBeInTheDocument();
  });

  it('submits starting cash with an empty note unless the operator writes one, without a dismiss action', async () => {
    const controller = createController();
    renderGate(controller);

    expect(screen.getByRole('heading', { name: 'Откройте смену' })).toBeInTheDocument();
    expect(document.querySelector('[data-dialog-close]')).toBeNull();
    fireEvent.change(screen.getByLabelText('Старт наличных'), { target: { value: '100.50' } });
    fireEvent.click(screen.getByRole('button', { name: 'Открыть смену' }));

    await waitFor(() => expect(controller.openShift).toHaveBeenCalledWith(expect.objectContaining({
      organizationId: ORG_ID,
      startingCash: { currencyCode: 'TJS', minorUnits: 10050 },
      openingNote: ''
    })));
  });

  // Приёмка 30.09.2026: «Старт наличных» был пуст, хотя прошлая смена закрылась с посчитанной
  // суммой; комментарий «Утренняя смена» подставлялся в любое время суток.
  it('подставляет в «Старт наличных» остаток прошлой смены', async () => {
    const history = mock(async () => closedShift(324500));
    renderGate(createController(), undefined, { history });

    await waitFor(() => expect(screen.getByLabelText('Старт наличных')).toHaveValue('3245'));
    expect(history).toHaveBeenCalledWith('b1', 1);
    expect(screen.getByLabelText('Комментарий')).toHaveValue('');
  });

  it('не затирает сумму, которую оператор уже ввёл, когда остаток приехал позже', async () => {
    let resolve: (value: never) => void = () => {};
    const history = mock(() => new Promise<never>((r) => { resolve = r; }));
    renderGate(createController(), undefined, { history });

    fireEvent.change(screen.getByLabelText('Старт наличных'), { target: { value: '500' } });
    resolve(closedShift(324500));
    await waitFor(() => expect(history).toHaveBeenCalled());
    await new Promise((r) => setTimeout(r, 0));

    expect(screen.getByLabelText('Старт наличных')).toHaveValue('500');
  });

  it('без остатка (нет прав на отчёты, смены не было) оставляет ноль и не падает', async () => {
    const history = mock(async () => { throw new Error('403'); });
    renderGate(createController(), undefined, { history });

    await waitFor(() => expect(history).toHaveBeenCalled());
    expect(screen.getByLabelText('Старт наличных')).toHaveValue('0');
  });

  it('rejects malformed or negative starting cash locally', () => {
    const controller = createController();
    renderGate(controller);

    fireEvent.change(screen.getByLabelText('Старт наличных'), { target: { value: '-1' } });
    fireEvent.click(screen.getByRole('button', { name: 'Открыть смену' }));

    expect(screen.getByRole('alert')).toHaveTextContent('Введите сумму от 0 и выше.');
    expect(controller.openShift).not.toHaveBeenCalled();
  });

  it('offers retry for a failed check and always permits sign-out', () => {
    const controller = createController({
      status: 'failed',
      error: 'Сервер недоступен',
      failureKind: 'check'
    });
    const { onSignOut } = renderGate(controller);

    expect(screen.getByRole('alert')).toHaveTextContent('Сервер недоступен');
    fireEvent.click(screen.getByRole('button', { name: 'Проверить снова' }));
    fireEvent.click(screen.getByRole('button', { name: 'Выйти из аккаунта' }));

    expect(controller.retry).toHaveBeenCalledTimes(1);
    expect(onSignOut).toHaveBeenCalledTimes(1);
  });

  it('preserves the open form after an open failure and disables it while opening', () => {
    const controller = createController({
      status: 'failed',
      error: 'Не удалось открыть смену',
      failureKind: 'open'
    });
    const onSignOut = mock(() => {});
    const { rerender } = render(
      <I18nProvider>
        <PostAuthShiftGate controller={controller} organizationId={ORG_ID} currencyCode="TJS" onSignOut={onSignOut} />
      </I18nProvider>
    );
    fireEvent.change(screen.getByLabelText('Старт наличных'), { target: { value: '75' } });
    expect(screen.getByRole('alert')).toHaveTextContent('Не удалось открыть смену');

    rerender(
      <I18nProvider>
        <PostAuthShiftGate controller={{ ...controller, status: 'opening', error: null, failureKind: null }} organizationId={ORG_ID} currencyCode="TJS" onSignOut={onSignOut} />
      </I18nProvider>
    );

    expect(screen.getByLabelText('Старт наличных')).toHaveValue('75');
    expect(screen.getByRole('button', { name: 'Открыть смену' })).toBeDisabled();
    const signOut = screen.getByRole('button', { name: 'Выйти из аккаунта' });
    expect(signOut).toBeEnabled();
    fireEvent.click(signOut);
    expect(onSignOut).toHaveBeenCalledTimes(1);
  });
});
