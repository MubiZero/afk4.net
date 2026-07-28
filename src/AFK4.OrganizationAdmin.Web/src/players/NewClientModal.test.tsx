import { describe, expect, it, afterEach, mock } from 'bun:test';
import { render, screen, cleanup, fireEvent } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { NewClientModal } from './NewClientModal';

afterEach(cleanup);

const renderModal = () => {
  const onChangeName = mock(() => {});
  const onChangePhone = mock(() => {});
  const onClose = mock(() => {});
  const onSubmit = mock(() => {});
  render(
    <I18nProvider initialLocale="ru">
      <NewClientModal name="" phone="" onChangeName={onChangeName} onChangePhone={onChangePhone} onClose={onClose} onSubmit={onSubmit} />
    </I18nProvider>
  );
  return { onChangeName, onChangePhone, onClose, onSubmit };
};

describe('NewClientModal', () => {
  it('renders the name and phone fields inside a dialog', () => {
    renderModal();
    expect(screen.getByRole('dialog', { name: 'Новый клиент' })).toBeInTheDocument();
    expect(screen.getByLabelText('Имя нового клиента')).toBeInTheDocument();
    expect(screen.getByLabelText('Телефон нового клиента')).toBeInTheDocument();
  });

  it('fires onSubmit when create button is clicked', () => {
    const { onSubmit } = renderModal();
    fireEvent.click(screen.getByRole('button', { name: /Создать/ }));
    expect(onSubmit).toHaveBeenCalled();
  });

  it('fires onChangeName when typing the name', () => {
    const { onChangeName } = renderModal();
    fireEvent.change(screen.getByLabelText('Имя нового клиента'), { target: { value: 'Zarina N.' } });
    expect(onChangeName).toHaveBeenCalledWith('Zarina N.');
  });
});
