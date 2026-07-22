import { describe, it, expect, mock, afterEach, afterAll } from 'bun:test';
import { render, screen, fireEvent, cleanup, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../../../operatorToast';
import type { EskhataConfigDto } from '../../../operatorApiClients';

let stored: EskhataConfigDto = { baseUrl: '', companyId: '', merchantId: 0, hashKeySet: false, status: 'inactive' };
const get = mock(async (): Promise<EskhataConfigDto> => stored);
const update = mock(async (req: { baseUrl: string; companyId: string; merchantId: number; hashKey: string | null }): Promise<EskhataConfigDto> => {
  stored = { baseUrl: req.baseUrl, companyId: req.companyId, merchantId: req.merchantId, hashKeySet: stored.hashKeySet || !!req.hashKey, status: 'configured' };
  return stored;
});

const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../../../operatorHelpers', () => ({
  ...actual,
  createAuthenticatedOperatorClients: () => ({ eskhataConfig: { get, update } })
}));

const { EskhataGatewayForm } = await import('./EskhataGatewayForm');

const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'o1' }, branchId: 'b1' } as never;

const view = () =>
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <EskhataGatewayForm backend={backend} />
      </ToastProvider>
    </I18nProvider>
  );

afterEach(() => {
  stored = { baseUrl: '', companyId: '', merchantId: 0, hashKeySet: false, status: 'inactive' };
  get.mockClear();
  update.mockClear();
  cleanup();
});
afterAll(() => mock.restore());

describe('EskhataGatewayForm', () => {
  it('keeps Save disabled until all required fields are valid, then saves', async () => {
    view();
    // Eskhata — тихий резервный блок: форма свёрнута, «Настроить» её раскрывает.
    fireEvent.click(await screen.findByRole('button', { name: /настроить/i }));
    const save = await screen.findByRole('button', { name: /сохранить/i });
    expect(save).toBeDisabled();

    fireEvent.change(screen.getByLabelText('Base URL'), { target: { value: 'https://merchant-api.example.tld' } });
    fireEvent.change(screen.getByLabelText('Company ID'), { target: { value: 'company-demo-001' } });
    fireEvent.change(screen.getByLabelText('Merchant ID'), { target: { value: '17' } });
    fireEvent.change(screen.getByLabelText('Hash key'), { target: { value: 'example-secret' } });
    expect(save).not.toBeDisabled();

    fireEvent.click(save);
    await waitFor(() => expect(update).toHaveBeenCalledWith({
      baseUrl: 'https://merchant-api.example.tld',
      companyId: 'company-demo-001',
      merchantId: 17,
      hashKey: 'example-secret'
    }));
  });

  it('shows the «задан» placeholder and allows saving without re-entering the hash key', async () => {
    stored = { baseUrl: 'https://merchant-api.example.tld', companyId: 'company-demo-001', merchantId: 17, hashKeySet: true, status: 'configured' };
    view();
    fireEvent.click(await screen.findByRole('button', { name: /настроить/i }));

    const hashInput = await screen.findByLabelText('Hash key');
    await waitFor(() => expect((hashInput as HTMLInputElement).placeholder).toMatch(/задан/i));

    const save = await screen.findByRole('button', { name: /сохранить/i });
    await waitFor(() => expect(save).not.toBeDisabled());
    fireEvent.click(save);
    await waitFor(() => expect(update).toHaveBeenCalledWith({
      baseUrl: 'https://merchant-api.example.tld',
      companyId: 'company-demo-001',
      merchantId: 17,
      hashKey: null
    }));
  });
});
