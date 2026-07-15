import { describe, expect, it } from 'bun:test';
import { createDevOperatorConfig, toDevSession } from './devHostBridge';

describe('devHostBridge preview config', () => {
  it('identifies the browser preview build in the system footer', () => {
    expect(createDevOperatorConfig()).toMatchObject({
      runtime: 'browser-dev',
      appVersion: 'dev'
    });
  });

  it('keeps backend roles when projecting a live dev sign-in response', () => {
    expect(toDevSession({
      branchIds: ['branch-1'],
      roleNames: ['shift_supervisor']
    })).toMatchObject({
      activeBranchId: 'branch-1',
      roleNames: ['shift_supervisor']
    });
  });
});
