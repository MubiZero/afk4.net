import { render, screen, fireEvent } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { TenantsTable } from './TenantsTable';
import type { TenantRow } from './tenantsModel';

const rows: TenantRow[] = [{
  organizationId: 'o1', name: 'Acme', slug: 'acme', status: 'active',
  planCode: 'starter', subscriptionStatus: 'active', branchCount: 2, updatedAtUtc: '2026-01-01T00:00:00Z'
}];

it('renders rows and fires onSelect on row click', () => {
  const onSelect = mock();
  render(<I18nProvider><TenantsTable rows={rows} selectedId={null} emptyMessage="none" onSelect={onSelect} /></I18nProvider>);
  fireEvent.click(screen.getByText('Acme'));
  expect(onSelect).toHaveBeenCalledWith('o1');
});

it('shows the empty message when there are no rows', () => {
  render(<I18nProvider><TenantsTable rows={[]} selectedId={null} emptyMessage="No tenants found." onSelect={() => {}} /></I18nProvider>);
  expect(screen.getByText('No tenants found.')).toBeInTheDocument();
});
