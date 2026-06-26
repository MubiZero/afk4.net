import { describe, it, expect } from 'bun:test';
import { visibleStockTabs, STOCK_TAB_ORDER } from './stockModel';

describe('stockModel', () => {
  it('levels виден при inventory.view', () => {
    const session = { permissions: ['inventory.view'] } as never;
    expect(visibleStockTabs(session)).toContain('levels');
  });
  it('пустые права → нет вкладок', () => {
    const session = { permissions: [] } as never;
    expect(visibleStockTabs(session)).toEqual([]);
  });
  it('порядок стабилен', () => {
    expect(STOCK_TAB_ORDER[0]).toBe('levels');
  });
  it('receiving виден только при праве управления складом', () => {
    expect(visibleStockTabs({ permissions: ['inventory.stock.manage'] } as never)).toContain('receiving');
    expect(visibleStockTabs({ permissions: ['inventory.view'] } as never)).not.toContain('receiving');
  });
});
