import { describe, expect, it, mock } from 'bun:test';
import { quickActions, getVisibleQuickActions, createCommandRegistry } from './operatorCommands';
import type { OperatorAuthSession } from './authClient';

const session = (perms: string[]) => ({ permissions: perms } as unknown as OperatorAuthSession);

describe('quickActions data', () => {
  it('declares exactly 8 actions with unique ids', () => {
    expect(quickActions).toHaveLength(8);
    expect(new Set(quickActions.map((a) => a.id)).size).toBe(8);
  });
});
describe('getVisibleQuickActions', () => {
  it('keeps only actions whose permission the session holds', () => {
    const visible = getVisibleQuickActions(session(['organization.players.create']));
    expect(visible.map((a) => a.id)).toEqual(['create_player']);
  });
  it('returns nothing for a permission-less session', () => {
    expect(getVisibleQuickActions(session([]))).toHaveLength(0);
  });
});
describe('createCommandRegistry', () => {
  it('dispatches to a registered handler and reports true', () => {
    const reg = createCommandRegistry();
    const handler = mock(() => {});
    reg.register('sell_product', handler);
    expect(reg.dispatch('sell_product')).toBe(true);
    expect(handler).toHaveBeenCalledTimes(1);
  });
  it('returns false when no handler is registered', () => {
    expect(createCommandRegistry().dispatch('sell_product')).toBe(false);
  });
});
