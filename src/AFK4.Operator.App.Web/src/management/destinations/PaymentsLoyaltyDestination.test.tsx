import { describe, it, expect, mock, afterEach, afterAll } from 'bun:test';
import { render, screen, fireEvent, cleanup, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../operatorToast';
import { permissionNames } from '../../operatorPermissions';
import type { LoyaltySettingsDto, EskhataConfigDto } from '../../operatorApiClients';

const loyaltyDefaults: LoyaltySettingsDto = {
  topUpEnabled: false, topUpPercentBasisPoints: 0,
  shopEnabled: false, shopPercentBasisPoints: 0,
  sessionEnabled: false, sessionPercentBasisPoints: 0,
  cashbackCapMinorUnits: 0, minimumSourceMinorUnits: 0
};
const loyaltyGet = mock(async (): Promise<LoyaltySettingsDto> => loyaltyDefaults);
const loyaltyUpdate = mock(async (req: LoyaltySettingsDto): Promise<LoyaltySettingsDto> => req);
const eskhataGet = mock(async (): Promise<EskhataConfigDto> => ({ baseUrl: '', companyId: '', merchantId: 0, hashKeySet: false, status: 'inactive' }));

const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../../operatorHelpers', () => ({
  ...actual,
  createAuthenticatedOperatorClients: () => ({
    loyaltySettings: { get: loyaltyGet, update: loyaltyUpdate },
    eskhataConfig: { get: eskhataGet, update: mock(async () => ({})) }
  })
}));

const { PaymentsLoyaltyDestination } = await import('./PaymentsLoyaltyDestination');

const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o1' }, branchId: 'b1' } as never;
const session = (perms: string[]) => ({ permissions: perms, organizationId: 'o1' }) as never;

const view = (perms: string[]) =>
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <PaymentsLoyaltyDestination backend={backend} session={session(perms)} currencyCode="TJS" />
      </ToastProvider>
    </I18nProvider>
  );

afterEach(() => {
  loyaltyGet.mockClear();
  loyaltyUpdate.mockClear();
  eskhataGet.mockClear();
  cleanup();
});
afterAll(() => mock.restore());

describe('PaymentsLoyaltyDestination (одна страница, без табов)', () => {
  // Одна связная страница: обе зоны стопкой, без таб-стрипа и без глобального save-бара
  // ManagementScreen (каждая зона сохраняется своей кнопкой).
  it('renders both zones with no tab strip and no shared save bar', async () => {
    const { container } = view([permissionNames.managePaymentGateways, permissionNames.manageLoyaltySettings]);

    expect(screen.getByText(/eskhata merchant/i)).toBeInTheDocument();
    expect(await screen.findByLabelText(/кэшбэк с пополнений/i)).toBeInTheDocument();
    expect(screen.queryByRole('tab')).toBeNull();
    expect(container.querySelector('.management-save-bar')).toBeNull();
  });

  it('shows only the payment-methods zone for gateways-only permission', () => {
    view([permissionNames.managePaymentGateways]);
    expect(screen.getByText(/eskhata merchant/i)).toBeInTheDocument();
    expect(screen.queryByLabelText(/кэшбэк с пополнений/i)).toBeNull();
    expect(screen.queryByRole('tab')).toBeNull();
  });

  it('shows only the loyalty zone for loyalty-only permission', async () => {
    view([permissionNames.manageLoyaltySettings]);
    expect(await screen.findByLabelText(/кэшбэк с пополнений/i)).toBeInTheDocument();
    expect(screen.queryByText(/eskhata merchant/i)).toBeNull();
    expect(screen.queryByRole('tab')).toBeNull();
  });

  it('keeps the loyalty section save button disabled until something changes', async () => {
    view([permissionNames.manageLoyaltySettings]);
    await screen.findByLabelText(/кэшбэк с пополнений/i);
    const saveButton = screen.getByRole('button', { name: /сохранить/i });
    expect(saveButton).toBeDisabled();
    fireEvent.click(screen.getByLabelText(/кэшбэк с пополнений/i));
    expect(saveButton).toBeEnabled();
  });

  it('saves loyalty percents in basis points from its own section button', async () => {
    view([permissionNames.manageLoyaltySettings]);
    const toggle = await screen.findByLabelText(/кэшбэк с пополнений/i);
    fireEvent.click(toggle);
    fireEvent.change(screen.getByLabelText(/процент с пополнений/i), { target: { value: '5' } });
    fireEvent.click(screen.getByRole('button', { name: /сохранить/i }));
    await waitFor(() => expect(loyaltyUpdate).toHaveBeenCalledWith(expect.objectContaining({
      topUpEnabled: true,
      topUpPercentBasisPoints: 500
    })));
  });

  it('shows a live accrual example when a rule is enabled', async () => {
    view([permissionNames.manageLoyaltySettings]);
    const toggle = await screen.findByLabelText(/кэшбэк с пополнений/i);
    fireEvent.click(toggle);
    fireEvent.change(screen.getByLabelText(/процент с пополнений/i), { target: { value: '10' } });
    // 10% со 100 → +10.00 (Money signed рендерит с «+»)
    expect(await screen.findByText(/\+10/)).toBeInTheDocument();
  });

  it('hides the accrual example when the rule is disabled', async () => {
    view([permissionNames.manageLoyaltySettings]);
    await screen.findByLabelText(/кэшбэк с пополнений/i);
    // Percent has a value but the rule stays off → example must not render.
    fireEvent.change(screen.getByLabelText(/процент с пополнений/i), { target: { value: '10' } });
    expect(screen.queryByText(/\+10/)).toBeNull();
  });
});
