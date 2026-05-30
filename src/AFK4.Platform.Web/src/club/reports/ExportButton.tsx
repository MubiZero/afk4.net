import { useState } from 'react';
import { Button } from '@/components/ui/button';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import { saveBlob } from '@/lib/saveBlob';

export function ExportButton({ onExport, filename }: { onExport: () => Promise<Blob>; filename: string }) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [busy, setBusy] = useState(false);

  async function run() {
    setBusy(true);
    try {
      const blob = await onExport();
      saveBlob(blob, filename);
    } catch {
      toast({ title: t('reports.export.error'), variant: 'error' });
    } finally {
      setBusy(false);
    }
  }

  return (
    <Button variant="outline" size="sm" disabled={busy} onClick={() => void run()}>
      {t('reports.export')}
    </Button>
  );
}
