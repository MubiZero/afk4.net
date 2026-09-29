import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { RowActions, type RowAction } from '@afk4/ui/react';
import { Card, CardContent, CardHeader, CardTitle, CardDescription } from '@/components/ui/card';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { ErrorState } from '@/components/ui/states';
import { Loading, SkeletonCard, SkeletonRows } from '@/components/ui/skeletons';
import { useToast } from '@/components/ui/toast';
import { describeApiError } from '@/api/describeApiError';
import { useAttemptKey } from '@/api/useAttemptKey';
import { useI18n } from '@/i18n/I18nProvider';
import { formatMoney } from '@afk4/money';
import type { InvoicesApi } from '@/api/platformClients/invoices';
import type { OrganizationsApi } from '@/api/platformClients/organizations';
import type { SubscriptionsApi } from '@/api/platformClients/subscriptions';
import type { SupportNotesApi } from '@/api/platformClients/supportNotes';
import type { DebtApi } from '@/api/platformClients/debt';
import type { DebtRow } from '@/api/types';
import { useDebt } from './useDebt';
import { debtTotals, dunningStageLabelKey, sortDebtRows } from './debtModel';
import { PaymentGraceDialog } from '@/platform/organizations/PaymentGraceDialog';

export interface DebtSectionClients {
  debt: Pick<DebtApi, 'listDebt'>;
  invoices: Pick<InvoicesApi, 'markInvoicePaid'>;
  organizations: Pick<OrganizationsApi, 'updateStatus'>;
  subscriptions: Pick<SubscriptionsApi, 'updateSubscription'>;
  supportNotes: Pick<SupportNotesApi, 'createSupportNote'>;
}

type Action = { kind: 'markPaid' | 'toggleStatus' | 'note'; row: DebtRow };

// Четыре действия строки проверяются бэкендом четырьмя РАЗНЫМИ правами (см. platformAccess.ts):
// «Отметить оплаченным» — platform.billing.invoices.manage, «Отсрочка» —
// platform.billing.subscriptions.manage, «Приостановить»/«Активировать» —
// platform.organizations.status.update, «Заметка» — platform.organizations.support_notes.manage.
// Один общий булев здесь показывал бы кнопку админу, у которого есть только одно из четырёх
// прав, и он получал бы 403 на остальных трёх — тот же паттерн разведения флагов, что уже
// применён в ClientPassport.tsx.
export interface DebtSectionAccess {
  canMarkPaid: boolean;
  canGrantGrace: boolean;
  canToggleStatus: boolean;
  canAddNote: boolean;
}

// Раздел «Задолженность» — первый блок экрана «Деньги», выше очереди неоплаченных счетов.
// Очередь отвечает на «какие счета не оплачены», этот раздел — на более крупный вопрос
// «какие клубы вообще требуют решения», включая тех, кто уже расплатился, но остался
// отключён: приостановку никто не снимает автоматически.
// Строка должника — одна кнопка следующего шага и «⋯» для остального. Раньше в строке стояли
// четыре кнопки, из них «Приостановить» красным посередине, вплотную к «Отсрочке».
// Следующий шаг: у должника — отметить оплату, у расплатившегося, но отключённого — вернуть его в
// работу (ради этого он в списке и стоит).
function DebtRowActions({ row, access, onAct, onGrace }: {
  row: DebtRow;
  access: DebtSectionAccess;
  onAct: (action: Action) => void;
  onGrace: (row: DebtRow) => void;
}) {
  const { t } = useI18n();
  const canPay = access.canMarkPaid && row.oldestOverdueInvoiceId !== null;
  const reactivateFirst = !canPay && access.canToggleStatus && row.organizationStatus !== 'active';
  const menu: RowAction[] = [
    ...(access.canGrantGrace && !row.settledButSuspended ? [{ id: 'grace', label: t('platform.debt.action.grace'), onSelect: () => onGrace(row) }] : []),
    ...(access.canAddNote ? [{ id: 'note', label: t('platform.debt.action.note'), onSelect: () => onAct({ kind: 'note', row }) }] : []),
    ...(access.canToggleStatus && !reactivateFirst ? [{
      id: 'status',
      label: row.organizationStatus === 'active' ? t('platform.organization.passport.action.suspend') : t('platform.organization.passport.action.activate'),
      danger: row.organizationStatus === 'active',
      onSelect: () => onAct({ kind: 'toggleStatus', row })
    }] : [])
  ];
  return (
    <span className="pc-cell-actions">
      {canPay ? (
        <Button variant="outline" size="sm" onClick={() => onAct({ kind: 'markPaid', row })}>{t('platform.billing.action.markPaid')}</Button>
      ) : reactivateFirst ? (
        <Button variant="outline" size="sm" onClick={() => onAct({ kind: 'toggleStatus', row })}>{t('platform.organization.passport.action.activate')}</Button>
      ) : null}
      <RowActions label={t('platform.row.more', { name: row.organizationName })} actions={menu} />
    </span>
  );
}

export function DebtSection({ client, access }: { client: DebtSectionClients; access: DebtSectionAccess }) {
  const { t, formatDate } = useI18n();
  const { toast } = useToast();
  const state = useDebt(client.debt);
  const [action, setAction] = useState<Action | null>(null);
  const [pending, setPending] = useState(false);
  const [graceRow, setGraceRow] = useState<DebtRow | null>(null);
  const attempt = useAttemptKey();

  async function confirm(reason: string) {
    if (action === null) return;
    setPending(true);
    try {
      if (action.kind === 'markPaid') {
        if (action.row.oldestOverdueInvoiceId === null) return;
        await client.invoices.markInvoicePaid(
          action.row.oldestOverdueInvoiceId,
          reason.length > 0 ? reason : null,
          attempt.forSubject({ invoiceId: action.row.oldestOverdueInvoiceId, reason }));
        attempt.done();
        toast({ title: t('platform.billing.markPaid.done'), variant: 'success' });
      } else if (action.kind === 'toggleStatus') {
        const nextStatus = action.row.organizationStatus === 'active' ? 'suspended' : 'active';
        await client.organizations.updateStatus(action.row.organizationId, nextStatus, reason);
        toast({ title: t('platform.organization.passport.statusUpdated'), variant: 'success' });
      } else {
        await client.supportNotes.createSupportNote(action.row.organizationId, reason);
        toast({ title: t('platform.debt.note.done'), variant: 'success' });
      }
      setAction(null);
      if (state.status === 'ready') state.retry();
    } catch (cause) {
      toast({ title: describeApiError(cause, t), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  if (state.status === 'loading') {
    const canActOnRows = access.canMarkPaid || access.canGrantGrace || access.canToggleStatus || access.canAddNote;
    return (
      <Loading>
        <SkeletonCard description>
          <SkeletonRows as="ul" rows={3} className="pc-queue" rowClassName="pc-queue-row" trailing={canActOnRows} />
        </SkeletonCard>
      </Loading>
    );
  }
  if (state.status === 'error') return <ErrorState message={state.message} retryLabel={state.canRetry ? t('state.retry') : undefined} onRetry={state.canRetry ? state.retry : undefined} />;

  const rows = sortDebtRows(state.data);
  const totals = debtTotals(rows);
  const canManageAny = access.canMarkPaid || access.canGrantGrace || access.canToggleStatus || access.canAddNote;

  const nextStatus = action?.kind === 'toggleStatus' ? (action.row.organizationStatus === 'active' ? 'suspended' : 'active') : null;

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('platform.debt.title')}</CardTitle>
        {rows.length > 0 ? (
          <div className="pc-debt-summary">
            <Badge variant="warning">{t('platform.debt.count', { count: rows.length })}</Badge>
            {totals.length > 0 ? (
              <span className="pc-debt-summary-amount ui-money">
                {totals.map(total => formatMoney(total.amountMinorUnits, total.currencyCode)).join(' · ')}
              </span>
            ) : null}
          </div>
        ) : null}
      </CardHeader>

      {/* Постоянная подпись раздела — под шапкой, а не внутри неё: в шапке она наследовала
          капитель заголовка. Строка totals пуста, когда в очереди только клубы, которые уже
          расплатились, но остались отключены, а «platform.debt.empty» подходит только пустой
          очереди целиком. */}
      <CardDescription>{rows.length === 0 ? t('platform.debt.empty') : t('platform.debt.subtitle')}</CardDescription>

      {rows.length > 0 ? (
        <CardContent>
          <ul className="pc-queue">
            {rows.map(row => (
              <li key={row.organizationId} className="pc-queue-row" data-testid="debt-row" data-organization={row.organizationSlug}>
                <span className="pc-queue-id">
                  <strong>{row.organizationName}</strong>
                  <span>
                    {row.settledButSuspended
                      ? t('platform.debt.settledButSuspended')
                      : row.graceUntilUtc !== null
                        ? t('platform.debt.grace.until', { date: formatDate(row.graceUntilUtc) })
                        : t('platform.debt.row.daysOverdue', { days: row.daysOverdue })}
                  </span>
                </span>
                <Badge
                  data-testid="debt-stage-badge"
                  variant={row.settledButSuspended ? 'outline' : row.graceUntilUtc !== null ? 'secondary' : 'destructive'}
                >
                  {t(dunningStageLabelKey(row.dunningStage))}
                </Badge>
                {/* Погашенный долг у отключённого клуба — сумма к оплате 0, показывать «0 с.» нечего. */}
                {row.outstandingMinorUnits > 0 ? (
                  <span className="pc-queue-amount ui-money">
                    {formatMoney(row.outstandingMinorUnits, row.currencyCode)}
                  </span>
                ) : null}
                {canManageAny ? <DebtRowActions row={row} access={access} onAct={setAction} onGrace={setGraceRow} /> : null}
              </li>
            ))}
          </ul>
        </CardContent>
      ) : null}

      <ConfirmDialog
        open={action !== null && action.kind === 'markPaid'}
        title={t('platform.billing.markPaid.title')}
        confirmLabel={t('platform.billing.markPaid.confirm')}
        cancelLabel={t('platform.billing.action.cancel')}
        reasonLabel={t('platform.billing.markPaid.reference')}
        pending={pending}
        onConfirm={reason => void confirm(reason)}
        onOpenChange={open => { if (!open) setAction(null); }}
      />

      <ConfirmDialog
        open={action !== null && action.kind === 'toggleStatus'}
        title={nextStatus === 'suspended' ? t('platform.organization.passport.suspendTitle') : t('platform.organization.passport.activateTitle')}
        confirmLabel={t('platform.organization.statusForm.confirm')}
        cancelLabel={t('platform.organization.statusForm.cancel')}
        reasonLabel={nextStatus === 'suspended' ? t('platform.organization.statusForm.reason') : undefined}
        destructive={nextStatus === 'suspended'}
        pending={pending}
        onConfirm={reason => void confirm(reason)}
        onOpenChange={open => { if (!open) setAction(null); }}
      />

      <ConfirmDialog
        open={action !== null && action.kind === 'note'}
        title={t('platform.debt.note.title')}
        confirmLabel={t('platform.debt.note.confirm')}
        cancelLabel={t('platform.billing.action.cancel')}
        reasonLabel={t('platform.debt.note.body')}
        pending={pending}
        onConfirm={reason => void confirm(reason)}
        onOpenChange={open => { if (!open) setAction(null); }}
      />

      {graceRow !== null ? (
        <PaymentGraceDialog
          client={client.subscriptions}
          organizationId={graceRow.organizationId}
          currentGraceUntilUtc={graceRow.graceUntilUtc}
          onClose={() => setGraceRow(null)}
          onUpdated={() => { setGraceRow(null); if (state.status === 'ready') state.retry(); }}
        />
      ) : null}
    </Card>
  );
}
