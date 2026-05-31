import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { it, expect, mock, spyOn } from 'bun:test';
import { I18nProvider } from '@/i18n/I18nProvider';
import { ToastProvider } from '@/components/ui/toast';
import { ExportButton } from './ExportButton';
import * as saveBlobModule from '@/lib/saveBlob';

it('calls onExport then saveBlob with the filename', async () => {
  const blob = new Blob(['x']);
  const onExport = mock<() => Promise<Blob>>(async () => blob);
  const save = spyOn(saveBlobModule, 'saveBlob').mockImplementation(() => {});
  render(
    <I18nProvider><ToastProvider>
      <ExportButton onExport={onExport} filename="sales.csv" />
    </ToastProvider></I18nProvider>
  );
  fireEvent.click(screen.getByRole('button', { name: 'Экспорт CSV' }));
  await waitFor(() => expect(onExport).toHaveBeenCalled());
  await waitFor(() => expect(save).toHaveBeenCalledWith(blob, 'sales.csv'));
});
