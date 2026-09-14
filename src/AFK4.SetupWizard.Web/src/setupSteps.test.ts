import { describe, it, expect } from 'bun:test';
import {
  nextSetupStep,
  previousVisibleStep,
  visibleSteps,
  type ClubSetupState,
  type WizardRunShape,
} from './setupSteps';
import type { WizardBranch } from './wizardApi';

function branch(overrides: Partial<WizardBranch> = {}): WizardBranch {
  return {
    branchId: 'b-1',
    branchSlug: 'main',
    branchName: 'Главный',
    zones: [{ zoneId: 'z-1', name: 'Общий зал', sortOrder: 0 }],
    seats: [],
    freeSeatIds: [],
    hasTariff: false,
    hasStaffBesidesOwner: false,
    ...overrides,
  };
}

const EMPTY_CLUB: ClubSetupState = { brandingConfigured: false, branch: branch() };

describe('nextSetupStep', () => {
  it('walks a fresh club through every setup step', () => {
    expect(nextSetupStep('role', 'manager_workstation', EMPTY_CLUB)).toBe('branding');
    expect(nextSetupStep('branding', 'manager_workstation', EMPTY_CLUB)).toBe('staff');
    expect(nextSetupStep('staff', 'manager_workstation', EMPTY_CLUB)).toBe('hall');
    expect(nextSetupStep('hall', 'manager_workstation', EMPTY_CLUB)).toBe('tariff');
    expect(nextSetupStep('tariff', 'manager_workstation', EMPTY_CLUB)).toBe('device');
  });

  // Второй админский ПК в том же клубе: настраивать нечего, спрашивать не о чем.
  it('goes straight to the device when the club is already set up', () => {
    const configured: ClubSetupState = {
      brandingConfigured: true,
      branch: branch({
        hasTariff: true,
        hasStaffBesidesOwner: true,
        seats: [{
          seatId: 's-1', pcName: 'ПК-1', zoneId: 'z-1', zoneName: 'Общий зал', sortOrder: 1,
          status: 'Free', deviceId: null, deviceName: null, isOnline: null,
        }],
      }),
    };

    expect(nextSetupStep('role', 'manager_workstation', configured)).toBe('device');
  });

  // Настроили наполовину — спрашиваем ровно про недостающее.
  it('asks only about what is missing', () => {
    const halfway: ClubSetupState = {
      brandingConfigured: true,
      branch: branch({
        hasStaffBesidesOwner: true,
        seats: [{
          seatId: 's-1', pcName: 'ПК-1', zoneId: 'z-1', zoneName: 'Общий зал', sortOrder: 1,
          status: 'Free', deviceId: null, deviceName: null, isOnline: null,
        }],
      }),
    };

    expect(nextSetupStep('role', 'manager_workstation', halfway)).toBe('tariff');
  });

  // На игровом ПК ставят железо: настройка клуба там не показывается вовсе.
  it('never shows club setup on a gaming pc', () => {
    expect(nextSetupStep('role', 'gaming_pc', EMPTY_CLUB)).toBe('device');
  });
});

const SEAT = {
  seatId: 's-1', pcName: 'ПК-1', zoneId: 'z-1', zoneName: 'Общий зал', sortOrder: 1,
  status: 'Free' as const, deviceId: null, deviceName: null, isOnline: null,
};

const CONFIGURED_CLUB: ClubSetupState = {
  brandingConfigured: true,
  branch: branch({ hasTariff: true, hasStaffBesidesOwner: true, seats: [SEAT] }),
};

function shape(overrides: Partial<WizardRunShape> = {}): WizardRunShape {
  return { role: 'manager_workstation', branchCount: 1, setup: EMPTY_CLUB, ...overrides };
}

describe('visibleSteps', () => {
  it('показывает весь путь на свежем клубе', () => {
    expect(visibleSteps(shape())).toEqual([
      'phoneLogin', 'role', 'branding', 'staff', 'hall', 'tariff', 'device', 'finished',
    ]);
  });

  // Степпер рисовал девять позиций всегда, включая четыре, которых на игровом ПК не бывает.
  it('на игровом ПК не обещает настройку клуба', () => {
    expect(visibleSteps(shape({ role: 'gaming_pc' }))).toEqual([
      'phoneLogin', 'role', 'device', 'finished',
    ]);
  });

  // Второй админский ПК в настроенном клубе: спрашивать не о чем.
  it('в настроенном клубе оставляет только вход, роль и устройство', () => {
    expect(visibleSteps(shape({ setup: CONFIGURED_CLUB }))).toEqual([
      'phoneLogin', 'role', 'device', 'finished',
    ]);
  });

  it('выбор филиала появляется только когда филиалов больше одного', () => {
    expect(visibleSteps(shape({ branchCount: 1 }))).not.toContain('branchSelection');
    expect(visibleSteps(shape({ branchCount: 3 }))).toContain('branchSelection');
  });

  // Пока роль не выбрана, считаем по самому длинному пути: степпер, который после ответа
  // становится короче, читается как «оказалось быстрее», а растущий — как обман.
  it('до ответа про роль считает по длинному пути', () => {
    expect(visibleSteps(shape({ role: null }))).toContain('branding');
  });
});

describe('previousVisibleStep', () => {
  // Раньше маршрут назад был зашит парами и про пропуски не знал: в клубе, где сотрудники уже
  // есть, «назад» с зала открывало пропущенный экран сотрудников.
  it('перепрыгивает пропущенный шаг', () => {
    const staffAlreadyThere: ClubSetupState = {
      brandingConfigured: true,
      branch: branch({ hasStaffBesidesOwner: true }),
    };

    expect(previousVisibleStep('hall', shape({ setup: staffAlreadyThere }))).toBe('role');
  });

  it('ведёт на предыдущий показанный шаг на свежем клубе', () => {
    expect(previousVisibleStep('hall', shape())).toBe('staff');
    expect(previousVisibleStep('tariff', shape())).toBe('hall');
  });

  // На игровом ПК назад с устройства — сразу к роли, а не к тарифу, которого не было.
  it('на игровом ПК с устройства возвращает к роли', () => {
    expect(previousVisibleStep('device', shape({ role: 'gaming_pc' }))).toBe('role');
  });

  it('с первого шага назад некуда', () => {
    expect(previousVisibleStep('phoneLogin', shape())).toBeNull();
  });
});
