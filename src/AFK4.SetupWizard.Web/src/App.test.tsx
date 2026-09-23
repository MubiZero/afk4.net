import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
import { afterAll, afterEach, beforeEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import type { WizardBranch, WizardDiscoverResponse } from './wizardApi';

// bun's mock.module is not hoisted above static imports — register before importing the app.
// Подменяем поверх настоящего модуля: список экспортов руками однажды отстанет от кода, и
// соседний экран останется без импорта (см. PhoneLoginScreen.test).
//
// Этот файл запускается отдельным процессом (см. скрипт `test` в package.json): App тянет за
// собой все экраны мастера сразу, и его подмена модуля в общем процессе доставалась бы соседям —
// та же причина, по которой отделён App.test у панели управляющего.
function branch(name: string, slug: string, overrides: Partial<WizardBranch> = {}): WizardBranch {
  return {
    branchId: `branch-${slug}`,
    branchSlug: slug,
    branchName: name,
    zones: [{ zoneId: 'zone-1', name: 'Главный зал', sortOrder: 1 }],
    seats: [],
    freeSeatIds: [],
    hasTariff: false,
    hasStaffBesidesOwner: false,
    ...overrides,
  };
}

const configuredBranch = branch('Центральный', 'central', {
  seats: [{
    seatId: 'seat-1',
    pcName: 'ПК-1',
    zoneId: 'zone-1',
    zoneName: 'Главный зал',
    sortOrder: 1,
    status: 'free',
    deviceId: null,
    deviceName: null,
    isOnline: null,
  }],
  freeSeatIds: ['seat-1'],
  hasTariff: true,
  hasStaffBesidesOwner: true,
});

let discovery: WizardDiscoverResponse = {
  ownerName: 'Владелец',
  branches: [branch('Центральный', 'central')],
  brandingConfigured: false,
};

const signInByPhone = mock(async () => ({ displayName: 'Владелец' }));
const discoverAuthenticated = mock(async () => discovery);
const brandingPresets = mock(async () => ({ presets: [] }));
const saveBranding = mock(async () => ({ saved: true }));
const inviteStaff = mock(async (_branchId: string, displayName: string, _phone: string, roleName: string) => ({
  displayName,
  roleName,
  code: '483920',
  expiresAtUtc: '2026-09-23T10:00:00Z',
}));
const createSeats = mock(async (_branchId: string, _zoneId: string, prefix: string, count: number) => ({
  names: Array.from({ length: count }, (_, index) => `${prefix}-${index + 1}`),
}));
const createTariff = mock(async (_branchId: string, name: string) => ({ name }));

const actualWizardApi = await import('./wizardApi');
mock.module('./wizardApi', () => ({
  ...actualWizardApi,
  signInByPhone,
  discoverAuthenticated,
  brandingPresets,
  saveBranding,
  inviteStaff,
  createSeats,
  createTariff,
}));

const { App } = await import('./App');

afterAll(() => {
  mock.module('./wizardApi', () => actualWizardApi);
});

async function signIn() {
  render(
    <I18nProvider initialLocale="ru">
      <App />
    </I18nProvider>,
  );
  fireEvent.change(screen.getByLabelText(/номер телефона/i), { target: { value: '+992 93 738-00-70' } });
  fireEvent.change(screen.getByLabelText('ПИН-код'), { target: { value: '246813' } });
  fireEvent.click(screen.getByRole('button', { name: /войти$/i }));
  await waitFor(() => expect(discoverAuthenticated).toHaveBeenCalled());
}

function chooseRole(name: RegExp) {
  fireEvent.click(screen.getByRole('radio', { name }));
  fireEvent.click(screen.getByRole('button', { name: 'Продолжить' }));
}

describe('App · поток мастера', () => {
  beforeEach(() => {
    discovery = { ownerName: 'Владелец', branches: [branch('Центральный', 'central')], brandingConfigured: false };
    signInByPhone.mockClear();
    discoverAuthenticated.mockClear();
  });

  afterEach(cleanup);

  // Один филиал — выбирать не из чего: лишний экран здесь читается как «вы что-то не так сделали».
  it('с единственным филиалом сразу спрашивает роль', async () => {
    await signIn();

    expect(await screen.findByText('Для кого это устройство?')).toBeInTheDocument();
    expect(screen.queryByText('Выберите филиал')).toBeNull();
  });

  it('с несколькими филиалами сначала спрашивает филиал', async () => {
    discovery = {
      ownerName: 'Владелец',
      branches: [branch('Центральный', 'central'), branch('Южный', 'south')],
      brandingConfigured: false,
    };
    await signIn();

    expect(await screen.findByText('Выберите филиал')).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /Южный/ }));

    expect(await screen.findByText('Для кого это устройство?')).toBeInTheDocument();
  });

  // На игровом ПК ставят железо, а не настраивают клуб: четыре экрана настройки там не нужны.
  it('на игровом ПК ведёт от роли прямо к устройству', async () => {
    await signIn();
    await screen.findByText('Для кого это устройство?');

    chooseRole(/Игровой ПК/);

    expect(await screen.findByRole('button', { name: 'Зарегистрировать' })).toBeInTheDocument();
    expect(screen.queryByText('Игровое место')).toBeInTheDocument();
  });

  it('на рабочем месте управляющего в новом клубе начинает с оформления', async () => {
    await signIn();
    await screen.findByText('Для кого это устройство?');

    chooseRole(/Админ \/ кассир/);

    expect(await screen.findByText('Как выглядит клуб')).toBeInTheDocument();
  });

  // Мастер ставится на каждый админский ПК, а клуб настраивают один раз: на втором рабочем месте
  // эти вопросы не помогают, а мешают — тариф с тем же именем сервер не примет.
  it('в настроенном клубе настройку не переспрашивает', async () => {
    discovery = { ownerName: 'Владелец', branches: [configuredBranch], brandingConfigured: true };
    await signIn();
    await screen.findByText('Для кого это устройство?');

    chooseRole(/Админ \/ кассир/);

    expect(await screen.findByRole('button', { name: 'Зарегистрировать' })).toBeInTheDocument();
  });

  // Степпер показывает шаги именно этого прогона: на игровом ПК их четыре, а не девять.
  it('степпер считает шаги того прогона, который идёт', async () => {
    await signIn();
    await screen.findByText('Для кого это устройство?');

    chooseRole(/Игровой ПК/);
    await screen.findByRole('button', { name: 'Зарегистрировать' });

    const stepper = screen.getByRole('list', { name: 'Прогресс настройки' });
    expect(stepper.querySelectorAll('li')).toHaveLength(4);
  });

  // «Назад» ходит по шагам этого прогона, а не по зашитым парам: с устройства на игровом ПК
  // возвращаемся к роли, а не к пропущенному экрану тарифа.
  it('«Назад» с устройства возвращает к роли, минуя пропущенные шаги', async () => {
    await signIn();
    await screen.findByText('Для кого это устройство?');
    chooseRole(/Игровой ПК/);
    await screen.findByRole('button', { name: 'Зарегистрировать' });

    fireEvent.click(screen.getByRole('button', { name: 'Назад' }));

    expect(await screen.findByText('Для кого это устройство?')).toBeInTheDocument();
  });
});

// Экраны мастера монтируются заново на каждом шаге. Пока введённое жило внутри экрана, «Назад»
// встречал человека пустыми полями — даже там, где он уже что-то отправил на сервер: список
// приглашённых с их кодами пропадал, а тариф снова предлагался к созданию, и сервер отвечал
// отказом на тот же самый тариф.
describe('App · «Назад» не теряет введённое', () => {
  beforeEach(() => {
    discovery = { ownerName: 'Владелец', branches: [branch('Центральный', 'central')], brandingConfigured: false };
    for (const fn of [signInByPhone, discoverAuthenticated, saveBranding, inviteStaff, createSeats, createTariff]) {
      fn.mockClear();
    }
  });

  afterEach(cleanup);

  async function toBranding() {
    await signIn();
    await screen.findByText('Для кого это устройство?');
    chooseRole(/Админ \/ кассир/);
    await screen.findByText('Как выглядит клуб');
  }

  async function toStaff() {
    await toBranding();
    fireEvent.click(screen.getByRole('button', { name: 'Пропустить' }));
    await screen.findByText('Кто будет работать в клубе');
  }

  async function toHall() {
    await toStaff();
    fireEvent.click(screen.getByRole('button', { name: /Пропустить/ }));
    await screen.findByText('Сколько мест в зале');
  }

  async function toTariff() {
    await toHall();
    fireEvent.click(screen.getByRole('button', { name: /Пропустить/ }));
    await screen.findByText('Почём час игры');
  }

  function back() {
    fireEvent.click(screen.getByRole('button', { name: 'Назад' }));
  }

  it('оформление: сохранённый цвет встречает на возврате', async () => {
    await toBranding();
    fireEvent.click(screen.getByRole('radio', { name: 'Синий' }));
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить и дальше' }));
    await screen.findByText('Кто будет работать в клубе');

    back();

    await screen.findByText('Как выглядит клуб');
    expect(screen.getByRole('radio', { name: 'Синий' }).getAttribute('aria-checked')).toBe('true');
    expect(saveBranding).toHaveBeenCalledTimes(1);
  });

  it('сотрудники: приглашённые с кодами и недописанное имя на месте', async () => {
    await toStaff();
    fireEvent.change(screen.getByLabelText('Имя'), { target: { value: 'Фарид' } });
    fireEvent.change(screen.getByLabelText('Телефон'), { target: { value: '901234567' } });
    fireEvent.click(screen.getByRole('button', { name: 'Пригласить' }));
    await screen.findByText('483920');
    fireEvent.change(screen.getByLabelText('Имя'), { target: { value: 'Нигора' } });

    fireEvent.click(screen.getByRole('button', { name: /Дальше/ }));
    await screen.findByText('Сколько мест в зале');
    back();

    await screen.findByText('Кто будет работать в клубе');
    expect(screen.getByText('483920')).toBeInTheDocument();
    expect(screen.getByText(/Фарид/)).toBeInTheDocument();
    expect(screen.getByLabelText('Имя')).toHaveValue('Нигора');
    expect(screen.getByRole('button', { name: /Дальше/ })).toBeInTheDocument();
    expect(inviteStaff).toHaveBeenCalledTimes(1);
  });

  it('зал: имя, число и заведённые места на месте', async () => {
    await toHall();
    fireEvent.change(screen.getByLabelText('Как называть места'), { target: { value: 'Зал' } });
    fireEvent.change(screen.getByLabelText('Сколько мест'), { target: { value: '5' } });
    fireEvent.click(screen.getByRole('button', { name: 'Завести места' }));
    await screen.findByText('Заведено 5 мест');

    fireEvent.click(screen.getByRole('button', { name: /Дальше/ }));
    await screen.findByText('Почём час игры');
    back();

    await screen.findByText('Сколько мест в зале');
    expect(screen.getByLabelText('Как называть места')).toHaveValue('Зал');
    expect(screen.getByLabelText('Сколько мест')).toHaveValue(5);
    expect(screen.getByText('Заведено 5 мест')).toBeInTheDocument();
    expect(createSeats).toHaveBeenCalledTimes(1);
  });

  // Повторное «Дальше» не создаёт тариф второй раз: он уже есть, а тот же тариф сервер не примет.
  it('тариф: созданный тариф на месте, повторное «Дальше» его не задваивает', async () => {
    await toTariff();
    fireEvent.change(screen.getByLabelText('Название тарифа'), { target: { value: 'Дневной' } });
    fireEvent.change(screen.getByLabelText('Цена за час, сомони'), { target: { value: '12' } });
    fireEvent.click(screen.getByRole('button', { name: 'Создать тариф' }));
    await screen.findByText(/Тариф «Дневной» создан/);

    fireEvent.click(screen.getByRole('button', { name: /Дальше/ }));
    await screen.findByRole('button', { name: 'Зарегистрировать' });
    back();

    await screen.findByText('Почём час игры');
    expect(screen.getByLabelText('Название тарифа')).toHaveValue('Дневной');
    expect(screen.getByLabelText('Цена за час, сомони')).toHaveValue(12);
    expect(screen.getByText(/Тариф «Дневной» создан/)).toBeInTheDocument();

    fireEvent.click(screen.getByRole('button', { name: /Дальше/ }));
    await screen.findByRole('button', { name: 'Зарегистрировать' });
    expect(createTariff).toHaveBeenCalledTimes(1);
  });

  it('устройство: имя ПК переживает шаг назад и обратно', async () => {
    await signIn();
    await screen.findByText('Для кого это устройство?');
    chooseRole(/Игровой ПК/);
    await screen.findByRole('button', { name: 'Зарегистрировать' });
    fireEvent.change(screen.getByLabelText('Имя ПК'), { target: { value: 'ПК-12' } });

    back();
    await screen.findByText('Для кого это устройство?');
    fireEvent.click(screen.getByRole('button', { name: 'Продолжить' }));

    await screen.findByRole('button', { name: 'Зарегистрировать' });
    expect(screen.getByLabelText('Имя ПК')).toHaveValue('ПК-12');
  });

  // ПИН-код — секрет, и на экране входа после возврата его нет намеренно: номер человек видит
  // свой, а ПИН набирает заново.
  it('вход: номер возвращается, ПИН-код — нет', async () => {
    await signIn();
    await screen.findByText('Для кого это устройство?');

    back();

    const phone = await screen.findByLabelText(/номер телефона/i);
    expect(phone).toHaveValue('93 738 00 70');
    expect(screen.getByLabelText('ПИН-код')).toHaveValue('');
  });

  // Приглашённые, заведённые места и тариф принадлежат филиалу: на другом филиале показывать их
  // значило бы соврать, что там уже кто-то работает.
  it('введённое для одного филиала не переезжает на другой', async () => {
    discovery = {
      ownerName: 'Владелец',
      branches: [branch('Центральный', 'central'), branch('Южный', 'south')],
      brandingConfigured: false,
    };
    await signIn();
    fireEvent.click(await screen.findByRole('button', { name: /Центральный/ }));
    await screen.findByText('Для кого это устройство?');
    chooseRole(/Админ \/ кассир/);
    await screen.findByText('Как выглядит клуб');
    fireEvent.click(screen.getByRole('button', { name: 'Пропустить' }));
    await screen.findByText('Кто будет работать в клубе');
    fireEvent.change(screen.getByLabelText('Имя'), { target: { value: 'Фарид' } });
    fireEvent.change(screen.getByLabelText('Телефон'), { target: { value: '901234567' } });
    fireEvent.click(screen.getByRole('button', { name: 'Пригласить' }));
    await screen.findByText('483920');

    back();
    await screen.findByText('Как выглядит клуб');
    back();
    await screen.findByText('Для кого это устройство?');
    back();
    fireEvent.click(await screen.findByRole('button', { name: /Южный/ }));
    await screen.findByText('Для кого это устройство?');
    fireEvent.click(screen.getByRole('button', { name: 'Продолжить' }));
    await screen.findByText('Как выглядит клуб');
    fireEvent.click(screen.getByRole('button', { name: 'Пропустить' }));

    await screen.findByText('Кто будет работать в клубе');
    expect(screen.queryByText('483920')).toBeNull();
    expect(screen.getByLabelText('Имя')).toHaveValue('');
  });
});
