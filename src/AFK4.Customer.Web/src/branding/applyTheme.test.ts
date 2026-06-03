import { it, expect } from 'bun:test';
import { applyTheme } from './applyTheme';
import type { TenantBrandingDto } from '../api/types';

it('writes the accent color into the --accent CSS variable', () => {
  applyTheme({ organizationId: 'o', name: 'CyberX', logoUrl: null, accentColor: '#ff0066' });
  expect(document.documentElement.style.getPropertyValue('--accent')).toBe('#ff0066');
  // dark magenta → white foreground for contrast
  expect(document.documentElement.style.getPropertyValue('--accent-fg')).toBe('#ffffff');
});

it('keeps the default accent when branding is null or has no color', () => {
  document.documentElement.style.removeProperty('--accent');
  applyTheme(null);
  expect(document.documentElement.style.getPropertyValue('--accent')).toBe('');
  applyTheme({ organizationId: 'o', name: 'X', logoUrl: null, accentColor: null });
  expect(document.documentElement.style.getPropertyValue('--accent')).toBe('');
});
