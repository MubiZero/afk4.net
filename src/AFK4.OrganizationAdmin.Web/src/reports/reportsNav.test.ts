import { describe, it, expect } from 'bun:test';
import { allowedReportsDestinations, reportsDestinations } from './reportsNav';
import { permissionNames } from '../permissionNames';

function session(permissions: string[]) {
  return { permissions } as never;
}

describe('reportsNav', () => {
  it('lists summary/shifts-and-cash/revenue in order', () => {
    expect(reportsDestinations.map((d) => d.id)).toEqual(['summary', 'shiftsCash', 'revenue']);
  });

  it('shows all report tabs for reports.view', () => {
    const ids = allowedReportsDestinations(session([permissionNames.viewReports])).map((d) => d.id);
    expect(ids).toEqual(['summary', 'shiftsCash', 'revenue']);
  });

  it('shows no report tabs for audit.view alone', () => {
    const ids = allowedReportsDestinations(session([permissionNames.viewAudit])).map((d) => d.id);
    expect(ids).toEqual([]);
  });

  it('shows all three when both permissions present', () => {
    const ids = allowedReportsDestinations(session([permissionNames.viewReports, permissionNames.viewAudit])).map((d) => d.id);
    expect(ids).toEqual(['summary', 'shiftsCash', 'revenue']);
  });

  it('hides section entirely with no relevant permission', () => {
    expect(allowedReportsDestinations(session([])).length).toBe(0);
  });
});
