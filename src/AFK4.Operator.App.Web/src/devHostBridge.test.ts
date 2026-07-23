import { describe, expect, it } from 'bun:test';
import { createDevOperatorConfig } from './devHostBridge';

describe('devHostBridge preview config', () => {
  it('identifies the browser preview build in the system footer', () => {
    expect(createDevOperatorConfig()).toMatchObject({
      runtime: 'browser-dev',
      appVersion: 'dev'
    });
  });
});
