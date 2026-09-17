import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen, waitFor } from '@testing-library/react';
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
const getBranding = mock(async () => ({ organizationId: 'org-1', name: 'AFK4 Центр', logoUrl: null, accentColor: '#0A84FF' }));
const updateBranding = mock(async (request: { logoUrl: string | null; accentColor: string | null }) => ({
  organizationId: 'org-1', name: 'AFK4 Центр', ...request
}));

const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../../operatorHelpers', () => ({
  ...actual,
  createAuthenticatedOperatorClients: () => ({
    settings: {
      getBranchProfile,
      updateBranchProfile: mock(async (_b: string, request: unknown): Promise<BranchProfileDto> => request as BranchProfileDto)
    },
    branding: { getBranding, updateBranding }
  })
}));

const { ClubDestination } = await import('./ClubDestination');
const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o1' }, branchId: 'b1' } as never;

afterEach(() => { getBranchProfile.mockClear(); getBranding.mockClear(); updateBranding.mockClear(); cleanup(); });

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

  // Оформление сети ставилось только мастером установки: промахнулся с цветом при установке —
  // и поменять его было негде, хотя логотип и цвет видят гости.
  it('показывает выбранный при установке цвет и сохраняет новый', async () => {
    render(
      <I18nProvider initialLocale="ru">
        <ToastProvider>
          <ClubDestination backend={backend} session={{ permissions: [], organizationId: 'o1' } as never} currencyCode="TJS" />
        </ToastProvider>
      </I18nProvider>
    );

    await screen.findByDisplayValue('AFK4 Центр');
    const swatches = screen.getAllByRole('radio');
    expect(swatches.find((swatch) => swatch.getAttribute('aria-checked') === 'true')?.getAttribute('aria-label'))
      .toBe('Синий');

    fireEvent.click(screen.getByRole('radio', { name: 'Зелёный' }));
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(updateBranding).toHaveBeenCalledTimes(1));
    expect(updateBranding.mock.calls[0][0]).toEqual({ logoUrl: null, accentColor: '#30D158' });
  });

  // Лишний PATCH писал бы в журнал клуба событие про бренд каждый раз, когда правят телефон.
  it('не трогает оформление, когда правили только профиль филиала', async () => {
    render(
      <I18nProvider initialLocale="ru">
        <ToastProvider>
          <ClubDestination backend={backend} session={{ permissions: [], organizationId: 'o1' } as never} currencyCode="TJS" />
        </ToastProvider>
      </I18nProvider>
    );

    const name = await screen.findByDisplayValue('AFK4 Центр');
    fireEvent.change(name, { target: { value: 'AFK4 Сомони' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(screen.getByDisplayValue('AFK4 Сомони')).toBeInTheDocument());
    expect(updateBranding).not.toHaveBeenCalled();
  });
});
