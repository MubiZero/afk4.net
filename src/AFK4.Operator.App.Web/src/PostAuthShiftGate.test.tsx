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

function renderGate(controller: PostAuthShiftGateController, onSignOut = mock(() => {})) {
  render(
    <I18nProvider>
      <PostAuthShiftGate
        controller={controller}
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

  it('submits starting cash and the default note without a dismiss action', async () => {
    const controller = createController();
    renderGate(controller);

    expect(screen.getByRole('heading', { name: 'Откройте смену' })).toBeInTheDocument();
    expect(document.querySelector('.panel-modal-close')).toBeNull();
    fireEvent.change(screen.getByLabelText('Старт наличных'), { target: { value: '100.50' } });
    fireEvent.click(screen.getByRole('button', { name: 'Открыть смену' }));

    await waitFor(() => expect(controller.openShift).toHaveBeenCalledWith(expect.objectContaining({
      organizationId: ORG_ID,
      startingCash: { currencyCode: 'TJS', minorUnits: 10050 },
      openingNote: 'Утренняя смена'
    })));
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
