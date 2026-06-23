import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { PinModal } from './PinModal';

afterEach(cleanup);

const renderModal = (over: Partial<Parameters<typeof PinModal>[0]> = {}) => {
  const onSubmit = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <PinModal
        pin="1234"
        onChangePin={() => {}}
        onClose={() => {}}
        onSubmit={onSubmit}
        busy={false}
        {...over}
      />
    </I18nProvider>
  );
  return { onSubmit };
};

describe('PinModal', () => {
  it('renders the PIN field', () => {
    renderModal();
    expect(screen.getByLabelText('Новый PIN')).toBeInTheDocument();
  });

  it('disables submit when PIN is shorter than 4 chars', () => {
    renderModal({ pin: '12' });
    expect(screen.getByRole('button', { name: /Сохранить PIN/ })).toBeDisabled();
  });

  it('fires onSubmit when PIN is valid', () => {
    const { onSubmit } = renderModal({ pin: '4567' });
    fireEvent.click(screen.getByRole('button', { name: /Сохранить PIN/ }));
    expect(onSubmit).toHaveBeenCalled();
  });
});
