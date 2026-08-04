import { describe, expect, it } from 'bun:test';
import { readFileSync } from 'node:fs';
import { join } from 'node:path';

const workspacePrimitivesCss = readFileSync(join(import.meta.dir, '16-workspace-primitives.css'), 'utf8');
const mapCss = readFileSync(join(import.meta.dir, '06-map-grid.css'), 'utf8');
const posCss = readFileSync(join(import.meta.dir, '11-pos.css'), 'utf8');
const playersCss = readFileSync(join(import.meta.dir, '12-players.css'), 'utf8');
const cashCss = readFileSync(join(import.meta.dir, '21-cash.css'), 'utf8');
const statusCss = readFileSync(join(import.meta.dir, '17-status-bar.css'), 'utf8');

describe('operator QA visual guards', () => {
  it('keeps the enabled session-start confirmation CTA visually primary', () => {
    expect(workspacePrimitivesCss).toContain('.critical-confirmation-actions .cta-primary:not(:disabled)');
  });

  // Гварды по общему слою (.auth-*, .ui-*, .ctable-*) переехали в packages/ui/styles.test.ts
  // вместе со стилями — здесь остаются только экраны оператора.
  it('keeps dense small text on AA-capable foreground tokens', () => {
    expect(mapCss).toMatch(/\.seat-invite-label\s*\{[^}]*color:\s*var\(--text-secondary\)/s);
    expect(posCss).toMatch(/\.pos-order-chevron\s*\{[^}]*color:\s*var\(--text-secondary\)/s);
    expect(posCss).toMatch(/\.pos-product-card > em\s*\{[^}]*color:\s*var\(--text-secondary\)/s);
    expect(playersCss).toMatch(/\.cc-phone\s*\{[^}]*color:\s*var\(--text-secondary\)/s);
    expect(playersCss).toMatch(/\.cc-visit\s*\{[^}]*color:\s*var\(--text-secondary\)/s);
  });

  it('disables order disclosure motion when reduced motion is requested', () => {
    expect(posCss).toMatch(
      /@media \(prefers-reduced-motion: reduce\)[\s\S]*\.pos-order-chevron\s*\{[^}]*transition:\s*none/s
    );
  });

  it('keeps cash registers stable and provides a reduced-motion responsive inspector', () => {
    expect(cashCss).toContain(".cash-register-row[aria-selected='true']");
    expect(cashCss).toContain('@media (max-width: 1180px)');
    expect(cashCss).toContain('@media (prefers-reduced-motion: reduce)');
    expect(cashCss).not.toMatch(/\.cash-register-row[^}]*transform:\s*translate/);
  });

  it('keeps the shift reconciliation and cash ledger readable at desktop scale', () => {
    expect(cashCss).toMatch(/\.cash-shift-reconcile-band strong \.ui-money\s*\{[^}]*font-size:\s*inherit/s);
    expect(cashCss).toMatch(/\.cash-shift-main-grid\s*\{[^}]*flex:\s*1/s);
    expect(cashCss).toMatch(/\.cash-shift-movements li\s*\{[^}]*font-size:\s*15px/s);
  });

  it('lets the stacked shift layout grow naturally instead of overlapping at narrow widths', () => {
    expect(cashCss).toMatch(/@media \(max-width: 1180px\)[\s\S]*\.cash-shift-main-grid\s*\{[^}]*flex:\s*none[^}]*grid-template-columns:\s*1fr/s);
    expect(cashCss).toMatch(/\.cash-shift-status-card,\s*\.cash-shift-reconcile-band\s*\{[^}]*flex:\s*none/s);
  });

  it('keeps the system footer on one line with separated, truncatable fields', () => {
    expect(statusCss).toMatch(/\.signals-strip\s*\{[^}]*white-space:\s*nowrap/s);
    expect(statusCss).toMatch(/\.signal-field\s*\{[^}]*border-left:\s*1px solid/s);
    expect(statusCss).toMatch(/\.signal-left\s*\{[^}]*overflow:\s*hidden/s);
    expect(statusCss).not.toContain('.signal-pos');
  });
});
