import { cleanup, fireEvent, render, screen, waitFor, within } from '@testing-library/react';
import { afterAll, afterEach, describe, expect, it, mock } from 'bun:test';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../../operatorToast';
import { permissionNames } from '../../../operatorPermissions';
import type { TariffOptionDto } from '../../../operatorApiClients';

const createTariff = mock(async () => ({ tariffId: 't-new' }));
const createTariffVersion = mock(async () => ({ tariffVersionId: 'tv-new' }));
const updateTariff = mock(async () => ({ tariffId: 't1' }));
const updateTariffVersion = mock(async () => ({ tariffVersionId: 'tv1' }));

const actualHelpers = await import('../../../operatorHelpers');
mock.module('../../../operatorHelpers', () => ({
  ...actualHelpers,
  createAuthenticatedOperatorClients: () => ({
    settings: { createTariff, createTariffVersion, updateTariff, updateTariffVersion }
  })
}));

const { TariffsTab } = await import('./TariffsTab');

afterAll(() => {
  mock.module('../../../operatorHelpers', () => (globalThis as typeof globalThis & {
    __afk4RealOperatorHelpers: typeof import('../../../operatorHelpers');
  }).__afk4RealOperatorHelpers);
});

afterEach(() => {
  cleanup();
  createTariff.mockClear();
  createTariffVersion.mockClear();
  updateTariff.mockClear();
  updateTariffVersion.mockClear();
});

const wrap = (ui: React.ReactNode) =>
  render(<I18nProvider initialLocale="ru"><ToastProvider>{ui}</ToastProvider></I18nProvider>);

const backend = {
  config: { platformBaseUrl: 'http://test' },
  session: { accessToken: 't', organizationId: 'org', permissions: [permissionNames.manageTariffs] },
  branchId: 'b1'
};

const tariffId = '11111111-1111-1111-1111-111111111111';
const tariffVersionId = '22222222-2222-2222-2222-222222222222';

const tariffs: TariffOptionDto[] = [{
  tariffId,
  tariffVersionId,
  name: 'Стандарт',
  pricePerMinuteMinorUnits: 150, // 150/min => 9000/hour => 90.00 TJS
  minimumBillableMinutes: 15,
  roundingIncrementMinutes: 5,
  isActive: true,
  currencyCode: 'TJS',
  effectiveFromUtc: '2026-01-01T00:00:00Z'
} as never];

const onReload = mock(async () => {});
const onFeedback = mock(() => {});

describe('TariffsTab', () => {
  it('renders the tariff list and the price without an amber/warning class — money is neutral', () => {
    const { container } = wrap(
      <TariffsTab tariffs={tariffs} currencyCode="TJS" backend={null} canManageTariffs={false} onReload={onReload} onFeedback={onFeedback} />
    );
    expect(screen.getByText('Стандарт')).toBeTruthy();
    const priceEl = container.querySelector('.ui-money');
    expect(priceEl).toBeTruthy();
    expect(priceEl!.textContent).toContain('90');
    expect(priceEl!.className).toBe('ui-money');
  });

  it('hides the "+ Тариф" primary action and row menu without canManageTariffs', () => {
    wrap(<TariffsTab tariffs={tariffs} currencyCode="TJS" backend={null} canManageTariffs={false} onReload={onReload} onFeedback={onFeedback} />);
    expect(screen.queryByRole('button', { name: '+ Тариф' })).toBeNull();
    expect(screen.queryByRole('button', { name: 'Действия' })).toBeNull();
  });

  it('shows a create-first-tariff empty state when there are no tariffs', () => {
    wrap(<TariffsTab tariffs={[]} currencyCode="TJS" backend={null} canManageTariffs onReload={onReload} onFeedback={onFeedback} />);
    expect(screen.getByText('Нет тарифов')).toBeTruthy();
  });

  it('clicking a row opens the drawer with the tariff form', () => {
    wrap(<TariffsTab tariffs={tariffs} currencyCode="TJS" backend={null} canManageTariffs={false} onReload={onReload} onFeedback={onFeedback} />);
    fireEvent.click(screen.getByText('Стандарт'));
    expect(screen.getByRole('textbox', { name: 'Название тарифа' })).toHaveValue('Стандарт');
  });

  it('"+ Тариф" opens the create modal and submits createTariff + createTariffVersion', async () => {
    wrap(<TariffsTab tariffs={tariffs} currencyCode="TJS" backend={backend as never} canManageTariffs onReload={onReload} onFeedback={onFeedback} />);
    fireEvent.click(screen.getByRole('button', { name: '+ Тариф' }));
    expect(screen.getByRole('dialog', { name: 'Новый тариф' })).toBeTruthy();

    fireEvent.click(screen.getByRole('button', { name: 'Создать тариф' }));

    await waitFor(() => expect(createTariff).toHaveBeenCalledTimes(1));
    expect(createTariff).toHaveBeenCalledWith('b1', expect.objectContaining({ organizationId: 'org', name: expect.any(String) }));
    await waitFor(() => expect(createTariffVersion).toHaveBeenCalledTimes(1));
    expect(createTariffVersion).toHaveBeenCalledWith('b1', 't-new', expect.objectContaining({
      organizationId: 'org',
      currencyCode: 'TJS',
      pricePerMinuteMinorUnits: 150 // 90.00/hour -> 1.50/min -> 150 minor units
    }));
    await waitFor(() => expect(onReload).toHaveBeenCalled());
  });

  it('saves an edit from the drawer form via updateTariff + updateTariffVersion', async () => {
    wrap(<TariffsTab tariffs={tariffs} currencyCode="TJS" backend={backend as never} canManageTariffs onReload={onReload} onFeedback={onFeedback} />);
    fireEvent.click(screen.getByText('Стандарт'));
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(updateTariff).toHaveBeenCalledWith('b1', tariffId, {
      organizationId: 'org',
      name: 'Стандарт',
      isActive: true,
      schedule: { appliesOnDaysMask: 0, appliesFromMinuteOfDay: null, appliesToMinuteOfDay: null }
    }));
    await waitFor(() => expect(updateTariffVersion).toHaveBeenCalledWith('b1', tariffId, tariffVersionId, expect.objectContaining({
      organizationId: 'org',
      pricePerMinuteMinorUnits: 150,
      isActive: true
    })));
  });

  it('retiring a tariff via the row menu asks for confirmation before calling updateTariff with isActive:false', async () => {
    const { container } = wrap(
      <TariffsTab tariffs={tariffs} currencyCode="TJS" backend={backend as never} canManageTariffs onReload={onReload} onFeedback={onFeedback} />
    );
    fireEvent.click(within(container).getAllByRole('button', { name: 'Действия' })[0]);
    fireEvent.click(screen.getByRole('menuitem', { name: 'Снять тариф' }));

    expect(updateTariff).not.toHaveBeenCalled();
    expect(screen.getByRole('alertdialog')).toBeTruthy();

    fireEvent.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Снять тариф' }));
    await waitFor(() => expect(updateTariff).toHaveBeenCalledWith('b1', tariffId, expect.objectContaining({ isActive: false })));
    await waitFor(() => expect(updateTariffVersion).toHaveBeenCalledWith('b1', tariffId, tariffVersionId, expect.objectContaining({ isActive: false })));
  });
  // Часы задаются в тех же минутах от полуночи, что и хранятся: 08:00 — это 480.
  it('saves the hours the owner set on the tariff', async () => {
    wrap(<TariffsTab tariffs={tariffs} currencyCode="TJS" backend={backend as never} canManageTariffs onReload={onReload} onFeedback={onFeedback} />);
    fireEvent.click(screen.getByText('Стандарт'));

    const [from, to] = screen.getAllByLabelText(/^(С|До)$/);
    fireEvent.change(from, { target: { value: '08:00' } });
    fireEvent.change(to, { target: { value: '16:00' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(updateTariff).toHaveBeenCalledWith('b1', tariffId, expect.objectContaining({
      schedule: expect.objectContaining({ appliesFromMinuteOfDay: 480, appliesToMinuteOfDay: 960 })
    })));
  });

  // Полдня — это не расписание, а недописанная настройка: сервер такое отклонит, и спрашивать
  // его об этом незачем.
  it('refuses to save half a window', async () => {
    wrap(<TariffsTab tariffs={tariffs} currencyCode="TJS" backend={backend as never} canManageTariffs onReload={onReload} onFeedback={onFeedback} />);
    fireEvent.click(screen.getByText('Стандарт'));

    const [from] = screen.getAllByLabelText(/^(С|До)$/);
    fireEvent.change(from, { target: { value: '08:00' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    await waitFor(() => expect(onFeedback).toHaveBeenCalledWith(expect.objectContaining({ state: 'failed' })));
    expect(updateTariff).not.toHaveBeenCalled();
  });

  // Снятие с продажи — про доступность, а не про расписание. Стереть часы здесь значит вернуть
  // тариф из архива уже круглосуточным, по вечерней цене за утро.
  it('retiring a tariff keeps its hours', async () => {
    const morning = [{ ...tariffs[0], appliesOnDaysMask: 3, appliesFromMinuteOfDay: 480, appliesToMinuteOfDay: 960 }] as TariffOptionDto[];
    const { container } = wrap(
      <TariffsTab tariffs={morning} currencyCode="TJS" backend={backend as never} canManageTariffs onReload={onReload} onFeedback={onFeedback} />
    );
    fireEvent.click(within(container).getAllByRole('button', { name: 'Действия' })[0]);
    fireEvent.click(screen.getByRole('menuitem', { name: 'Снять тариф' }));
    fireEvent.click(within(screen.getByRole('alertdialog')).getByRole('button', { name: 'Снять тариф' }));

    // Расписание не передаётся вовсе — сервер оставляет часы как есть.
    await waitFor(() => expect(updateTariff).toHaveBeenCalledWith('b1', tariffId, {
      organizationId: 'org',
      name: 'Стандарт',
      isActive: false
    }));
  });

  it('shows the hours in the list so lookalike tariffs stay apart', () => {
    const morning = [{ ...tariffs[0], appliesFromMinuteOfDay: 480, appliesToMinuteOfDay: 960 }] as TariffOptionDto[];
    wrap(<TariffsTab tariffs={morning} currencyCode="TJS" backend={backend as never} canManageTariffs onReload={onReload} onFeedback={onFeedback} />);

    expect(screen.getByText('08:00–16:00')).toBeTruthy();
  });
  // Пустая маска означает «каждый день», и все семь дней показаны выбранными. Голый XOR по нулю
  // превращал первый же клик в противоположность: снимая субботу у ежедневного тарифа, владелец
  // получал тариф «только по субботам».
  it('unchecking one day of an everyday tariff leaves the other six', async () => {
    wrap(<TariffsTab tariffs={tariffs} currencyCode="TJS" backend={backend as never} canManageTariffs onReload={onReload} onFeedback={onFeedback} />);
    fireEvent.click(screen.getByText('Стандарт'));

    fireEvent.click(screen.getByRole('button', { name: 'Сб' }));
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    // 0b1111111 без субботы (шестой бит) = 95.
    await waitFor(() => expect(updateTariff).toHaveBeenCalledWith('b1', tariffId, expect.objectContaining({
      schedule: expect.objectContaining({ appliesOnDaysMask: 95 })
    })));
  });

  it('a weekday tariff loses exactly the day that was clicked', async () => {
    const weekdays = [{ ...tariffs[0], appliesOnDaysMask: 0b0011111 }] as TariffOptionDto[];
    wrap(<TariffsTab tariffs={weekdays} currencyCode="TJS" backend={backend as never} canManageTariffs onReload={onReload} onFeedback={onFeedback} />);
    fireEvent.click(screen.getByText('Стандарт'));

    fireEvent.click(screen.getByRole('button', { name: 'Пт' }));
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));

    // Пн-Чт: пятница снята, остальные не тронуты.
    await waitFor(() => expect(updateTariff).toHaveBeenCalledWith('b1', tariffId, expect.objectContaining({
      schedule: expect.objectContaining({ appliesOnDaysMask: 0b0001111 })
    })));
  });
});
