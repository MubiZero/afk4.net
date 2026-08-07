import { useEffect, useState, type ReactNode } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import { minorToMajor } from '@/lib/money';
import type { DebtApi } from '@/api/platformClients/debt';
import type { InvoicesApi } from '@/api/platformClients/invoices';
import type { OrganizationOwnerInvitesApi } from '@/api/platformClients/organizationOwnerInvites';
import type { OrganizationsApi } from '@/api/platformClients/organizations';
import type { SubscriptionsApi } from '@/api/platformClients/subscriptions';
import type { DebtRow, OrganizationDetail, OrganizationSubscription } from '@/api/types';
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

export interface ClientPassportClients {
  organizations: OrganizationsClient;
  subscriptions: SubscriptionsClient;
  invoices: InvoicesClient;
  organizationOwnerInvites: OwnerInvitesClient;
  debt: DebtClient;
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

  const [subscription, setSubscription] = useState<OrganizationSubscription | null>(null);
  const [owner, setOwner] = useState<string | null>(null);
  const [debtRow, setDebtRow] = useState<DebtRow | null>(null);
  const [openDialog, setOpenDialog] = useState<DialogKind>(null);
  const [statusConfirmOpen, setStatusConfirmOpen] = useState(false);
  const [statusPending, setStatusPending] = useState(false);
  const [invoicePending, setInvoicePending] = useState(false);
  const [tick, setTick] = useState(0);

  useEffect(() => {
    let cancelled = false;
    setSubscription(null);
    client.subscriptions.getSubscription(organization.organizationId)
      .then(value => { if (!cancelled) setSubscription(value); })
      .catch(() => { /* паспорт остаётся полезным и без цены со сроком счёта */ });
    return () => { cancelled = true; };
  }, [client, organization.organizationId, tick]);

  useEffect(() => {
    let cancelled = false;
    if (!access.canManageAccess) { setOwner(null); return; }
    setOwner(null);
    client.organizationOwnerInvites.listOrganizationOwnerInvites(organization.organizationId)
      .then(invites => {
        if (cancelled) return;
        const accepted = invites
          .filter(invite => invite.status === 'accepted')
          .sort((left, right) => right.createdAtUtc.localeCompare(left.createdAtUtc))[0];
        setOwner(accepted !== undefined ? (accepted.ownerDisplayName ?? accepted.ownerUserName ?? null) : null);
      })
      .catch(() => { /* паспорт остаётся полезным и без строки владельца */ });
    return () => { cancelled = true; };
  }, [client, organization.organizationId, access.canManageAccess, tick]);

  useEffect(() => {
    let cancelled = false;
    setDebtRow(null);
    client.debt.listDebt()
      .then(rows => {
        if (cancelled) return;
        setDebtRow(rows.find(row => row.organizationId === organization.organizationId) ?? null);
      })
      .catch(() => { /* паспорт остаётся полезным и без суммы долга */ });
    return () => { cancelled = true; };
  }, [client, organization.organizationId, tick]);

  const cities = Array.from(new Set(organization.branches.map(branch => branch.city)));
  const nextStatus = organization.status === 'active' ? 'suspended' : 'active';
  const isPastDue = organization.subscriptionStatus === 'past_due';

  async function applyStatus(reason: string) {
    setStatusPending(true);
    try {
      const next = await client.organizations.updateStatus(organization.organizationId, nextStatus, reason);
      onUpdated(next);
      setStatusConfirmOpen(false);
      toast({ title: t('platform.organization.passport.statusUpdated'), variant: 'success' });
    } catch {
      toast({ title: t('platform.organization.action.error'), variant: 'error' });
    } finally {
      setStatusPending(false);
    }
  }

  async function generateInvoice() {
    setInvoicePending(true);
    try {
      await client.invoices.generateInvoice(organization.organizationId);
      toast({ title: t('platform.organization.passport.invoiceGenerated'), variant: 'success' });
    } catch {
      toast({ title: t('platform.organization.action.error'), variant: 'error' });
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
        {isPastDue ? <Badge variant="destructive">{t('platform.organization.passport.debtChip')}</Badge> : null}
      </div>

      <dl className="pc-passport-facts">
        <Row label={t('platform.organization.subscriptionForm.plan')}>
          {PLAN_LABEL[organization.planCode] !== undefined ? t(PLAN_LABEL[organization.planCode]) : organization.planCode}
        </Row>
        <Row label={t('platform.organization.passport.price')}>
          {subscription === null ? <Skeleton className="pc-skel-value" /> : formatCurrency(minorToMajor(subscription.amountMinorUnits), subscription.currencyCode)}
        </Row>
        <Row label={t('platform.organization.passport.nextInvoice')}>
          {subscription === null ? <Skeleton className="pc-skel-value" /> : subscription.nextInvoiceUtc !== null ? formatDate(subscription.nextInvoiceUtc) : '—'}
        </Row>
        <Row label={t('platform.organization.passport.debt.label')}>
          <OrganizationDebtBlock row={debtRow} />
        </Row>
        <Row label={t('platform.organization.invites.colOwner')}>
          {access.canManageAccess ? (owner ?? '—') : '—'}
        </Row>
        <Row label={t('platform.organization.passport.updateChannel')}>
          {organization.updateChannel}{organization.pinnedClientVersion !== null ? ` · ${organization.pinnedClientVersion}` : ''}
        </Row>
      </dl>

      {/* Иерархия действий явная: главный рычаг — условия обслуживания, остальное вторично,
          приостановка отдельно и красным. Прошлая версия давала шесть одинаковых серых кнопок. */}
      <div className="pc-passport-actions">
        {access.canManageBilling ? (
          <Button size="sm" onClick={() => setOpenDialog('subscription')}>
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
          organizationId={organization.organizationId}
          subscription={subscription}
          onClose={() => setOpenDialog(null)}
          onUpdated={next => { setSubscription(next); setOpenDialog(null); setTick(value => value + 1); }}
        />
      ) : null}
      {openDialog === 'grace' ? (
        <PaymentGraceDialog
          client={client.subscriptions}
          organizationId={organization.organizationId}
          currentGraceUntilUtc={subscription?.paymentGraceUntilUtc ?? null}
          onClose={() => setOpenDialog(null)}
          onUpdated={next => { setSubscription(next); setOpenDialog(null); }}
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
