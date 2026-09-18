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

const actualWizardApi = await import('./wizardApi');
mock.module('./wizardApi', () => ({
  ...actualWizardApi,
  signInByPhone,
  discoverAuthenticated,
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
