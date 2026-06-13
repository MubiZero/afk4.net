import { render, screen } from '@testing-library/react';
import { it, expect } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { InstallScreen } from './InstallScreen';

it('renders the install header, a download link, and the branch list', async () => {
  render(
    <I18nProvider><ToastProvider>
      <InstallScreen branches={[{ branchId: 'b1', name: 'Центр', city: 'Москва' }]} />
    </ToastProvider></I18nProvider>
  );
  expect(screen.getByText('Установка на ПК')).toBeInTheDocument();
  expect(screen.getByRole('link', { name: 'Скачать установщик MSI' })).toBeInTheDocument();
  expect(await screen.findByText('Центр')).toBeInTheDocument();
});
