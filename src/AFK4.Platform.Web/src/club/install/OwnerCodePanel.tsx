import { useState } from 'react';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { LoadingCards, ErrorState } from '@/components/ui/states';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { OwnerCodeApi } from '@/api/clients/ownerCode';
import type { OwnerCodeSummary, OwnerCodeIssued } from '@/api/types';
import { useOwnerCode } from './useOwnerCode';
import { toOwnerCodeView } from './installModel';

type Client = Pick<OwnerCodeApi, 'getOwnerCode' | 'generateOwnerCode' | 'rotateOwnerCode'>;

export function OwnerCodePanel({ client, canManage }: { client: Client; canManage: boolean }) {
  const { t, formatDate } = useI18n();
  const { toast } = useToast();
  const state = useOwnerCode(client, canManage);
  const [issued, setIssued] = useState<OwnerCodeIssued | null>(null);
  const [override, setOverride] = useState<OwnerCodeSummary | null>(null);
  const [reason, setReason] = useState('');
  const [busy, setBusy] = useState(false);

  async function run(kind: 'generate' | 'rotate') {
    setBusy(true);
    try {
      const next = kind === 'generate'
        ? await client.generateOwnerCode()
        : await client.rotateOwnerCode(reason.trim().length > 0 ? reason.trim() : 'dashboard rotation');
      setIssued(next);
      setOverride({ codeSuffix: next.codeSuffix, expiresAtUtc: next.expiresAtUtc, lastUsedAtUtc: null, failedAttemptCount: 0 });
      toast({ title: kind === 'generate' ? t('install.ownerCode.generated') : t('install.ownerCode.rotated'), variant: 'success' });
    } catch {
      toast({ title: t('install.ownerCode.error'), variant: 'error' });
    } finally {
      setBusy(false);
    }
  }

  return (
    <Card>
      <CardHeader><CardTitle>{t('install.ownerCode.title')}</CardTitle></CardHeader>
      <CardContent>
        {!canManage ? (
          <p className="text-sm text-muted-foreground">{t('install.ownerCode.noAccess')}</p>
        ) : state.status === 'loading' ? (
          <LoadingCards count={1} />
        ) : state.status === 'error' ? (
          <ErrorState message={t('state.error')} retryLabel={t('state.retry')} onRetry={state.retry} />
        ) : (
          <OwnerCodeBody
            view={toOwnerCodeView(override ?? state.summary, issued)}
            reason={reason} setReason={setReason} busy={busy}
            onGenerate={() => void run('generate')} onRotate={() => void run('rotate')}
            formatDate={formatDate}
          />
        )}
      </CardContent>
    </Card>
  );
}

function OwnerCodeBody({ view, reason, setReason, busy, onGenerate, onRotate, formatDate }: {
  view: ReturnType<typeof toOwnerCodeView>;
  reason: string;
  setReason: (v: string) => void;
  busy: boolean;
  onGenerate: () => void;
  onRotate: () => void;
  formatDate: (iso: string) => string;
}) {
  const { t } = useI18n();
  return (
    <div className="flex flex-col gap-4">
      <div className="font-mono text-2xl font-semibold tracking-widest" aria-label={t('install.ownerCode.title')}>
        {view.hasCode ? view.code : t('install.ownerCode.none')}
      </div>
      <dl className="grid grid-cols-1 gap-2 text-sm sm:grid-cols-3">
        <div><dt className="text-xs text-muted-foreground">{t('install.ownerCode.validUntil')}</dt>
          <dd>{view.expiresAtUtc === null ? '—' : formatDate(view.expiresAtUtc)}</dd></div>
        <div><dt className="text-xs text-muted-foreground">{t('install.ownerCode.lastUsed')}</dt>
          <dd>{view.lastUsedAtUtc === null ? '—' : formatDate(view.lastUsedAtUtc)}</dd></div>
        <div><dt className="text-xs text-muted-foreground">{t('install.ownerCode.failed')}</dt>
          <dd className="tabular-nums">{view.failedAttemptCount}</dd></div>
      </dl>
      <div className="flex flex-wrap items-end gap-3">
        <Button disabled={busy} onClick={onGenerate}>{t('install.ownerCode.generate')}</Button>
        <label className="flex flex-col gap-1 text-xs text-muted-foreground">
          {t('install.ownerCode.reason')}
          <Input aria-label={t('install.ownerCode.reason')} value={reason} onChange={e => setReason(e.target.value)} disabled={busy} />
        </label>
        <Button variant="outline" disabled={busy || !view.hasCode} onClick={onRotate}>{t('install.ownerCode.rotate')}</Button>
      </div>
    </div>
  );
}
