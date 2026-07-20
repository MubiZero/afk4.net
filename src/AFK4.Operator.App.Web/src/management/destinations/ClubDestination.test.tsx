import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../operatorToast';
import type { BranchProfileDto } from '../../operatorApiClients';

const getBranchProfile = mock(async (): Promise<BranchProfileDto> => ({ name: 'AFK4 Центр', city: 'Душанбе' }));
const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../../operatorHelpers', () => ({
  ...actual,
  createAuthenticatedOperatorClients: () => ({
    settings: {
      getBranchProfile,
      updateBranchProfile: mock(async (_branchId: string, request: unknown): Promise<BranchProfileDto> => request as BranchProfileDto)
    }
  })
}));

const { ClubDestination } = await import('./ClubDestination');
const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o1' }, branchId: 'b1' } as never;

afterEach(() => {
  getBranchProfile.mockClear();
  cleanup();
});

describe('ClubDestination', () => {
  it('renders club profile in a mgmt-form with read-only meta rows', async () => {
    const { container } = render(
      <I18nProvider initialLocale="ru">
        <ToastProvider>
          <ClubDestination backend={backend} session={{ permissions: [], organizationId: 'o1' } as never} currencyCode="TJS" />
        </ToastProvider>
      </I18nProvider>
    );
    expect(await screen.findByDisplayValue('AFK4 Центр')).toBeInTheDocument();
    // Валюта/филиал — не инпуты, а мета-значения
    expect(container.querySelector('.mgmt-meta-value')).not.toBeNull();
    expect(screen.getByText('TJS')).toBeInTheDocument();
    expect(container.querySelector('.mgmt-form')).not.toBeNull();
    expect(container.querySelector('.settings-form-grid')).toBeNull();
  });
});
