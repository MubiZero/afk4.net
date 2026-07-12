import { describe, expect, it } from 'bun:test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

const mapGridCss = readFileSync(join(import.meta.dir, '06-map-grid.css'), 'utf8');

describe('operator QA visual guards', () => {
  it('keeps the enabled session-start confirmation CTA visually primary', () => {
    expect(mapGridCss).toContain('.critical-confirmation-actions .cta-primary:not(:disabled)');
  });
});
