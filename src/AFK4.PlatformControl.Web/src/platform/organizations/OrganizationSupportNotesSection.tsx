import { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Textarea } from '@/components/ui/textarea';
import { LoadingCards, ErrorState, EmptyState } from '@/components/ui/states';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { SupportNotesApi } from '@/api/platformClients/supportNotes';
import type { OrganizationSupportNote } from '@/api/types';

type Client = Pick<SupportNotesApi, 'listSupportNotes' | 'createSupportNote' | 'updateSupportNote'>;

export function OrganizationSupportNotesSection({ client, organizationId }: { client: Client; organizationId: string }) {
  const { t, formatDate } = useI18n();
  const { toast } = useToast();
  const [tick, setTick] = useState(0);
  const [notes, setNotes] = useState<OrganizationSupportNote[] | null>(null);
  const [error, setError] = useState(false);
  const [draft, setDraft] = useState('');
  const [creating, setCreating] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editingBody, setEditingBody] = useState('');
  const [savingEdit, setSavingEdit] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setNotes(null); setError(false);
    client.listSupportNotes(organizationId)
      .then(rows => { if (!cancelled) setNotes(rows); })
      .catch(() => { if (!cancelled) setError(true); });
    return () => { cancelled = true; };
  }, [client, organizationId, tick]);

  async function create() {
    if (draft.trim().length === 0) return;
    setCreating(true);
    try {
      await client.createSupportNote(organizationId, draft.trim());
      setDraft('');
      toast({ title: t('platform.organization.notes.created'), variant: 'success' });
      setTick(n => n + 1);
    } catch {
      toast({ title: t('platform.organization.action.error'), variant: 'error' });
    } finally {
      setCreating(false);
    }
  }

  function startEdit(n: OrganizationSupportNote) {
    setEditingId(n.organizationSupportNoteId);
    setEditingBody(n.body);
  }

  async function saveEdit() {
    if (editingId === null || editingBody.trim().length === 0) return;
    setSavingEdit(true);
    try {
      await client.updateSupportNote(organizationId, editingId, editingBody.trim());
      setEditingId(null); setEditingBody('');
      toast({ title: t('platform.organization.notes.updated'), variant: 'success' });
      setTick(n => n + 1);
    } catch {
      toast({ title: t('platform.organization.action.error'), variant: 'error' });
    } finally {
      setSavingEdit(false);
    }
  }

  return (
    <Card>
      <CardHeader><CardTitle>{t('platform.organization.section.notes')}</CardTitle></CardHeader>
      <CardContent className="flex flex-col gap-4 text-sm">
        <div className="flex flex-col gap-2">
          <label className="block">
            <span className="mb-1 block text-muted-foreground">{t('platform.organization.notes.newNote')}</span>
            <Textarea aria-label={t('platform.organization.notes.newNote')} rows={3} maxLength={4000} value={draft} onChange={e => setDraft(e.target.value)} />
          </label>
          <p className="text-xs text-muted-foreground">{t('platform.organization.notes.hint')}</p>
          <div>
            <Button onClick={() => void create()} disabled={creating || draft.trim().length === 0}>{t('platform.organization.notes.add')}</Button>
          </div>
        </div>

        {error ? (
          <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={() => setTick(n => n + 1)} />
        ) : notes === null ? (
          <LoadingCards count={1} />
        ) : notes.length === 0 ? (
          <EmptyState message={t('platform.organization.notes.empty')} />
        ) : (
          <ul className="flex flex-col gap-3">
            {notes.map(n => (
              <li key={n.organizationSupportNoteId} className="rounded-md border border-border p-3">
                <div className="mb-1 flex items-center justify-between text-xs text-muted-foreground">
                  <span>{n.authorDisplayName.length === 0 ? n.authorPlatformAdminId : n.authorDisplayName}</span>
                  <span className="tabular-nums">{formatDate(n.createdAtUtc)}</span>
                </div>
                {editingId === n.organizationSupportNoteId ? (
                  <div className="flex flex-col gap-2">
                    <Textarea aria-label={t('platform.organization.notes.editNote')} rows={4} maxLength={4000} value={editingBody} onChange={e => setEditingBody(e.target.value)} />
                    <div className="flex gap-2">
                      <Button variant="outline" size="sm" disabled={savingEdit} onClick={() => setEditingId(null)}>{t('platform.organization.notes.cancel')}</Button>
                      <Button size="sm" disabled={savingEdit || editingBody.trim().length === 0} onClick={() => void saveEdit()}>{t('platform.organization.notes.save')}</Button>
                    </div>
                  </div>
                ) : (
                  <div className="flex flex-col gap-2">
                    <p className="whitespace-pre-wrap">{n.body}</p>
                    <div>
                      <Button variant="ghost" size="sm" onClick={() => startEdit(n)}>{t('platform.organization.notes.edit')}</Button>
                    </div>
                  </div>
                )}
              </li>
            ))}
          </ul>
        )}
      </CardContent>
    </Card>
  );
}
