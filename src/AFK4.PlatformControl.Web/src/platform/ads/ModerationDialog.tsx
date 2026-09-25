import { useState } from 'react';
import { Dialog } from '@/components/ui/dialog';
import { ErrorBanner, Field, fieldErrorId } from '@/components/ui/field';
import { Textarea } from '@/components/ui/textarea';
import { Button } from '@/components/ui/button';
import { useI18n } from '@/i18n/I18nProvider';
import type { AdCreativeDto } from '@/api/types';
import { AD_LIMITS } from './adsModel';
import { AdImagePreview } from './AdImages';

const REASON_ID = 'moderation-reason';

/**
 * Одобрить или отклонить креатив. Модератор видит креатив так, как его увидит игрок, — с подписью
 * рекламодателя: по ней и видно, не другой ли это клуб.
 *
 * Одобрение требует отметки: код не отличит рекламу пива от рекламы сока, и отметка — это
 * решение человека, которое уходит в журнал. Отказ требует причины: её передадут рекламодателю.
 */
export function ModerationDialog({ mode, creative, advertiserName, pending, error, onConfirm, onClose }: {
  mode: 'approve' | 'reject';
  creative: AdCreativeDto;
  advertiserName: string;
  pending: boolean;
  /** Отказ сервера, уже переведённый в человеческую фразу. */
  error: string | null;
  onConfirm: (reason: string | null) => void;
  onClose: () => void;
}) {
  const { t } = useI18n();
  const [confirmed, setConfirmed] = useState(false);
  const [reason, setReason] = useState('');

  const trimmed = reason.trim();
  const reasonTooLong = trimmed.length > AD_LIMITS.reasonMax;
  const canConfirm = !pending && (mode === 'approve' ? confirmed : trimmed !== '' && !reasonTooLong);

  return (
    <Dialog
      open
      title={t(mode === 'approve' ? 'platform.ads.moderation.approveTitle' : 'platform.ads.moderation.rejectTitle')}
      tone={mode === 'reject' ? 'danger' : undefined}
      onClose={onClose}
      footer={
        <>
          <Button variant="outline" disabled={pending} onClick={onClose}>{t('common.cancel')}</Button>
          <Button
            variant={mode === 'reject' ? 'destructive' : 'default'}
            disabled={!canConfirm}
            onClick={() => onConfirm(mode === 'approve' ? null : trimmed)}
          >
            {t(mode === 'approve' ? 'platform.ads.moderation.approve' : 'platform.ads.moderation.reject')}
          </Button>
        </>
      }
    >
      <div className="pc-panel-body">
        <ErrorBanner message={error} dismissLabel={t('common.close')} />

        <figure className="pc-ad-card">
          <figcaption className="pc-ad-card-label">{t('platform.ads.preview.label', { advertiser: advertiserName })}</figcaption>
          <strong>{creative.title}</strong>
          {creative.body !== null ? <p>{creative.body}</p> : null}
          {creative.imageUrl !== null ? <AdImagePreview url={creative.imageUrl} /> : null}
        </figure>

        {mode === 'approve' ? (
          <>
            <p className="mgmt-drawer-hint">{t('platform.ads.moderation.approveHint')}</p>
            <label className="pc-check-row">
              <input
                type="checkbox"
                className="pc-checkbox"
                checked={confirmed}
                onChange={event => setConfirmed(event.target.checked)}
              />
              {t('platform.ads.moderation.confirmAllowed')}
            </label>
          </>
        ) : (
          <div className="mgmt-form">
            <Field
              label={t('platform.ads.moderation.reason')}
              htmlFor={REASON_ID}
              hint={t('platform.ads.moderation.reasonHint')}
              error={reasonTooLong ? t('platform.ads.error.reasonTooLong', { max: AD_LIMITS.reasonMax }) : undefined}
            >
              <Textarea
                id={REASON_ID}
                rows={3}
                aria-invalid={reasonTooLong ? true : undefined}
                aria-describedby={reasonTooLong ? fieldErrorId(REASON_ID) : undefined}
                value={reason}
                onChange={event => setReason(event.target.value)}
              />
            </Field>
          </div>
        )}
      </div>
    </Dialog>
  );
}
