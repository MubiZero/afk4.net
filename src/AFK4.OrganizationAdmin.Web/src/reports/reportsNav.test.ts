import { describe, it, expect } from 'bun:test';
import { allowedReportsDestinations, reportsDestinations } from './reportsNav';
import { permissionNames } from '../permissionNames';

function session(permissions: string[]) {
  return { permissions } as never;
}

describe('reportsNav', () => {
  it('перечисляет вкладки в порядке показа', () => {
    expect(reportsDestinations.map((d) => d.id)).toEqual(['summary', 'shiftsCash', 'revenue', 'gameplay', 'operatorActions', 'schedules']);
  });

  it('shows all report tabs for reports.view', () => {
    const ids = allowedReportsDestinations(session([permissionNames.viewReports])).map((d) => d.id);
    expect(ids).toEqual(['summary', 'shiftsCash', 'revenue', 'gameplay', 'operatorActions', 'schedules']);
  });

  it('shows no report tabs for audit.view alone', () => {
    const ids = allowedReportsDestinations(session([permissionNames.viewAudit])).map((d) => d.id);
    expect(ids).toEqual([]);
  });

  it('shows every tab when both permissions present', () => {
    const ids = allowedReportsDestinations(session([permissionNames.viewReports, permissionNames.viewAudit])).map((d) => d.id);
    expect(ids).toEqual(['summary', 'shiftsCash', 'revenue', 'gameplay', 'operatorActions', 'schedules']);
  });

  it('hides section entirely with no relevant permission', () => {
    expect(allowedReportsDestinations(session([])).length).toBe(0);
  });
});
