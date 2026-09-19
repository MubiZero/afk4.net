import { useEffect, useRef, useState, type ReactNode } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { PartialFailure } from '@/components/ui/states';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { useToast } from '@/components/ui/toast';
import { describeApiError } from '@/api/describeApiError';
import { useAttemptKey } from '@/api/useAttemptKey';
import { useI18n } from '@/i18n/I18nProvider';
import { minorToMajor } from '@/lib/money';
import type { DebtApi } from '@/api/platformClients/debt';
import type { InvoicesApi } from '@/api/platformClients/invoices';
import type { PlansApi } from '@/api/platformClients/plans';
import type { OrganizationOwnerInvitesApi } from '@/api/platformClients/organizationOwnerInvites';
import type { OrganizationsApi } from '@/api/platformClients/organizations';
import type { SubscriptionsApi } from '@/api/platformClients/subscriptions';
import type { OrganizationDetail } from '@/api/types';
import { channelLabelKey } from '@/platform/updates/updatesModel';
import { PLAN_LABEL, STATUS_LABEL, STATUS_VARIANT } from './organizationsModel';
import type { OrganizationPageAccess } from './OrganizationPage';
import { OrganizationDebtBlock } from './OrganizationDebtBlock';
import { OrganizationProfileDialog } from './OrganizationProfileDialog';
import { SubscriptionDialog } from './SubscriptionDialog';
import { PaymentGraceDialog } from './PaymentGraceDialog';
import { OwnerTransferDialog } from './OwnerTransferDialog';

type OrganizationsClient = Pick<OrganizationsApi, 'updateProfile' | 'updateStatus' | 'updateUpdateChannel' | 'transferOwner'>;
type SubscriptionsClient = Pick<SubscriptionsApi, 'getSubscription' | 'updateSubscription'>;
type InvoicesClient = Pick<InvoicesApi, 'generateInvoice'>;
type OwnerInvitesClient = Pick<OrganizationOwnerInvitesApi, 'listOrganizationOwnerInvites'>;
type DebtClient = Pick<DebtApi, 'listDebt'>;
type PlansClient = Pick<PlansApi, 'listPlans'>;

export interface ClientPassportClients {
  organizations: OrganizationsClient;
  subscriptions: SubscriptionsClient;
  invoices: InvoicesClient;
  organizationOwnerInvites: OwnerInvitesClient;
  debt: DebtClient;
  plans: PlansClient;
}

interface Props {
  client: ClientPassportClients;
  organization: OrganizationDetail;
  access: OrganizationPageAccess;
  onUpdated: (next: OrganizationDetail) => void;
}

type DialogKind = 'profile' | 'subscription' | 'grace' | 'ownerTransfer' | null;

export function ClientPassport({ client, organization, access, onUpdated }: Props) {
  const { t, formatCurrency, formatDate } = useI18n();
  const { toast } = useToast();

  const [openDialog, setOpenDialog] = useState<DialogKind>(null);
  const [statusConfirmOpen, setStatusConfirmOpen] = useState(false);
  const [statusPending, setStatusPending] = useState(false);
  const [invoicePending, setInvoicePending] = useState(false);
  const attempt = useAttemptKey();
  const [tick, setTick] = useState(0);

  // Три запроса паспорта независимы, и падение одного не должно стирать остальные: цена со
  // сроком счёта, владелец и долг приходят из разных мест и требуют разных прав.
  //
  // Раньше каждый сбой гасился пустым catch. Цена и дата оставались вечным скелетоном — человек
  // ждал данных, которых уже не будет; владелец молча превращался в «—», неотличимо от «владельца
  // нет»; а кнопка «Изменить условия обслуживания» просто не открывала диалог, потому что подписки
  // в руках не было. Теперь каждая часть знает, спросили её или нет, и неудачу видно словами.
  const subscriptionPart = usePassportPart(
    () => client.subscriptions.getSubscription(organization.organizationId),
    [client, organization.organizationId, tick]);
  const ownerPart = usePassportPart(
    access.canManageAccess
      ? async () => {
        const invites = await client.organizationOwnerInvites.listOrganizationOwnerInvites(organization.organizationId);
        const accepted = invites
          .filter(invite => invite.status === 'accepted')
          .sort((left, right) => right.createdAtUtc.localeCompare(left.createdAtUtc))[0];
        return accepted !== undefined ? (accepted.ownerDisplayName ?? accepted.ownerUserName ?? null) : null;
      }
      : null,
    [client, organization.organizationId, access.canManageAccess, tick]);
  // `/api/platform/debt` требует platform.billing.view — сотрудник без этого права получит 403,
  // а гасить ошибку и рисовать «Долгов нет» нельзя: это выглядит как утверждение, что долга
  // нет, хотя на деле мы просто не спрашивали.
  const debtPart = usePassportPart(
    access.canViewBilling
      ? async () => {
        const rows = await client.debt.listDebt();
        return rows.find(row => row.organizationId === organization.organizationId) ?? null;
      }
      : null,
    [client, organization.organizationId, access.canViewBilling, tick]);

  const subscription = subscriptionPart.status === 'ready' ? subscriptionPart.value : null;
  const owner = ownerPart.status === 'ready' ? ownerPart.value : null;
  const debtRow = debtPart.status === 'ready' ? debtPart.value : null;
  const debtStatus = debtPart.status === 'ready' ? 'ready' : 'unknown';
  const somethingFailed = [subscriptionPart, ownerPart, debtPart].some(part => part.status === 'failed');
  const reloadParts = () => setTick(value => value + 1);

  const cities = Array.from(new Set(organization.branches.map(branch => branch.city)));
  const nextStatus = organization.status === 'active' ? 'suspended' : 'active';
  const isPastDue = organization.subscriptionStatus === 'past_due';
  // Отсрочка не откатывает уже случившийся past_due (§7/§8) — это законное состояние, а не сбой,
  // и держать тревожный чип рядом со спокойным «Отсрочка до …» ломает инвариант «клуб под
  // отсрочкой — спокойное состояние». Гасим чип, только когда точно знаем про активную отсрочку;
  // при неизвестном статусе долга безопаснее оставить чип как есть.
  const isUnderActiveGrace = debtStatus === 'ready' && debtRow !== null && debtRow.graceUntilUtc !== null;

  async function applyStatus(reason: string) {
    setStatusPending(true);
    try {
      const next = await client.organizations.updateStatus(organization.organizationId, nextStatus, reason);
      onUpdated(next);
      setStatusConfirmOpen(false);
      toast({ title: t('platform.organization.passport.statusUpdated'), variant: 'success' });
    } catch (cause) {
      toast({ title: describeApiError(cause, t), variant: 'error' });
    } finally {
      setStatusPending(false);
    }
  }

  async function generateInvoice() {
    setInvoicePending(true);
    try {
      await client.invoices.generateInvoice(
        organization.organizationId,
        attempt.forSubject({ action: 'generate', organizationId: organization.organizationId }));
      attempt.done();
      toast({ title: t('platform.organization.passport.invoiceGenerated'), variant: 'success' });
    } catch (cause) {
      toast({ title: describeApiError(cause, t), variant: 'error' });
    } finally {
      setInvoicePending(false);
    }
  }

  return (
    <aside className="pc-passport">
      <div className="pc-passport-id">
        <strong>{organization.name}</strong>
        <span>{t('platform.organization.passport.branchCount', { count: organization.branches.length })}{cities.length > 0 ? ` · ${cities.join(', ')}` : ''}</span>
      </div>

      <div className="pc-passport-chips">
        <Badge variant={STATUS_VARIANT[organization.status] ?? 'outline'}>
          {STATUS_LABEL[organization.status] !== undefined ? t(STATUS_LABEL[organization.status]) : organization.status}
        </Badge>
        {isPastDue && !isUnderActiveGrace ? <Badge variant="destructive">{t('platform.organization.passport.debtChip')}</Badge> : null}
      </div>

      {somethingFailed ? (
        <PartialFailure
          title={t('platform.organization.passport.partialError')}
          retryLabel={t('state.retry')}
          onRetry={reloadParts}
        />
      ) : null}

      <dl className="pc-passport-facts">
        <Row label={t('platform.organization.subscriptionForm.plan')}>
          {PLAN_LABEL[organization.planCode] !== undefined ? t(PLAN_LABEL[organization.planCode]) : organization.planCode}
        </Row>
        <Row label={t('platform.organization.passport.price')}>
          {subscriptionPart.status === 'loading' ? <Skeleton className="pc-skel-value" />
            : subscription === null ? t('platform.organization.passport.unknownValue')
            : formatCurrency(minorToMajor(subscription.amountMinorUnits), subscription.currencyCode)}
        </Row>
        <Row label={t('platform.organization.passport.nextInvoice')}>
          {subscriptionPart.status === 'loading' ? <Skeleton className="pc-skel-value" />
            : subscription === null ? t('platform.organization.passport.unknownValue')
            : subscription.nextInvoiceUtc !== null ? formatDate(subscription.nextInvoiceUtc) : '—'}
        </Row>
        <Row label={t('platform.organization.passport.debt.label')}>
          <OrganizationDebtBlock row={debtRow} status={debtStatus} />
        </Row>
        <Row label={t('platform.organization.invites.colOwner')}>
          {/* «—» здесь значит «владельца нет», и подменять им несостоявшийся запрос нельзя:
              отсутствие владельца — повод завести код доступа, а неудача — повод повторить. */}
          {ownerPart.status === 'failed' ? t('platform.organization.passport.unknownValue') : (owner ?? '—')}
        </Row>
        <Row label={t('platform.organization.passport.updateChannel')}>
          {/* Соседняя секция канала обновлений давно называет его словами; паспорт печатал сырое
              stable/beta/internal, и один и тот же канал на одном экране читался двумя способами. */}
          {t(channelLabelKey(organization.updateChannel))}{organization.pinnedClientVersion !== null ? ` · ${organization.pinnedClientVersion}` : ''}
        </Row>
      </dl>

      {/* Иерархия действий явная: главный рычаг — условия обслуживания, остальное вторично,
          приостановка отдельно и красным. Прошлая версия давала шесть одинаковых серых кнопок. */}
      <div className="pc-passport-actions">
        {access.canManageBilling ? (
          // Диалог условий строится вокруг текущей подписки: пока её нет в руках, открывать
          // нечего. Мёртвая на вид кнопка без объяснения хуже погашенной — рядом стоит полоса,
          // которая говорит, что сведения не загрузились, и предлагает повторить.
          <Button size="sm" disabled={subscription === null} onClick={() => setOpenDialog('subscription')}>
            {t('platform.organization.passport.action.editSubscription')}
          </Button>
        ) : null}
        {access.canManageBilling ? (
          <Button variant="outline" size="sm" disabled={invoicePending} onClick={() => void generateInvoice()}>
            {t('platform.organization.passport.action.generateInvoice')}
          </Button>
        ) : null}
        {access.canManageBilling ? (
          <Button variant="outline" size="sm" onClick={() => setOpenDialog('grace')}>
            {t('platform.organization.passport.action.paymentGrace')}
          </Button>
        ) : null}
        {access.canManageProfile ? (
          <Button variant="outline" size="sm" onClick={() => setOpenDialog('profile')}>
            {t('platform.organization.passport.action.editProfile')}
          </Button>
        ) : null}
        {access.canTransferOwner ? (
          <Button variant="outline" size="sm" onClick={() => setOpenDialog('ownerTransfer')}>
            {t('platform.organization.passport.action.transferOwner')}
          </Button>
        ) : null}
        {access.canManageOrganization ? (
          <Button
            variant={nextStatus === 'suspended' ? 'destructive' : 'default'}
            size="sm"
            className="pc-passport-danger"
            onClick={() => setStatusConfirmOpen(true)}
          >
            {nextStatus === 'suspended' ? t('platform.organization.passport.action.suspend') : t('platform.organization.passport.action.activate')}
          </Button>
        ) : null}
      </div>

      <ConfirmDialog
        open={statusConfirmOpen}
        title={nextStatus === 'suspended' ? t('platform.organization.passport.suspendTitle') : t('platform.organization.passport.activateTitle')}
        confirmLabel={t('platform.organization.statusForm.confirm')}
        cancelLabel={t('platform.organization.statusForm.cancel')}
        reasonLabel={nextStatus === 'suspended' ? t('platform.organization.statusForm.reason') : undefined}
        destructive={nextStatus === 'suspended'}
        pending={statusPending}
        onConfirm={reason => void applyStatus(reason)}
        onOpenChange={open => { if (!open) setStatusConfirmOpen(false); }}
      />

      {openDialog === 'profile' ? (
        <OrganizationProfileDialog
          client={client.organizations}
          organization={organization}
          onClose={() => setOpenDialog(null)}
          onUpdated={next => { onUpdated(next); setOpenDialog(null); }}
        />
      ) : null}
      {openDialog === 'subscription' && subscription !== null ? (
        <SubscriptionDialog
          client={client.subscriptions}
          plansClient={client.plans}
          organizationId={organization.organizationId}
          subscription={subscription}
          onClose={() => setOpenDialog(null)}
          onUpdated={() => { setOpenDialog(null); reloadParts(); }}
        />
      ) : null}
      {openDialog === 'grace' ? (
        <PaymentGraceDialog
          client={client.subscriptions}
          organizationId={organization.organizationId}
          currentGraceUntilUtc={subscription?.paymentGraceUntilUtc ?? null}
          onClose={() => setOpenDialog(null)}
          onUpdated={() => { setOpenDialog(null); reloadParts(); }}
        />
      ) : null}
      {openDialog === 'ownerTransfer' ? (
        <OwnerTransferDialog
          client={client.organizations}
          organizationId={organization.organizationId}
          onClose={() => setOpenDialog(null)}
          onTransferred={() => { setOpenDialog(null); setTick(value => value + 1); }}
        />
      ) : null}
    </aside>
  );
}

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="pc-passport-row">
      <dt>{label}</dt>
      <dd>{children}</dd>
    </div>
  );
}

/// Одна графа паспорта: её могли не спрашивать (нет права), ещё ждать, получить или не получить.
/// Разница между «не спрашивали» и «не смогли» важна на экране: первое — нормальное состояние
/// сотрудника с узкими правами, второе — повод нажать «Повторить».
type PassportPart<T> =
  | { status: 'skipped'; value: null }
  | { status: 'loading'; value: null }
  | { status: 'failed'; value: null }
  | { status: 'ready'; value: T };

function usePassportPart<T>(load: (() => Promise<T>) | null, deps: readonly unknown[]): PassportPart<T> {
  const [part, setPart] = useState<PassportPart<T>>(load === null ? { status: 'skipped', value: null } : { status: 'loading', value: null });
  const loadRef = useRef(load);
  loadRef.current = load;

  useEffect(() => {
    const current = loadRef.current;
    if (current === null) { setPart({ status: 'skipped', value: null }); return; }
    let cancelled = false;
    setPart({ status: 'loading', value: null });
    current()
      .then(value => { if (!cancelled) setPart({ status: 'ready', value }); })
      .catch(() => { if (!cancelled) setPart({ status: 'failed', value: null }); });
    return () => { cancelled = true; };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, deps);

  return part;
}
