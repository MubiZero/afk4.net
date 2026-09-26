import { Dialog } from '@/components/ui/dialog';
import { ErrorBanner } from '@/components/ui/field';
import { Button } from '@/components/ui/button';
import { useI18n } from '@/i18n/I18nProvider';
import type { AdCreativeDto } from '@/api/types';
import { AdCardPreview } from './AdCreativePreview';

/**
 * Снять креатив с показа. Обратного действия нет — поэтому спрашиваем, а не снимаем по одному
 * нажатию. Если это последний одобренный креатив запущенной кампании, говорим, что кампания
 * пропадёт с ПК: иначе это выяснится только по отчёту.
 */
export function ArchiveCreativeDialog({ creative, advertiserName, lastOnAir, pending, error, onConfirm, onClose }: {
  creative: AdCreativeDto;
  advertiserName: string;
  lastOnAir: boolean;
  pending: boolean;
  /** Отказ сервера, уже переведённый в человеческую фразу. */
  error: string | null;
  onConfirm: () => void;
  onClose: () => void;
}) {
  const { t } = useI18n();
  return (
    <Dialog
      open
      title={t('platform.ads.creative.archiveTitle')}
      tone="warning"
      onClose={onClose}
      footer={
        <>
          <Button variant="outline" disabled={pending} onClick={onClose}>{t('common.cancel')}</Button>
          <Button variant="destructive" disabled={pending} onClick={onConfirm}>{t('platform.ads.creative.archive')}</Button>
        </>
      }
    >
      <div className="pc-panel-body">
        <ErrorBanner message={error} dismissLabel={t('common.close')} />
        <AdCardPreview creative={creative} advertiserName={advertiserName} />
        <p className="mgmt-drawer-hint">{t('platform.ads.creative.archiveHint')}</p>
        {lastOnAir ? <p className="pc-ad-flags" role="status">{t('platform.ads.creative.archiveLast')}</p> : null}
      </div>
    </Dialog>
  );
}
