import { render, screen } from '@testing-library/react';
import { it, expect, mock } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import type { OwnerCodeSummary } from '@/api/types';
import { InstallScreen } from './InstallScreen';

function fakeClient() {
  return {
    getOwnerCode: mock<() => Promise<OwnerCodeSummary | null>>(async () => null),
    generateOwnerCode: mock(),
    rotateOwnerCode: mock()
  };
}

it('renders the install header, a download link, and the branch list', async () => {
  render(
    <I18nProvider><ToastProvider>
      <InstallScreen client={fakeClient() as never} canManage branches={[{ branchId: 'b1', name: 'Центр', city: 'Москва' }]} />
    </ToastProvider></I18nProvider>
  );
  expect(screen.getByText('Установка на ПК')).toBeInTheDocument();
  expect(screen.getByRole('link', { name: 'Скачать установщик MSI' })).toBeInTheDocument();
  expect(await screen.findByText('Центр')).toBeInTheDocument();
});
