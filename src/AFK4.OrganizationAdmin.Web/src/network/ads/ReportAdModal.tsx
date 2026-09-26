import { useState } from 'react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { AdComplaintReasonNames, type AdComplaintReasonName, type ClubAdDto, type ClubAdsDto } from '@afk4/contracts';
import { PanelModal } from '../../PanelModal';
import { projectOperatorError } from '../../apiErrors';

export interface ReportAdClient {
  reportPlatformAd(creativeId: string, reason: AdComplaintReasonName, comment: string | null): Promise<ClubAdsDto>;
}

// Зеркало AdComplaintLimits.CommentMax: пределы кодогенерация в TS не переносит.
const COMMENT_MAX = 500;
const REASONS: readonly AdComplaintReasonName[] = Object.values(AdComplaintReasonNames);

const REASON_KEY: Record<AdComplaintReasonName, MessageKey> = {
  [AdComplaintReasonNames.BannedGoods]: 'op.ads.report.reason.banned_goods',
  [AdComplaintReasonNames.Minors]: 'op.ads.report.reason.minors',
  [AdComplaintReasonNames.Misleading]: 'op.ads.report.reason.misleading',
  [AdComplaintReasonNames.OtherClub]: 'op.ads.report.reason.other_club',
  [AdComplaintReasonNames.Other]: 'op.ads.report.reason.other'
};

/**
 * «Пожаловаться» (спека рекламы, §8.4): клуб — распространитель, но снять рекламу сам не может.
 * Жалоба уходит платформе; «Отправлено» — только после ответа сервера.
 */
export function ReportAdModal({ ad, client, onReported, onClose }: {
  ad: ClubAdDto;
  client: ReportAdClient;
  onReported: (next: ClubAdsDto) => void;
  onClose: () => void;
}) {
  const { t } = useI18n();
  const [reason, setReason] = useState<AdComplaintReasonName | null>(null);
  const [comment, setComment] = useState('');
  const [sending, setSending] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const tooLong = comment.trim().length > COMMENT_MAX;

  const send = async () => {
    if (reason === null) return;
    setSending(true);
    setError(null);
    try {
      onReported(await client.reportPlatformAd(ad.creativeId, reason, comment.trim() || null));
    } catch (failure) {
      setError(projectOperatorError(failure, t).detail);
      setSending(false);
    }
  };

  return (
    <PanelModal title={t('op.ads.report.title')} subtitle={t('op.ads.report.subtitle', { advertiser: ad.advertiser })} onClose={onClose} closeDisabled={sending}>
      <fieldset className="network-ad-report-reasons" disabled={sending}>
        <legend>{t('op.ads.report.reason')}</legend>
        {REASONS.map((value) => (
          <label key={value} className="network-ad-report-reason">
            <input type="radio" name="ad-report-reason" value={value} checked={reason === value} onChange={() => setReason(value)} />
            <span>{t(REASON_KEY[value])}</span>
          </label>
        ))}
      </fieldset>
      <label className="ui-field">
        <span>{t('op.ads.report.comment')}</span>
        <textarea
          rows={3}
          value={comment}
          disabled={sending}
          aria-invalid={tooLong || undefined}
          onChange={(event) => setComment(event.currentTarget.value)}
        />
      </label>
      {tooLong ? <p className="ui-inline-error" role="alert">{t('op.ads.report.commentTooLong', { max: COMMENT_MAX })}</p> : null}
      {error ? <p className="ui-inline-error" role="alert">{error}</p> : null}
      <div className="mgmt-form-actions">
        <button type="button" className="ui-btn" disabled={sending} onClick={onClose}>{t('common.cancel')}</button>
        <button type="button" className="ui-btn ui-btn--primary" disabled={sending || reason === null || tooLong} onClick={() => void send()}>
          {t(sending ? 'op.ads.report.sending' : 'op.ads.report.send')}
        </button>
      </div>
    </PanelModal>
  );
}
