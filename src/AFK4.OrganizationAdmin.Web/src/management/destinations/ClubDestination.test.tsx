import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../operatorToast';
import type { BranchProfileDto } from '../../api/clients/settings';

const getBranchProfile = mock(async (): Promise<BranchProfileDto> => ({
  organizationId: 'org-1',
  branchId: 'branch-1',
  name: 'AFK4 Центр',
  city: 'Душанбе',
  description: null,
  address: null,
  phone: null,
  telegram: null,
  website: null,
  instagram: null,
  logoUrl: null,
  logoMediaId: null,
  coverImageUrl: null,
  coverMediaId: null,
  photos: [],
  latitude: null,
  longitude: null,
  timeZone: 'Asia/Dushanbe',
  locale: 'ru',
  workingHours: [1, 2, 3, 4, 5, 6, 7].map((d) => ({ dayOfWeek: d, isClosed: false, openTime: '10:00', closeTime: '22:00' })),
  createdAtUtc: '2026-01-01T00:00:00Z'
}));
const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../../operatorHelpers', () => ({
  ...actual,
  createAuthenticatedOperatorClients: () => ({
    settings: {
      getBranchProfile,
      updateBranchProfile: mock(async (_b: string, request: unknown): Promise<BranchProfileDto> => request as BranchProfileDto)
    }
  })
}));

const { ClubDestination } = await import('./ClubDestination');
const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o1' }, branchId: 'b1' } as never;

afterEach(() => { getBranchProfile.mockClear(); cleanup(); });

describe('ClubDestination', () => {
  it('renders full club profile with player preview', async () => {
    const { container } = render(
      <I18nProvider initialLocale="ru">
        <ToastProvider>
          <ClubDestination backend={backend} session={{ permissions: [], organizationId: 'o1' } as never} currencyCode="TJS" />
        </ToastProvider>
      </I18nProvider>
    );
    expect(await screen.findByDisplayValue('AFK4 Центр')).toBeInTheDocument();
    expect(container.querySelector('.club-preview')).not.toBeNull();
    expect(screen.getByDisplayValue('TJS')).toBeInTheDocument();
    // 7 дней часов работы
    expect(container.querySelectorAll('.club-hours-row')).toHaveLength(7);
  });
});
