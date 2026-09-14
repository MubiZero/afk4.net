import { useCallback, useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { Select } from '@/components/ui/select';
import { useToast } from '@/components/ui/toast';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { useI18n } from '@/i18n/I18nProvider';
import { describeApiError } from '@/api/describeApiError';
import type { SupportAccessApi } from '@/api/platformClients/supportAccess';
import type { SupportAccessGrantListItem } from '@/api/types';

type Client = Pick<SupportAccessApi, 'issueGrant' | 'listGrants' | 'revokeGrant'>;

const REASON_MIN_LENGTH = 10;
const LIFETIME_OPTIONS = [15, 30] as const;
const DEFAULT_LIFETIME_MINUTES = 30;

// window.open — не тестируемый в bun:test побочный эффект, поэтому вынесен в проп с дефолтом:
// тест подменяет его и проверяет вызов без реального окна.
function defaultOpenUrl(url: string): void {
  window.open(url, '_blank', 'noopener');
}

// Список загружается отдельным состоянием, а не «пустым массивом по умолчанию»: сбой загрузки и
// отсутствие доступов — разные ответы на вопрос «кто сейчас внутри», и показать первое как второе
// значит сказать «никого нет», ничего не зная.
type GrantsState =
  | { status: 'loading' }
  | { status: 'ready'; grants: SupportAccessGrantListItem[] }
  | { status: 'failed' };

export function SupportAccessSection({ client, organizationId, openUrl = defaultOpenUrl }: {
  client: Client;
  organizationId: string;
  openUrl?: (url: string) => void;
}) {
  const { t, formatDate } = useI18n();
  const { toast } = useToast();
  const [reason, setReason] = useState('');
  const [lifetimeMinutes, setLifetimeMinutes] = useState(DEFAULT_LIFETIME_MINUTES);
  const [issuing, setIssuing] = useState(false);
  const [grants, setGrants] = useState<GrantsState>({ status: 'loading' });
  const [revokeTarget, setRevokeTarget] = useState<SupportAccessGrantListItem | null>(null);
  const [revoking, setRevoking] = useState(false);

  const trimmedReason = reason.trim();
  const reasonTooShort = trimmedReason.length < REASON_MIN_LENGTH;

  const loadGrants = useCallback(async () => {
    try {
      setGrants({ status: 'ready', grants: await client.listGrants(organizationId) });
    } catch {
      setGrants({ status: 'failed' });
    }
  }, [client, organizationId]);

  useEffect(() => { void loadGrants(); }, [loadGrants]);

  async function issue() {
    if (reasonTooShort) return;
    setIssuing(true);
    try {
      const issue = await client.issueGrant(organizationId, trimmedReason, lifetimeMinutes);
      // Билет ни в лог, ни в toast не попадает — только в готовую ссылку, которую собрал сервер.
      openUrl(issue.adminUrl);
      setReason('');
      toast({ title: t('platform.supportAccess.issued'), variant: 'success' });
      await loadGrants();
    } catch (cause) {
      toast({ title: describeApiError(cause, t, { 400: 'platform.supportAccess.error.validation' }), variant: 'error' });
    } finally {
      setIssuing(false);
    }
  }

  async function revoke(grant: SupportAccessGrantListItem) {
    setRevoking(true);
    try {
      await client.revokeGrant(grant.grantId);
      toast({ title: t('platform.supportAccess.revoked'), variant: 'success' });
      setRevokeTarget(null);
      await loadGrants();
    } catch (cause) {
      // 404 здесь означает «чужой доступ, и прав оборвать его нет» — сервер намеренно отвечает так
      // же, как на несуществующий, и обещать иное нельзя.
      toast({ title: describeApiError(cause, t, { 404: 'platform.supportAccess.error.notYours' }), variant: 'error' });
    } finally {
      setRevoking(false);
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

        <section aria-label={t('platform.supportAccess.active.title')}>
          <h3>{t('platform.supportAccess.active.title')}</h3>
          {grants.status === 'loading' && <p className="mgmt-drawer-hint">{t('state.loading')}</p>}
          {grants.status === 'failed' && (
            <p role="alert">
              {t('platform.supportAccess.active.failed')}{' '}
              <Button variant="ghost" onClick={() => void loadGrants()}>{t('state.retry')}</Button>
            </p>
          )}
          {grants.status === 'ready' && grants.grants.length === 0 && (
            <p className="mgmt-drawer-hint">{t('platform.supportAccess.active.empty')}</p>
          )}
          {grants.status === 'ready' && grants.grants.length > 0 && (
            <ul className="ui-list">
              {grants.grants.map(grant => (
                <li key={grant.grantId}>
                  <p>{grant.platformAdminDisplayName}</p>
                  <p>{grant.reason}</p>
                  <p className="mgmt-drawer-hint">
                    {t('platform.supportAccess.active.until', { time: formatDate(grant.expiresAtUtc) })}
                    {' · '}
                    {grant.enteredAtUtc === null
                      ? t('platform.supportAccess.active.notEntered')
                      : t('platform.supportAccess.active.entered', { time: formatDate(grant.enteredAtUtc) })}
                  </p>
                  <Button variant="destructive" onClick={() => setRevokeTarget(grant)}>
                    {t('platform.supportAccess.active.revoke')}
                  </Button>
                </li>
              ))}
            </ul>
          )}
        </section>
      </CardContent>

      <ConfirmDialog
        open={revokeTarget !== null}
        title={t('platform.supportAccess.revokeConfirm.title')}
        description={t('platform.supportAccess.revokeConfirm.body')}
        confirmLabel={t('platform.supportAccess.revokeConfirm.confirm')}
        cancelLabel={t('common.cancel')}
        destructive
        pending={revoking}
        onConfirm={() => { if (revokeTarget !== null) void revoke(revokeTarget); }}
        onOpenChange={open => { if (!open) setRevokeTarget(null); }}
      />
    </Card>
  );
}
