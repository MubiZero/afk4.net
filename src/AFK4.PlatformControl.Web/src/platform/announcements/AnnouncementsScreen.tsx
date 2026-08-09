import { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Table, TableHeader, TableRow, TableHead, TableBody, TableCell } from '@/components/ui/table';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { AnnouncementsApi } from '@/api/platformClients/announcements';
import type { PlatformAnnouncement } from '@/api/types';
import {
  AUDIENCE_KINDS,
  SEVERITIES,
  audienceIsLocked,
  audienceLabelKey,
  describeAnnouncementError,
  fromLocalInput,
  severityLabelKey,
  statusLabelKey,
  toLocalInput
} from './announcementsModel';

type Client = Pick<AnnouncementsApi,
  'listAnnouncements' | 'createAnnouncement' | 'updateAnnouncement' | 'publishAnnouncement' | 'withdrawAnnouncement'>;

interface Draft {
  announcementId: string | null;
  title: string;
  body: string;
  severity: string;
  showFrom: string;
  showUntil: string;
  audienceKind: string;
  planCodes: string;
}

function emptyDraft(): Draft {
  const now = new Date();
  const week = new Date(now.getTime() + 7 * 24 * 60 * 60 * 1000);
  return {
    announcementId: null,
    title: '',
    body: '',
    severity: 'info',
    showFrom: toLocalInput(now.toISOString()),
    showUntil: toLocalInput(week.toISOString()),
    audienceKind: 'all',
    planCodes: ''
  };
}

function draftFrom(announcement: PlatformAnnouncement): Draft {
  return {
    announcementId: announcement.announcementId,
    title: announcement.title,
    body: announcement.body,
    severity: announcement.severity,
    showFrom: toLocalInput(announcement.showFromUtc),
    showUntil: toLocalInput(announcement.showUntilUtc),
    audienceKind: announcement.audienceKind,
    planCodes: announcement.audiencePlanCodes.join(', ')
  };
}

export function AnnouncementsScreen({ client }: { client: Client }) {
  const { t, formatDate } = useI18n();
  const { toast } = useToast();
  const [announcements, setAnnouncements] = useState<PlatformAnnouncement[] | null>(null);
  const [failed, setFailed] = useState(false);
  const [tick, setTick] = useState(0);
  const [draft, setDraft] = useState<Draft | null>(null);
  const [publishTarget, setPublishTarget] = useState<PlatformAnnouncement | null>(null);
  const [withdrawTarget, setWithdrawTarget] = useState<PlatformAnnouncement | null>(null);
  const [pending, setPending] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setFailed(false);
    client.listAnnouncements()
      .then(loaded => { if (!cancelled) setAnnouncements(Array.isArray(loaded) ? loaded : []); })
      .catch(() => { if (!cancelled) setFailed(true); });
    return () => { cancelled = true; };
  }, [client, tick]);

  function reload() {
    setDraft(null);
    setPublishTarget(null);
    setWithdrawTarget(null);
    setTick(value => value + 1);
  }

  const editing = draft?.announcementId === null || draft === null
    ? null
    : announcements?.find(item => item.announcementId === draft.announcementId) ?? null;
  const locked = audienceIsLocked(editing);

  async function save() {
    if (draft === null || pending) return;
    const showFromUtc = fromLocalInput(draft.showFrom);
    const showUntilUtc = fromLocalInput(draft.showUntil);
    if (showFromUtc === null || showUntilUtc === null) {
      toast({ title: t('platform.announcements.error.invalid'), variant: 'error' });
      return;
    }

    setPending(true);
    try {
      const request = {
        title: draft.title.trim(),
        body: draft.body.trim(),
        severity: draft.severity,
        showFromUtc,
        showUntilUtc,
        audienceKind: draft.audienceKind,
        audiencePlanCodes: draft.audienceKind === 'plans'
          ? draft.planCodes.split(',').map(code => code.trim()).filter(code => code.length > 0)
          : [],
        audienceOrganizationIds: []
      };
      if (draft.announcementId === null) {
        await client.createAnnouncement(request);
      } else {
        await client.updateAnnouncement(draft.announcementId, request);
      }
      toast({ title: t('platform.announcements.saved'), variant: 'success' });
      reload();
    } catch (cause) {
      toast({ title: describeAnnouncementError(cause, t), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  async function run(action: () => Promise<unknown>, successKey: 'platform.announcements.published' | 'platform.announcements.withdrawn') {
    if (pending) return;
    setPending(true);
    try {
      await action();
      toast({ title: t(successKey), variant: 'success' });
      reload();
    } catch (cause) {
      toast({ title: describeAnnouncementError(cause, t), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  if (failed) {
    return <ErrorState message={t('platform.announcements.error.load')} retryLabel={t('state.retry')} onRetry={reload} />;
  }
  if (announcements === null) return <LoadingCards count={2} />;

  const canSave = draft !== null && draft.title.trim().length > 0 && draft.body.trim().length > 0;

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('platform.announcements.title')}</CardTitle>
        <Button onClick={() => setDraft(emptyDraft())}>{t('platform.announcements.create')}</Button>
      </CardHeader>
      <CardContent>
        <p className="mgmt-drawer-hint">{t('platform.announcements.description')}</p>

        {announcements.length === 0 ? (
          <EmptyState message={t('platform.announcements.empty')} />
        ) : (
          <Table>
            <TableHeader>
              <TableRow>
                <TableHead>{t('platform.announcements.column.title')}</TableHead>
                <TableHead>{t('platform.announcements.column.severity')}</TableHead>
                <TableHead>{t('platform.announcements.column.window')}</TableHead>
                <TableHead>{t('platform.announcements.column.audience')}</TableHead>
                <TableHead>{t('platform.announcements.column.status')}</TableHead>
                <TableHead>{t('platform.announcements.column.reach')}</TableHead>
                <TableHead>{t('platform.announcements.column.actions')}</TableHead>
              </TableRow>
            </TableHeader>
            <TableBody>
              {announcements.map(announcement => (
                <TableRow key={announcement.announcementId}>
                  <TableCell>{announcement.title}</TableCell>
                  <TableCell>
                    <Badge variant={announcement.severity === 'info' ? 'outline' : 'warning'}>
                      {t(severityLabelKey(announcement.severity))}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    {formatDate(announcement.showFromUtc)} — {formatDate(announcement.showUntilUtc)}
                  </TableCell>
                  <TableCell>{t(audienceLabelKey(announcement.audienceKind))}</TableCell>
                  <TableCell>
                    <Badge variant={announcement.status === 'published' ? 'success' : 'outline'}>
                      {t(statusLabelKey(announcement.status))}
                    </Badge>
                  </TableCell>
                  <TableCell>
                    <span className="pc-num">{t('platform.announcements.readers', { count: announcement.readCount })}</span>
                    {announcement.emailDispatched
                      ? <Badge variant="outline">{t('platform.announcements.emailSent')}</Badge>
                      : null}
                  </TableCell>
                  <TableCell>
                    <span className="pc-cell-actions">
                      {announcement.status === 'withdrawn' ? null : (
                        <Button size="sm" variant="outline" disabled={pending} onClick={() => setDraft(draftFrom(announcement))}>
                          {t('platform.announcements.edit')}
                        </Button>
                      )}
                      {announcement.status === 'draft' ? (
                        <Button size="sm" disabled={pending} onClick={() => setPublishTarget(announcement)}>
                          {t('platform.announcements.publish')}
                        </Button>
                      ) : null}
                      {announcement.status === 'published' ? (
                        <Button size="sm" variant="outline" disabled={pending} onClick={() => setWithdrawTarget(announcement)}>
                          {t('platform.announcements.withdraw')}
                        </Button>
                      ) : null}
                    </span>
                  </TableCell>
                </TableRow>
              ))}
            </TableBody>
          </Table>
        )}

        {draft === null ? null : (
          <div className="mgmt-form">
            <label>
              {t('platform.announcements.field.title')}
              <Input value={draft.title} onChange={event => setDraft({ ...draft, title: event.target.value })} />
            </label>
            <label>
              {t('platform.announcements.field.body')}
              <textarea
                className="ui-input"
                rows={5}
                value={draft.body}
                onChange={event => setDraft({ ...draft, body: event.target.value })}
              />
            </label>
            {/* Тело пишет человек и хранится одной строкой: машинного перевода на en/tg нет
                намеренно — иначе туда скопировали бы русский под видом перевода. */}
            <p className="mgmt-drawer-hint">{t('platform.announcements.field.bodyHint')}</p>

            <label>
              {t('platform.announcements.field.severity')}
              <select
                className="ui-input"
                value={draft.severity}
                disabled={locked}
                onChange={event => setDraft({ ...draft, severity: event.target.value })}
              >
                {SEVERITIES.map(severity => (
                  <option key={severity} value={severity}>{t(severityLabelKey(severity))}</option>
                ))}
              </select>
            </label>

            <label>
              {t('platform.announcements.field.showFrom')}
              <Input
                type="datetime-local"
                value={draft.showFrom}
                onChange={event => setDraft({ ...draft, showFrom: event.target.value })}
              />
            </label>
            <label>
              {t('platform.announcements.field.showUntil')}
              <Input
                type="datetime-local"
                value={draft.showUntil}
                onChange={event => setDraft({ ...draft, showUntil: event.target.value })}
              />
            </label>

            <label>
              {t('platform.announcements.field.audience')}
              <select
                className="ui-input"
                value={draft.audienceKind}
                disabled={locked}
                onChange={event => setDraft({ ...draft, audienceKind: event.target.value })}
              >
                {AUDIENCE_KINDS.map(kind => (
                  <option key={kind} value={kind}>{t(audienceLabelKey(kind))}</option>
                ))}
              </select>
            </label>
            {draft.audienceKind === 'plans' ? (
              <label>
                {t('platform.announcements.field.planCodes')}
                <Input
                  value={draft.planCodes}
                  disabled={locked}
                  onChange={event => setDraft({ ...draft, planCodes: event.target.value })}
                />
              </label>
            ) : null}

            {locked ? <p className="mgmt-drawer-hint">{t('platform.announcements.audienceLockedHint')}</p> : null}

            <Button disabled={pending || !canSave} onClick={save}>{t('platform.announcements.save')}</Button>
            <Button variant="outline" disabled={pending} onClick={() => setDraft(null)}>{t('common.cancel')}</Button>
          </div>
        )}

        <ConfirmDialog
          open={publishTarget !== null}
          title={t('platform.announcements.publishConfirm.title')}
          description={publishTarget === null
            ? undefined
            : t(publishTarget.severity === 'info'
              ? 'platform.announcements.publishConfirm.bodyNoEmail'
              : 'platform.announcements.publishConfirm.bodyWithEmail')}
          confirmLabel={t('platform.announcements.publish')}
          cancelLabel={t('common.cancel')}
          pending={pending}
          onConfirm={() => {
            if (publishTarget !== null) void run(() => client.publishAnnouncement(publishTarget.announcementId), 'platform.announcements.published');
          }}
          onOpenChange={open => { if (!open) setPublishTarget(null); }}
        />

        <ConfirmDialog
          open={withdrawTarget !== null}
          title={t('platform.announcements.withdrawConfirm.title')}
          description={t('platform.announcements.withdrawConfirm.body')}
          confirmLabel={t('platform.announcements.withdraw')}
          cancelLabel={t('common.cancel')}
          destructive
          pending={pending}
          onConfirm={() => {
            if (withdrawTarget !== null) void run(() => client.withdrawAnnouncement(withdrawTarget.announcementId), 'platform.announcements.withdrawn');
          }}
          onOpenChange={open => { if (!open) setWithdrawTarget(null); }}
        />
      </CardContent>
    </Card>
  );
}
