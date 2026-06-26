import { describe, it, expect, mock, beforeEach, afterEach, afterAll } from 'bun:test';
import { render, screen, fireEvent, cleanup, waitFor } from '@testing-library/react';
import { I18nProvider } from '@afk4/i18n';
import { ToastProvider } from '../operatorToast';
import type { ProductBarcodeDto } from '../api/clients/settings';

const getProductBarcodes = mock(async (_branchId: string, _productId: string): Promise<ProductBarcodeDto[]> => []);
const addProductBarcode = mock(async (_branchId: string, _productId: string, _req: Record<string, unknown>): Promise<ProductBarcodeDto> =>
  ({ barcodeId: 'b2', productId: 'p1', code: '222', isPrimary: false }));
const deleteProductBarcode = mock(async (_branchId: string, _productId: string, _barcodeId: string): Promise<void> => { });

const actual = (globalThis as Record<string, unknown>).__afk4RealOperatorHelpers as Record<string, unknown>;
mock.module('../operatorHelpers', () => ({
  ...actual,
  createAuthenticatedOperatorClients: () => ({
    settings: { getProductBarcodes, addProductBarcode, deleteProductBarcode }
  })
}));

const { ProductBarcodesSection } = await import('./ProductBarcodesSection');

const backend = { config: { platformBaseUrl: 'http://x' }, session: { accessToken: 't', organizationId: 'org1' }, branchId: 'br1' } as never;

const view = (productId = 'p1', canManage = true) =>
  render(
    <I18nProvider initialLocale="ru">
      <ToastProvider>
        <ProductBarcodesSection productId={productId} backend={backend} organizationId="org1" canManage={canManage} />
      </ToastProvider>
    </I18nProvider>
  );

afterEach(() => {
  getProductBarcodes.mockClear();
  addProductBarcode.mockClear();
  deleteProductBarcode.mockClear();
  cleanup();
});
afterAll(() => mock.restore());

describe('ProductBarcodesSection', () => {
  it('отображает пустое состояние когда штрихкодов нет', async () => {
    getProductBarcodes.mockResolvedValueOnce([]);
    view();
    expect(await screen.findByText('Штрих-коды не привязаны')).toBeInTheDocument();
  });

  it('помечает primary-штрихкод лейблом «Основной»', async () => {
    getProductBarcodes.mockResolvedValueOnce([
      { barcodeId: 'b1', productId: 'p1', code: '111', isPrimary: true }
    ]);
    view();
    expect(await screen.findByText('111')).toBeInTheDocument();
    expect(await screen.findByText('Основной')).toBeInTheDocument();
  });

  it('добавляет штрихкод через ручной ввод', async () => {
    getProductBarcodes.mockResolvedValueOnce([]);
    // после добавления рефетч возвращает новый штрихкод
    getProductBarcodes.mockResolvedValueOnce([
      { barcodeId: 'b2', productId: 'p1', code: '222', isPrimary: false }
    ]);
    view();
    await screen.findByText('Штрих-коды не привязаны');
    fireEvent.change(screen.getByPlaceholderText('Введите или отсканируйте код'), { target: { value: '222' } });
    fireEvent.click(screen.getByRole('button', { name: 'Добавить' }));
    await waitFor(() => expect(addProductBarcode).toHaveBeenCalledTimes(1));
    const [, , req] = addProductBarcode.mock.calls[0] as [string, string, Record<string, unknown>];
    expect(req).toMatchObject({ code: '222', organizationId: 'org1' });
    expect(await screen.findByText('222')).toBeInTheDocument();
  });

  it('показывает ошибку при дубликате', async () => {
    getProductBarcodes.mockResolvedValueOnce([
      { barcodeId: 'b1', productId: 'p1', code: '111', isPrimary: true }
    ]);
    view();
    await screen.findByText('111');
    fireEvent.change(screen.getByPlaceholderText('Введите или отсканируйте код'), { target: { value: '111' } });
    fireEvent.click(screen.getByRole('button', { name: 'Добавить' }));
    expect(await screen.findByText('Этот штрих-код уже привязан к товару')).toBeInTheDocument();
    expect(addProductBarcode).not.toHaveBeenCalled();
  });

  it('удаляет штрихкод по кнопке', async () => {
    getProductBarcodes.mockResolvedValueOnce([
      { barcodeId: 'b1', productId: 'p1', code: '111', isPrimary: false }
    ]);
    getProductBarcodes.mockResolvedValueOnce([]);
    view();
    await screen.findByText('111');
    fireEvent.click(screen.getByRole('button', { name: 'Удалить штрих-код' }));
    await waitFor(() => expect(deleteProductBarcode).toHaveBeenCalledTimes(1));
    const [, , barcodeId] = deleteProductBarcode.mock.calls[0] as [string, string, string];
    expect(barcodeId).toBe('b1');
    expect(await screen.findByText('Штрих-коды не привязаны')).toBeInTheDocument();
  });

  it('скрывает поля ввода при canManage=false', async () => {
    getProductBarcodes.mockResolvedValueOnce([]);
    view('p1', false);
    await screen.findByText('Штрих-коды не привязаны');
    expect(screen.queryByPlaceholderText('Введите или отсканируйте код')).toBeNull();
    expect(screen.queryByRole('button', { name: 'Добавить' })).toBeNull();
  });
});
