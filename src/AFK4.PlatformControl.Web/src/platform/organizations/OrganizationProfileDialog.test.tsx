import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { PlatformApiError } from '@/api/platformTransport';
import { OrganizationProfileDialog } from './OrganizationProfileDialog';

afterEach(cleanup);

const organization = {
  organizationId: 'org-1',
  name: 'Клуб на Рудаки',
  contactEmail: 'club@example.tj',
  contactPhone: '+992 90 123-45-67',
  legalDetails: 'ООО «Клуб»'
} as never;

function setup(updateProfile = mock().mockResolvedValue(organization)) {
  const client = { updateProfile };
  const onUpdated = mock();
  render(
    <I18nProvider><ToastProvider>
      <OrganizationProfileDialog client={client as never} organization={organization} onClose={() => {}} onUpdated={onUpdated} />
    </ToastProvider></I18nProvider>
  );
  return { client, onUpdated };
}

describe('OrganizationProfileDialog', () => {
  it('открывается тем, что уже записано у клуба', () => {
    setup();

    expect(screen.getByLabelText('Название')).toHaveValue('Клуб на Рудаки');
    expect(screen.getByLabelText('Email для связи')).toHaveValue('club@example.tj');
    expect(screen.getByLabelText('Телефон для связи')).toHaveValue('+992 90 123-45-67');
    expect(screen.getByLabelText('Юридические реквизиты')).toHaveValue('ООО «Клуб»');
  });

  it('сохраняет все четыре поля', async () => {
    const { client, onUpdated } = setup();

    fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'Клуб на Сомони' } });
    fireEvent.change(screen.getByLabelText('Email для связи'), { target: { value: 'new@example.tj' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(client.updateProfile).toHaveBeenCalledTimes(1));
    expect(client.updateProfile.mock.calls[0]).toEqual([
      'org-1',
      {
        name: 'Клуб на Сомони',
        contactEmail: 'new@example.tj',
        contactPhone: '+992 90 123-45-67',
        legalDetails: 'ООО «Клуб»'
      }
    ]);
    expect(onUpdated).toHaveBeenCalledTimes(1);
  });

  // Стёртый телефон — это «контакта нет», а не контакт из пустой строки: иначе в карточке клуба
  // остаётся пустая строчка там, где раньше был номер.
  it('стёртое поле уезжает как «не указано»', async () => {
    const { client } = setup();

    fireEvent.change(screen.getByLabelText('Телефон для связи'), { target: { value: '   ' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(client.updateProfile).toHaveBeenCalledTimes(1));
    expect(client.updateProfile.mock.calls[0][1].contactPhone).toBeNull();
  });

  // Клуб без названия — это строка-призрак в списке клиентов, по которой его не найти.
  it('не даёт сохранить клуб без названия', () => {
    setup();

    fireEvent.change(screen.getByLabelText('Название'), { target: { value: '  ' } });

    expect(screen.getByRole('button', { name: 'Сохранить' })).toBeDisabled();
  });

  it('показывает отказ сервера', async () => {
    const { client, onUpdated } = setup(mock().mockRejectedValue(new PlatformApiError(403, 'Forbidden.')));

    fireEvent.change(screen.getByLabelText('Название'), { target: { value: 'Клуб на Сомони' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(client.updateProfile).toHaveBeenCalled());
    await screen.findByText('Недостаточно прав для этого действия.');
    expect(onUpdated).not.toHaveBeenCalled();
  });
});
