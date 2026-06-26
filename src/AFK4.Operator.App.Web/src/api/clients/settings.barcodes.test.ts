import { describe, it, expect, mock } from 'bun:test';
import { createSettingsClient } from './settings';

function fakeApi() {
  const calls: Array<{ method: string; path: string; body?: unknown }> = [];
  const api = {
    get: mock(async (path: string) => { calls.push({ method: 'GET', path }); return []; }),
    post: mock(async (path: string, body: Record<string, unknown>) => { calls.push({ method: 'POST', path, body }); return { barcodeId: 'b1', productId: 'p1', code: '111', isPrimary: true }; }),
    delete: mock(async (path: string) => { calls.push({ method: 'DELETE', path }); return null; }),
  };
  return { api, calls };
}

describe('settings barcode client', () => {
  it('GET barcodes hits the product barcodes path', async () => {
    const { api, calls } = fakeApi();
    const client = createSettingsClient(api as never);
    await client.getProductBarcodes('br1', 'p1');
    expect(calls[0]).toEqual({ method: 'GET', path: '/api/branches/br1/pos/products/p1/barcodes' });
  });

  it('POST barcode sends organizationId + code in body', async () => {
    const { api, calls } = fakeApi();
    const client = createSettingsClient(api as never);
    const res = await client.addProductBarcode('br1', 'p1', { organizationId: 'org1', code: '111', isPrimary: true });
    expect(calls[0].path).toBe('/api/branches/br1/pos/products/p1/barcodes');
    expect(calls[0].body).toMatchObject({ organizationId: 'org1', code: '111', isPrimary: true });
    expect(res.code).toBe('111');
  });

  it('DELETE barcode hits the barcode id path', async () => {
    const { api, calls } = fakeApi();
    const client = createSettingsClient(api as never);
    await client.deleteProductBarcode('br1', 'p1', 'b1');
    expect(calls[0]).toEqual({ method: 'DELETE', path: '/api/branches/br1/pos/products/p1/barcodes/b1' });
  });
});
