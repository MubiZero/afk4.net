import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { Select } from '@/components/ui/select';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import { describeApiError } from '@/api/describeApiError';
import type { SupportAccessApi } from '@/api/platformClients/supportAccess';

type Client = Pick<SupportAccessApi, 'issueGrant' | 'revokeGrant'>;

const REASON_MIN_LENGTH = 10;
const LIFETIME_OPTIONS = [15, 30] as const;
const DEFAULT_LIFETIME_MINUTES = 30;

// window.open — не тестируемый в bun:test побочный эффект, поэтому вынесен в проп с дефолтом:
// тест подменяет его и проверяет вызов без реального окна.
function defaultOpenUrl(url: string): void {
  window.open(url, '_blank', 'noopener');
}

export function SupportAccessSection({ client, organizationId, openUrl = defaultOpenUrl }: {
  client: Client;
  organizationId: string;
  openUrl?: (url: string) => void;
}) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [reason, setReason] = useState('');
  const [lifetimeMinutes, setLifetimeMinutes] = useState(DEFAULT_LIFETIME_MINUTES);
  const [issuing, setIssuing] = useState(false);

  const trimmedReason = reason.trim();
  const reasonTooShort = trimmedReason.length < REASON_MIN_LENGTH;

  async function issue() {
    if (reasonTooShort) return;
    setIssuing(true);
    try {
      const issue = await client.issueGrant(organizationId, trimmedReason, lifetimeMinutes);
      // Билет ни в лог, ни в toast не попадает — только в готовую ссылку, которую собрал сервер.
      openUrl(issue.adminUrl);
      setReason('');
      toast({ title: t('platform.supportAccess.issued'), variant: 'success' });
    } catch (cause) {
      toast({ title: describeApiError(cause, t, { 400: 'platform.supportAccess.error.validation' }), variant: 'error' });
    } finally {
      setIssuing(false);
    }
  }

  return (
    <Card>
      <CardHeader><CardTitle>{t('platform.supportAccess.title')}</CardTitle></CardHeader>
      <CardContent>
        <p role="alert">{t('platform.supportAccess.warning')}</p>

        <label className="ui-field">
          <span>{t('platform.supportAccess.reasonLabel')}</span>
          <Textarea
            aria-label={t('platform.supportAccess.reasonLabel')}
            rows={2}
            maxLength={500}
            value={reason}
            onChange={e => setReason(e.target.value)}
          />
          <span className="mgmt-drawer-hint">{t('platform.supportAccess.reasonHint')}</span>
        </label>

        <label className="ui-field">
          <span>{t('platform.supportAccess.lifetimeLabel')}</span>
          <Select
            aria-label={t('platform.supportAccess.lifetimeLabel')}
            value={String(lifetimeMinutes)}
            onChange={e => setLifetimeMinutes(Number(e.target.value))}
          >
            {LIFETIME_OPTIONS.map(minutes => (
              <option key={minutes} value={minutes}>{t('platform.supportAccess.lifetimeOption', { minutes })}</option>
            ))}
          </Select>
        </label>

        <div>
          <Button disabled={issuing || reasonTooShort} onClick={() => void issue()}>
            {t('platform.supportAccess.submit')}
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
