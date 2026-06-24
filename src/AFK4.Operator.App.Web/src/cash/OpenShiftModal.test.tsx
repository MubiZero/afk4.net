import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { OpenShiftModal } from './OpenShiftModal';

afterEach(cleanup);

function renderModal(overrides: Partial<Parameters<typeof OpenShiftModal>[0]> = {}) {
  const onSubmit = mock(() => {});
  const onClose = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <OpenShiftModal
        startingCash="150.00"
        note="Утренняя смена"
        onChangeStartingCash={() => {}}
        onChangeNote={() => {}}
        onClose={onClose}
        onSubmit={onSubmit}
        busy={false}
        {...overrides}
      />
    </I18nProvider>
  );
  return { onSubmit, onClose };
}

describe('OpenShiftModal', () => {
  it('рендерит поля старта наличных и комментария', () => {
    renderModal();
    expect(screen.getByLabelText('Старт наличных')).toHaveValue('150.00');
    expect(screen.getByLabelText('Комментарий')).toHaveValue('Утренняя смена');
  });

  it('submit формы вызывает onSubmit', () => {
    const { onSubmit } = renderModal();
    fireEvent.click(screen.getByRole('button', { name: 'Открыть смену' }));
    expect(onSubmit).toHaveBeenCalledTimes(1);
  });

  it('busy дизейблит сабмит', () => {
    renderModal({ busy: true });
    expect(screen.getByRole('button', { name: 'Открыть смену' })).toBeDisabled();
  });
});
