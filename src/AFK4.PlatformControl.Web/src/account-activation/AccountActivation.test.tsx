import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterEach, expect, it, mock } from 'bun:test';
import { cleanup } from '@testing-library/react';
import { ThemeProvider } from '@/theme/ThemeProvider';
import { PlatformApiError } from '../api/platformApi';
import { I18nProvider } from '../i18n/I18nProvider';
import { AccountActivation } from './AccountActivation';
import type { AccountActivationApi, AccountActivationKind } from './accountActivationApi';

afterEach(cleanup);

function renderScreen(kind: AccountActivationKind, accept: AccountActivationApi['accept']) {
  render(
    <ThemeProvider>
      <I18nProvider>
        <AccountActivation client={{ accept } as never} initialCode=" invite-1 " kind={kind} />
      </I18nProvider>
    </ThemeProvider>
  );
}

it('activates the owner and directs them to Organization Admin without exposing a staff session', async () => {
  const accept = mock(async () => {});
  renderScreen('organization-owner', accept as never);

  fireEvent.change(screen.getByLabelText('Логин или email'), { target: { value: ' owner@example.test ' } });
  fireEvent.change(screen.getByLabelText('ПИН-код'), { target: { value: '246813' } });
  fireEvent.change(screen.getByLabelText('Повторите ПИН-код'), { target: { value: '246813' } });
  fireEvent.click(screen.getByRole('button', { name: 'Активировать владельца' }));

  await waitFor(() => expect(accept).toHaveBeenCalledWith({
    code: 'invite-1',
    userName: 'owner@example.test',
    displayName: '',
    password: '246813'
  }, 'organization-owner'));
  expect(screen.getByRole('heading', { name: 'Владелец активирован' })).toBeInTheDocument();
});

// Приглашение администратора платформы сервер принимал с самого начала, а позвать его было
// неоткуда: экран знал ровно один путь — владельца. Приглашённый получал код и упирался в тупик.
it('activates a platform administrator through the platform-admin path', async () => {
  const accept = mock(async () => {});
  renderScreen('platform-admin', accept as never);

  fireEvent.change(screen.getByLabelText('Логин или email'), { target: { value: 'support1' } });
  fireEvent.change(screen.getByLabelText('Имя и фамилия'), { target: { value: ' Первая поддержка ' } });
  fireEvent.change(screen.getByLabelText('ПИН-код'), { target: { value: '246813' } });
  fireEvent.change(screen.getByLabelText('Повторите ПИН-код'), { target: { value: '246813' } });
  fireEvent.click(screen.getByRole('button', { name: 'Активировать администратора' }));

  await waitFor(() => expect(accept).toHaveBeenCalledWith({
    code: 'invite-1',
    userName: 'support1',
    displayName: 'Первая поддержка',
    password: '246813'
  }, 'platform-admin'));
  expect(screen.getByRole('heading', { name: 'Администратор активирован' })).toBeInTheDocument();
});

// В списке администраторов человека различают по имени; пустое имя оставило бы строку без
// подписи, и понять, кому выдан доступ, было бы не по чему. Проверяется именно то, что
// проверяемо: поле обязательное и пустая форма никуда не уходит. Русскую подсказку здесь не
// ждём — до неё дело не доходит, форму останавливает браузер своим сообщением (так же, как на
// соседних полях).
it('will not send a platform administrator without a name', async () => {
  const accept = mock(async () => {});
  renderScreen('platform-admin', accept as never);

  expect(screen.getByLabelText('Имя и фамилия')).toBeRequired();
  fireEvent.change(screen.getByLabelText('Логин или email'), { target: { value: 'support1' } });
  fireEvent.change(screen.getByLabelText('ПИН-код'), { target: { value: '246813' } });
  fireEvent.change(screen.getByLabelText('Повторите ПИН-код'), { target: { value: '246813' } });
  fireEvent.click(screen.getByRole('button', { name: 'Активировать администратора' }));

  expect(accept).not.toHaveBeenCalled();
});

// Владелец имя не вводит — поле показывается только там, где оно действительно нужно.
it('does not ask the owner for a name', () => {
  renderScreen('organization-owner', (async () => {}) as never);
  expect(screen.queryByLabelText('Имя и фамилия')).toBeNull();
});

// Обе неудачи активации администратора платформы приходят как 400. Если бы текст выбирался по
// статусу, человеку с коротким паролем сообщили бы, что не подошёл код, и он правил бы не то.
it('tells a rejected password apart from a rejected code, though both arrive as 400', async () => {
  const accept = mock(async () => { throw new PlatformApiError(400, 'invalid_details', 'invalid_details'); });
  renderScreen('platform-admin', accept as never);

  fireEvent.change(screen.getByLabelText('Логин или email'), { target: { value: 'support1' } });
  fireEvent.change(screen.getByLabelText('Имя и фамилия'), { target: { value: 'Первая поддержка' } });
  fireEvent.change(screen.getByLabelText('ПИН-код'), { target: { value: '246813' } });
  fireEvent.change(screen.getByLabelText('Повторите ПИН-код'), { target: { value: '246813' } });
  fireEvent.click(screen.getByRole('button', { name: 'Активировать администратора' }));

  await screen.findByText('Сервер не принял логин, имя или ПИН-код. Проверьте их и попробуйте снова.');
});

it('reports an unusable code without claiming which way it is unusable', async () => {
  const accept = mock(async () => { throw new PlatformApiError(400, 'invalid_invitation', 'invalid_invitation'); });
  renderScreen('platform-admin', accept as never);

  fireEvent.change(screen.getByLabelText('Логин или email'), { target: { value: 'support1' } });
  fireEvent.change(screen.getByLabelText('Имя и фамилия'), { target: { value: 'Первая поддержка' } });
  fireEvent.change(screen.getByLabelText('ПИН-код'), { target: { value: '246813' } });
  fireEvent.change(screen.getByLabelText('Повторите ПИН-код'), { target: { value: '246813' } });
  fireEvent.click(screen.getByRole('button', { name: 'Активировать администратора' }));

  await screen.findByText('Код приглашения не подошёл: он мог истечь, быть отозванным или уже использованным.');
});
