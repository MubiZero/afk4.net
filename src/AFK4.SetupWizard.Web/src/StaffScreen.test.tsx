import { describe, it, expect, mock } from 'bun:test';
import { HostBridgeRequestError } from './hostBridge';
import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { StaffScreen, type StaffClient } from './StaffScreen';

function renderScreen(client: StaffClient, onContinue = mock()) {
  render(
    <I18nProvider>
      <StaffScreen
      stepNumber={1}
        client={client}
        ownerName="Владелец"
        branchName="Главный"
        onContinue={onContinue}
        onBack={mock()}
      />
    </I18nProvider>,
  );
  return onContinue;
}

const INVITE = {
  displayName: 'Дилшод',
  roleName: 'operator',
  code: '123456',
  expiresAtUtc: '2026-09-14T10:00:00Z',
};

describe('StaffScreen', () => {
  it('invites a person and shows the code to hand over', async () => {
    const invite = mock().mockResolvedValue(INVITE);
    renderScreen({ invite });

    fireEvent.change(screen.getByLabelText('Имя'), { target: { value: ' Дилшод ' } });
    fireEvent.change(screen.getByLabelText('Телефон'), { target: { value: '+992 90 000-00-00' } });
    fireEvent.click(screen.getByRole('button', { name: 'Пригласить' }));

    await waitFor(() => expect(screen.getByText('123456')).toBeTruthy());
    // Номер уезжает нормализованным, как и на входе в мастер: сервер не должен разбирать
    // пробелы и дефисы, которые набрал человек.
    expect(invite).toHaveBeenCalledWith('Дилшод', '992900000000', 'operator');
    expect(screen.getByText(/Дилшод · Администратор/)).toBeTruthy();
  });

  // Пустая форма не должна слать запрос: без имени и номера приглашать некого.
  it('does not invite without a name and a phone', () => {
    const invite = mock();
    renderScreen({ invite });

    const button = screen.getByRole('button', { name: 'Пригласить' });
    fireEvent.click(button);

    expect(invite).not.toHaveBeenCalled();
    expect((button as HTMLButtonElement).disabled).toBe(true);
  });

  // Номер набирают со слуха: недобранная цифра уходила приглашением постороннему человеку, а
  // поле очищается сразу после отправки — заметить было негде.
  it('не отправляет приглашение на недобранный номер', () => {
    const invite = mock();
    renderScreen({ invite });

    fireEvent.change(screen.getByLabelText('Имя'), { target: { value: 'Дилшод' } });
    fireEvent.change(screen.getByLabelText('Телефон'), { target: { value: '90 000 00' } });
    fireEvent.blur(screen.getByLabelText('Телефон'));

    expect(screen.getByText(/девять цифр/i)).toBeTruthy();
    expect((screen.getByRole('button', { name: 'Пригласить' }) as HTMLButtonElement).disabled).toBe(true);
    expect(invite).not.toHaveBeenCalled();
  });

  // Клуб можно открыть и одному: шаг проходится без единого приглашения.
  it('can be passed with nobody invited', () => {
    const onContinue = renderScreen({ invite: mock() });

    fireEvent.click(screen.getByRole('button', { name: 'Пропустить' }));

    expect(onContinue).toHaveBeenCalled();
  });

  it('keeps the form filled when the invite fails', async () => {
    renderScreen({ invite: mock().mockRejectedValue(new Error('network')) });

    fireEvent.change(screen.getByLabelText('Имя'), { target: { value: 'Дилшод' } });
    fireEvent.change(screen.getByLabelText('Телефон'), { target: { value: '+992900000000' } });
    fireEvent.click(screen.getByRole('button', { name: 'Пригласить' }));

    await waitFor(() => expect(screen.getByText(/Не удалось пригласить/)).toBeTruthy());
    expect((screen.getByLabelText('Имя') as HTMLInputElement).value).toBe('Дилшод');
  });

  // «Проверьте номер» на номере, который в порядке, — это ложный след: человек уже работает в
  // клубе, и приглашение ему просто не нужно.
  it('различает занятый номер и неверный', async () => {
    const invite = mock()
      .mockRejectedValueOnce(new HostBridgeRequestError('Platform API returned 400', 'staff_phone_taken', null))
      .mockRejectedValueOnce(new HostBridgeRequestError('Platform API returned 400', 'invalid_phone', null));
    renderScreen({ invite });

    fireEvent.change(screen.getByLabelText('Имя'), { target: { value: 'Дилшод' } });
    fireEvent.change(screen.getByLabelText('Телефон'), { target: { value: '+992900000000' } });
    fireEvent.click(screen.getByRole('button', { name: 'Пригласить' }));
    await waitFor(() => expect(screen.getByText(/уже принадлежит сотруднику клуба/)).toBeTruthy());

    fireEvent.click(screen.getByRole('button', { name: 'Пригласить' }));
    await waitFor(() => expect(screen.getByText(/приглашение уходит SMS/)).toBeTruthy());
  });

  // Предел подписки чинится не повтором, а разговором с владельцем.
  it('предел тарифного плана называет пределом, а не сбоем', async () => {
    renderScreen({
      invite: mock().mockRejectedValue(
        new HostBridgeRequestError('Platform API returned 409', 'plan_limit_reached', null),
      ),
    });

    fireEvent.change(screen.getByLabelText('Имя'), { target: { value: 'Дилшод' } });
    fireEvent.change(screen.getByLabelText('Телефон'), { target: { value: '+992900000000' } });
    fireEvent.click(screen.getByRole('button', { name: 'Пригласить' }));

    await waitFor(() => expect(screen.getByText(/Тарифный план клуба/)).toBeTruthy());
  });

});
