import { describe, expect, it } from 'bun:test';
import { INVITE_STATUS_VARIANT, INVITE_STATUS_LABEL } from './organizationsModel';

describe('organizationsModel invite status maps', () => {
  it('maps every invite status to a variant and label', () => {
    for (const s of ['pending', 'accepted', 'revoked', 'expired']) {
      expect(INVITE_STATUS_VARIANT[s]).toBeTruthy();
      expect(INVITE_STATUS_LABEL[s]).toContain('platform.organization.invites.status.');
    }
  });
});
