import { describe, it, expect, mock, afterEach, afterAll } from 'bun:test';
import { render, screen, fireEvent, cleanup, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../operatorToast';
import { permissionNames } from '../../operatorPermissions';
import type { LoyaltySettingsDto, ReferralSettingsDto, EskhataConfigDto, DcPayLinkConfigDto } from '../../operatorApiClients';
import type { BirthdayGiftSettingsDto } from '../../api/clients/birthdayGiftSettings';

const loyaltyDefaults: LoyaltySettingsDto = {
  topUpEnabled: false, topUpPercentBasisPoints: 0,
  shopEnabled: false, shopPercentBasisPoints: 0,
  sessionEnabled: false, sessionPercentBasisPoints: 0,
  cashbackCapMinorUnits: 0, minimumSourceMinorUnits: 0
};
const loyaltyGet = mock(async (): Promise<LoyaltySettingsDto> => loyaltyDefaults);
const loyaltyUpdate = mock(async (req: LoyaltySettingsDto): Promise<LoyaltySettingsDto> => req);
const referralDefaults: ReferralSettingsDto = {
  enabled: false,
  referrerBonusMinorUnits: 0,
  inviteeBonusMinorUnits: 0,
  minimumTopUpMinorUnits: 0,
  claimWindowDays: 30,
  maxRewardedPerReferrer: 0
};
const referralGet = mock(async (): Promise<ReferralSettingsDto> => referralDefaults);
const referralUpdate = mock(async (req: ReferralSettingsDto): Promise<ReferralSettingsDto> => req);
const birthdayGiftGet = mock(async (): Promise<BirthdayGiftSettingsDto> => ({ enabled: false, amountMinorUnits: 0, recentVisitDays: 180 }));
const birthdayGiftUpdate = mock(async (req: BirthdayGiftSettingsDto): Promise<BirthdayGiftSettingsDto> => req);
const eskhataGet = mock(async (): Promise<EskhataConfigDto> => ({ baseUrl: '', companyId: '', merchantId: 0, hashKeySet: false, status: 'inactive' }));
const dcConfigGet = mock(async (): Promise<DcPayLinkConfigDto> => ({ cardSet: false, cardLast4: '', commentTemplate: 'AFK4-{ref}', isActive: false }));
// Default: loyalty feature enabled, so the existing zone-visibility tests above keep seeing the
// loyalty zone unless a test below overrides this with `featuresList.mockResolvedValueOnce(...)`.
const featuresList = mock(async (): Promise<string[]> => ['loyalty']);

const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../../operatorHelpers', () => ({
  ...actual,
  createAuthenticatedOperatorClients: () => ({
    loyaltySettings: { get: loyaltyGet, update: loyaltyUpdate },
    referralSettings: { get: referralGet, update: referralUpdate },
    birthdayGiftSettings: { get: birthdayGiftGet, update: birthdayGiftUpdate },
    eskhataConfig: { get: eskhataGet, update: mock(async () => ({})) },
    dcConfig: { get: dcConfigGet, update: mock(async () => ({})) },
    features: { list: featuresList }
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
  dcConfigGet.mockClear();
  featuresList.mockClear();
  cleanup();
});
afterAll(() => { mock.restore(); mock.module('../../operatorHelpers', () => actual); });

describe('PaymentsLoyaltyDestination (одна страница, без табов)', () => {
  // Одна связная страница: обе зоны стопкой, без таб-стрипа, и одна плашка сохранения на экран
  // (решение владельца 29.09) вместо трёх кнопок «Сохранить» под кэшбэком, приглашением и подарком.
  it('renders both zones with no tab strip and a single save bar', async () => {
    const { container } = view([permissionNames.managePaymentGateways, permissionNames.manageLoyaltySettings]);

    expect(screen.getByText(/eskhata merchant/i)).toBeInTheDocument();
    expect(await screen.findByLabelText(/кэшбек с пополнений/i)).toBeInTheDocument();
    expect(screen.queryByRole('tab')).toBeNull();
    expect(container.querySelectorAll('.management-save-bar')).toHaveLength(1);
    expect(screen.getAllByRole('button', { name: 'Сохранить' })).toHaveLength(1);
  });

  // Шлюзам нечего сохранять плашкой: их реквизиты — отдельная форма за «Настроить».
  it('shows no save bar when only the gateways are on screen', () => {
    const { container } = view([permissionNames.managePaymentGateways]);
    expect(container.querySelector('.management-save-bar')).toBeNull();
  });

  it('one save writes every changed section, and only those', async () => {
    view([permissionNames.manageLoyaltySettings]);
    fireEvent.click(await screen.findByLabelText(/кэшбек с пополнений/i));
    fireEvent.click(await screen.findByLabelText('Приглашение друзей'));
    fireEvent.change(screen.getByLabelText(/^Другу/), { target: { value: '10' } });
    referralUpdate.mockClear();
    birthdayGiftUpdate.mockClear();
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
    await waitFor(() => expect(loyaltyUpdate).toHaveBeenCalledTimes(1));
    await waitFor(() => expect(referralUpdate).toHaveBeenCalledWith(expect.objectContaining({ enabled: true })));
    expect(birthdayGiftUpdate).not.toHaveBeenCalled();
  });

  it('shows only the payment-methods zone for gateways-only permission', () => {
    view([permissionNames.managePaymentGateways]);
    expect(screen.getByText(/eskhata merchant/i)).toBeInTheDocument();
    expect(screen.queryByLabelText(/кэшбек с пополнений/i)).toBeNull();
    expect(screen.queryByRole('tab')).toBeNull();
  });

  it('shows only the loyalty zone for loyalty-only permission', async () => {
    view([permissionNames.manageLoyaltySettings]);
    expect(await screen.findByLabelText(/кэшбек с пополнений/i)).toBeInTheDocument();
    expect(screen.queryByText(/eskhata merchant/i)).toBeNull();
    expect(screen.queryByRole('tab')).toBeNull();
  });

  it('keeps the save bar disabled until something changes', async () => {
    view([permissionNames.manageLoyaltySettings]);
    await screen.findByLabelText(/кэшбек с пополнений/i);
    const saveButton = screen.getByRole('button', { name: 'Сохранить' });
    expect(saveButton).toBeDisabled();
    fireEvent.click(screen.getByLabelText(/кэшбек с пополнений/i));
    expect(saveButton).toBeEnabled();
  });

  it('saves loyalty percents in basis points from the screen save bar', async () => {
    view([permissionNames.manageLoyaltySettings]);
    const toggle = await screen.findByLabelText(/кэшбек с пополнений/i);
    fireEvent.click(toggle);
    fireEvent.change(screen.getByLabelText(/процент с пополнений/i), { target: { value: '5' } });
    fireEvent.click(screen.getByRole('button', { name: 'Сохранить' }));
    await waitFor(() => expect(loyaltyUpdate).toHaveBeenCalledWith(expect.objectContaining({
      topUpEnabled: true,
      topUpPercentBasisPoints: 500
    })));
  });

  it('shows a live accrual example when a rule is enabled', async () => {
    view([permissionNames.manageLoyaltySettings]);
    const toggle = await screen.findByLabelText(/кэшбек с пополнений/i);
    fireEvent.click(toggle);
    fireEvent.change(screen.getByLabelText(/процент с пополнений/i), { target: { value: '10' } });
    // 10% со 100 → +10.00 (Money signed рендерит с «+»)
    expect(await screen.findByText(/\+10/)).toBeInTheDocument();
  });

  it('hides the accrual example when the rule is disabled', async () => {
    view([permissionNames.manageLoyaltySettings]);
    await screen.findByLabelText(/кэшбек с пополнений/i);
    // Percent has a value but the rule stays off → example must not render.
    fireEvent.change(screen.getByLabelText(/процент с пополнений/i), { target: { value: '10' } });
    expect(screen.queryByText(/\+10/)).toBeNull();
  });

  it('прячет раздел «Лояльность», когда фича выключена', async () => {
    featuresList.mockResolvedValueOnce([]);
    view([permissionNames.managePaymentGateways, permissionNames.manageLoyaltySettings]);

    // The payments zone (unrelated to the loyalty feature flag) stays visible...
    await screen.findByText(/eskhata merchant/i);
    // ...but the loyalty zone disappears entirely, not just its settings.
    await waitFor(() => expect(screen.queryByLabelText(/кэшбек с пополнений/i)).toBeNull());
    expect(screen.queryByText(/как вы возвращаете/i)).toBeNull();
  });

  it('показывает раздел «Лояльность», когда фича включена', async () => {
    featuresList.mockResolvedValueOnce(['loyalty']);
    view([permissionNames.managePaymentGateways, permissionNames.manageLoyaltySettings]);

    expect(await screen.findByLabelText(/кэшбек с пополнений/i)).toBeInTheDocument();
  });
});
