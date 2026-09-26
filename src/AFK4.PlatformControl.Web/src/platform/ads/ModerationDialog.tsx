import { useState } from 'react';
import type { AdModerationCheckName } from '@afk4/contracts';
import { Dialog } from '@/components/ui/dialog';
import { ErrorBanner, Field, fieldErrorId } from '@/components/ui/field';
import { Textarea } from '@/components/ui/textarea';
import { Button } from '@/components/ui/button';
import { useI18n } from '@/i18n/I18nProvider';
import type { AdCreativeDto } from '@/api/types';
import { AD_LIMITS, AD_MODERATION_CHECKS, formatWordingFlags, moderationCheckLabelKey } from './adsModel';
import { AdCardPreview } from './AdCreativePreview';

const REASON_ID = 'moderation-reason';

export interface ModerationDecision {
  reason: string | null;
  confirmed: AdModerationCheckName[];
}

/**
 * Одобрить или отклонить креатив. Модератор видит креатив так, как его увидит игрок, — с подписью
 * рекламодателя: по ней и видно, не другой ли это клуб.
 *
 * Одобрение требует отметить каждую строку закона, которую код не проверит (спека рекламы, §8.2):
 * отметки уходят в журнал как решение человека. Отказ требует причины: её передадут рекламодателю.
 */
export function ModerationDialog({ mode, creative, advertiserName, pending, error, onConfirm, onClose }: {
  mode: 'approve' | 'reject';
  creative: AdCreativeDto;
  advertiserName: string;
  pending: boolean;
  /** Отказ сервера, уже переведённый в человеческую фразу. */
  error: string | null;
  onConfirm: (decision: ModerationDecision) => void;
  onClose: () => void;
}) {
  const { t } = useI18n();
  const [confirmed, setConfirmed] = useState<ReadonlySet<AdModerationCheckName>>(new Set());
  const [reason, setReason] = useState('');

  const trimmed = reason.trim();
  const reasonTooLong = trimmed.length > AD_LIMITS.reasonMax;
  const allConfirmed = AD_MODERATION_CHECKS.every(check => confirmed.has(check));
  const canConfirm = !pending && (mode === 'approve' ? allConfirmed : trimmed !== '' && !reasonTooLong);
  const flaggedWords = formatWordingFlags(creative.wordingFlags);

  function toggle(check: AdModerationCheckName, checked: boolean) {
    setConfirmed(previous => {
      const next = new Set(previous);
      if (checked) next.add(check);
      else next.delete(check);
      return next;
    });
  }

  function confirm() {
    onConfirm(mode === 'approve'
      ? { reason: null, confirmed: AD_MODERATION_CHECKS.filter(check => confirmed.has(check)) }
      : { reason: trimmed, confirmed: [] });
  }

  return (
    <Dialog
      open
      title={t(mode === 'approve' ? 'platform.ads.moderation.approveTitle' : 'platform.ads.moderation.rejectTitle')}
      tone={mode === 'reject' ? 'danger' : undefined}
      onClose={onClose}
      footer={
        <>
          <Button variant="outline" disabled={pending} onClick={onClose}>{t('common.cancel')}</Button>
          <Button variant={mode === 'reject' ? 'destructive' : 'default'} disabled={!canConfirm} onClick={confirm}>
            {t(mode === 'approve' ? 'platform.ads.moderation.approve' : 'platform.ads.moderation.reject')}
          </Button>
        </>
      }
    >
      <div className="pc-panel-body">
        <ErrorBanner message={error} dismissLabel={t('common.close')} />

        <AdCardPreview creative={creative} advertiserName={advertiserName} />

        {/* Подсветка, а не запрет: «лучший» с документом закон разрешает (ст. 7). Решает модератор. */}
        {flaggedWords !== null ? (
          <p className="pc-ad-flags" role="status">{t('platform.ads.moderation.wordingFlags', { words: flaggedWords })}</p>
        ) : null}

        {mode === 'approve' ? (
          <>
            <p className="mgmt-drawer-hint">{t('platform.ads.moderation.approveHint')}</p>
            <fieldset className="pc-ad-checklist">
              <legend>{t('platform.ads.moderation.checklist')}</legend>
              {AD_MODERATION_CHECKS.map(check => (
                <label key={check} className="pc-check-row">
                  <input
                    type="checkbox"
                    className="pc-checkbox"
                    checked={confirmed.has(check)}
                    onChange={event => toggle(check, event.target.checked)}
                  />
                  {t(moderationCheckLabelKey(check))}
                </label>
              ))}
            </fieldset>
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
