import { describe, it, expect } from 'bun:test';
import { nextSetupStep, type ClubSetupState } from './setupSteps';
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
