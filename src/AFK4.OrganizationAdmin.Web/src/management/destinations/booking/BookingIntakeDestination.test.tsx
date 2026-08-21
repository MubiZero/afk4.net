import { afterEach, describe, expect, it, mock } from 'bun:test';
import { cleanup, fireEvent, render, screen } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../../operatorToast';
import type { BranchBookingSettingsDto } from '../../../api/clients/settings';

const organizationId = 'o1';

function settings(overrides: Partial<BranchBookingSettingsDto> = {}): BranchBookingSettingsDto {
  return {
    organizationId,
    branchId: 'b1',
    acceptanceMode: 'auto',
    respondWithinMinutes: 15,
    requirePrepaymentFromNewGuests: true,
    maxActiveReservationsForNewGuests: 1,
    regularAfterVisits: 3,
    holdSeatAfterStartMinutes: 20,
    keepPrepaymentOnNoShow: false,
    updatedAtUtc: null,
    ...overrides
  };
}

let stored = settings();
const getBookingSettings = mock(async (): Promise<BranchBookingSettingsDto> => stored);
const updateBookingSettings = mock(async (_branchId: string, request: Record<string, unknown>): Promise<BranchBookingSettingsDto> => {
  stored = { ...settings(), ...request, updatedAtUtc: '2026-08-20T09:00:00Z' } as BranchBookingSettingsDto;
  return stored;
});

const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../../../operatorHelpers', () => ({
  ...actual,
  createAuthenticatedOperatorClients: () => ({ settings: { getBookingSettings, updateBookingSettings } })
}));

const { BookingIntakeDestination } = await import('./BookingIntakeDestination');
const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId }, branchId: 'b1' } as never;

function renderScreen() {
  return render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <BookingIntakeDestination backend={backend} session={{ permissions: [], organizationId } as never} currencyCode="TJS" />
      </ToastProvider>
    </I18nProvider>
  );
}

afterEach(() => {
  stored = settings();
  getBookingSettings.mockClear();
  updateBookingSettings.mockClear();
  cleanup();
});

describe('BookingIntakeDestination', () => {
  it('показывает настройки филиала и помечает ненастроенный филиал значениями по умолчанию', async () => {
    renderScreen();

    expect(await screen.findByLabelText('Срок ответа на заявку')).toHaveValue(15);
    expect(screen.getByLabelText('Гость становится своим после')).toHaveValue(3);
    expect(screen.getByLabelText('Броней у новичка одновременно')).toHaveValue(1);
    expect(screen.getByLabelText('Держим место после начала')).toHaveValue(20);
    expect(screen.getByLabelText('Просить предоплату с новых')).toBeChecked();
    expect(screen.getByLabelText('Предоплату при неявке оставляем себе')).not.toBeChecked();
    expect(screen.getByText('Филиал приём броней ещё не настраивал — работают значения по умолчанию.')).toBeInTheDocument();
  });

  it('режимы приёма — взаимоисключающий выбор, активен пришедший с сервера', async () => {
    stored = settings({ acceptanceMode: 'manual' });
    renderScreen();

    const manual = await screen.findByLabelText(/Смотрит администратор/);
    expect(manual).toBeChecked();
    expect(screen.getByLabelText(/Подтверждаем сами/)).not.toBeChecked();
    expect(screen.getByLabelText(/Не принимаем/)).not.toBeChecked();
  });

  it('сохраняет изменённые правила одним PUT', async () => {
    renderScreen();

    fireEvent.click(await screen.findByLabelText(/Смотрит администратор/));
    fireEvent.change(screen.getByLabelText('Срок ответа на заявку'), { target: { value: '45' } });
    fireEvent.click(screen.getByLabelText('Предоплату при неявке оставляем себе'));
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await screen.findByText('Изменено', { exact: false });
    expect(updateBookingSettings).toHaveBeenCalledTimes(1);
    expect(updateBookingSettings.mock.calls[0][1]).toEqual({
      organizationId,
      acceptanceMode: 'manual',
      respondWithinMinutes: 45,
      requirePrepaymentFromNewGuests: true,
      maxActiveReservationsForNewGuests: 1,
      regularAfterVisits: 3,
      holdSeatAfterStartMinutes: 20,
      keepPrepaymentOnNoShow: true
    });
  });

  it('число вне границ сервера не уходит на сервер', async () => {
    renderScreen();

    fireEvent.change(await screen.findByLabelText('Срок ответа на заявку'), { target: { value: '2' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    expect(updateBookingSettings).not.toHaveBeenCalled();
  });

  it('«Отменить» возвращает поля к последнему загруженному состоянию', async () => {
    renderScreen();

    const respond = await screen.findByLabelText('Срок ответа на заявку');
    fireEvent.change(respond, { target: { value: '90' } });
    fireEvent.click(screen.getByRole('button', { name: 'Отменить' }));

    expect(screen.getByLabelText('Срок ответа на заявку')).toHaveValue(15);
    expect(updateBookingSettings).not.toHaveBeenCalled();
  });
});
