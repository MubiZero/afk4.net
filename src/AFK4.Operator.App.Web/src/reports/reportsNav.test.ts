import { describe, it, expect } from 'bun:test';
import { allowedReportsDestinations, reportsDestinations } from './reportsNav';
import { permissionNames } from '../permissionNames';

function session(permissions: string[]) {
  return { permissions } as never;
}

describe('reportsNav', () => {
  it('lists overview/history/journal in order', () => {
    expect(reportsDestinations.map((d) => d.id)).toEqual(['overview', 'history', 'journal']);
  });

  it('shows overview+history for reports.view, hides journal', () => {
    const ids = allowedReportsDestinations(session([permissionNames.viewReports])).map((d) => d.id);
    expect(ids).toEqual(['overview', 'history']);
  });

  it('shows only journal for audit.view alone', () => {
    const ids = allowedReportsDestinations(session([permissionNames.viewAudit])).map((d) => d.id);
    expect(ids).toEqual(['journal']);
  });

  it('shows all three when both permissions present', () => {
    const ids = allowedReportsDestinations(session([permissionNames.viewReports, permissionNames.viewAudit])).map((d) => d.id);
    expect(ids).toEqual(['overview', 'history', 'journal']);
  });

  it('hides section entirely with no relevant permission', () => {
    expect(allowedReportsDestinations(session([])).length).toBe(0);
  });
});
