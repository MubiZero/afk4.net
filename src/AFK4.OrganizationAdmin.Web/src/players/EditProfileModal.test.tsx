import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { EditProfileModal } from './EditProfileModal';

afterEach(cleanup);

const renderModal = (over: Partial<Parameters<typeof EditProfileModal>[0]> = {}) => {
  const onSubmit = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <EditProfileModal
        name="Madina S."
        phone="+992 90 555 22 11"
        onChangeName={() => {}}
        onChangePhone={() => {}}
        onClose={() => {}}
        onSubmit={onSubmit}
        busy={false}
        {...over}
      />
    </I18nProvider>
  );
  return { onSubmit };
};

describe('EditProfileModal', () => {
  it('prefills name and phone', () => {
    renderModal();
    expect(screen.getByLabelText('Имя')).toHaveValue('Madina S.');
    expect(screen.getByLabelText('Телефон')).toHaveValue('+992 90 555 22 11');
  });

  it('disables submit when name is blank', () => {
    renderModal({ name: '   ' });
    expect(screen.getByRole('button', { name: /Сохранить/ })).toBeDisabled();
  });

  it('fires onSubmit when valid', () => {
    const { onSubmit } = renderModal();
    fireEvent.click(screen.getByRole('button', { name: /Сохранить/ }));
    expect(onSubmit).toHaveBeenCalled();
  });
});
