import { useCallback, useEffect, useState, type FormEvent } from 'react';
import { PlatformApiClient, PlatformApiError } from '../api/platformApi';
import type { TenantSupportNote } from '../api/types';
import { EmptyState, ErrorBanner, Field, Loading, formatDate } from './ui';

export interface SupportNotesSectionProps {
  client: PlatformApiClient;
  organizationId: string;
}

export function SupportNotesSection({ client, organizationId }: SupportNotesSectionProps) {
  const [notes, setNotes] = useState<TenantSupportNote[] | null>(null);
  const [isLoading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [draft, setDraft] = useState('');
  const [isCreating, setCreating] = useState(false);
  const [editingId, setEditingId] = useState<string | null>(null);
  const [editingBody, setEditingBody] = useState('');

  const reload = useCallback(async () => {
    setLoading(true);
    setError(null);
    try {
      const data = await client.listSupportNotes(organizationId);
      setNotes(data);
    } catch (cause) {
      setError(toMessage(cause, 'Failed to load support notes.'));
    } finally {
      setLoading(false);
    }
  }, [client, organizationId]);

  useEffect(() => {
    void reload();
  }, [reload]);

  async function handleCreate(event: FormEvent<HTMLFormElement>) {
    event.preventDefault();
    setCreating(true);
    setError(null);
    try {
      const created = await client.createSupportNote(organizationId, draft);
      setNotes(current => [created, ...(current ?? [])]);
      setDraft('');
    } catch (cause) {
      setError(toMessage(cause, 'Failed to save support note.'));
    } finally {
      setCreating(false);
    }
  }

  function startEdit(note: TenantSupportNote) {
    setEditingId(note.tenantSupportNoteId);
    setEditingBody(note.body);
  }

  async function saveEdit() {
    if (editingId === null) {
      return;
    }
    setError(null);
    try {
      const updated = await client.updateSupportNote(organizationId, editingId, editingBody);
      setNotes(current => (current ?? []).map(note => (note.tenantSupportNoteId === updated.tenantSupportNoteId ? updated : note)));
      setEditingId(null);
      setEditingBody('');
    } catch (cause) {
      setError(toMessage(cause, 'Failed to update support note.'));
    }
  }

  return (
    <section className="section">
      <header className="section-header">
        <h2>Support notes</h2>
        <button type="button" onClick={() => void reload()} disabled={isLoading}>Refresh</button>
      </header>
      <ErrorBanner message={error} onDismiss={() => setError(null)} />
      <form className="form" onSubmit={handleCreate}>
        <Field label="New note" htmlFor="note-body" hint="Up to 4000 characters. Visible to platform admins only.">
          <textarea id="note-body" value={draft} onChange={e => setDraft(e.target.value)} rows={3} required />
        </Field>
        <div className="actions">
          <button type="submit" className="primary" disabled={isCreating || draft.trim().length === 0}>
            {isCreating ? 'Saving…' : 'Add note'}
          </button>
        </div>
      </form>
      {isLoading && notes === null && <Loading label="Loading notes…" />}
      {notes !== null && notes.length === 0 && (
        <EmptyState>No support notes yet.</EmptyState>
      )}
      {notes !== null && notes.length > 0 && (
        <ul className="cards">
          {notes.map(note => (
            <li key={note.tenantSupportNoteId} className="card">
              <header className="card-header">
                <span>{note.authorDisplayName.length === 0 ? note.authorPlatformAdminId : note.authorDisplayName}</span>
                <span className="muted">{formatDate(note.createdAtUtc)}</span>
              </header>
              {editingId === note.tenantSupportNoteId ? (
                <>
                  <textarea value={editingBody} onChange={e => setEditingBody(e.target.value)} rows={4} />
                  <div className="actions">
                    <button type="button" onClick={() => setEditingId(null)}>Cancel</button>
                    <button type="button" className="primary" onClick={() => void saveEdit()}>Save</button>
                  </div>
                </>
              ) : (
                <>
                  <p className="prewrap">{note.body}</p>
                  <div className="actions">
                    <button type="button" className="link" onClick={() => startEdit(note)}>Edit</button>
                  </div>
                </>
              )}
            </li>
          ))}
        </ul>
      )}
    </section>
  );
}

function toMessage(cause: unknown, fallback: string): string {
  if (cause instanceof PlatformApiError) {
    return cause.message;
  }
  if (cause instanceof Error) {
    return cause.message;
  }
  return fallback;
}
