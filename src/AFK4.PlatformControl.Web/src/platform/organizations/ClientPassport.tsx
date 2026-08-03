import { useEffect, useState, type ReactNode } from 'react';
import { Card, CardContent } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import { minorToMajor } from '@/lib/money';
import type { InvoicesApi } from '@/api/platformClients/invoices';
import type { OrganizationOwnerInvitesApi } from '@/api/platformClients/organizationOwnerInvites';
import type { OrganizationsApi } from '@/api/platformClients/organizations';
import type { SubscriptionsApi } from '@/api/platformClients/subscriptions';
import type { OrganizationDetail, OrganizationSubscription } from '@/api/types';
import { PLAN_LABEL, STATUS_LABEL, STATUS_VARIANT, SUBSCRIPTION_LABEL } from './organizationsModel';
import type { OrganizationPageAccess } from './OrganizationPage';
import { OrganizationProfileDialog } from './OrganizationProfileDialog';
import { SubscriptionDialog } from './SubscriptionDialog';
import { PaymentGraceDialog } from './PaymentGraceDialog';
import { OwnerTransferDialog } from './OwnerTransferDialog';

type OrganizationsClient = Pick<OrganizationsApi, 'updateProfile' | 'updateStatus' | 'updateUpdateChannel' | 'transferOwner'>;
type SubscriptionsClient = Pick<SubscriptionsApi, 'getSubscription' | 'updateSubscription'>;
type InvoicesClient = Pick<InvoicesApi, 'generateInvoice'>;
type OwnerInvitesClient = Pick<OrganizationOwnerInvitesApi, 'listOrganizationOwnerInvites'>;

export interface ClientPassportClients {
  organizations: OrganizationsClient;
  subscriptions: SubscriptionsClient;
  invoices: InvoicesClient;
  organizationOwnerInvites: OwnerInvitesClient;
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
      .catch(() => { /* passport stays useful without price/next-invoice details */ });
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
          .sort((a, b) => b.createdAtUtc.localeCompare(a.createdAtUtc))[0];
        setOwner(accepted ? (accepted.ownerDisplayName ?? accepted.ownerUserName ?? null) : null);
      })
      .catch(() => { /* passport stays useful without the owner row */ });
    return () => { cancelled = true; };
  }, [client, organization.organizationId, access.canManageAccess, tick]);

  const cities = Array.from(new Set(organization.branches.map(b => b.city)));
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

  function refresh() {
    setTick(n => n + 1);
  }

  return (
    <Card className="h-fit">
      <CardContent className="flex flex-col gap-4 text-sm">
        <div>
          <h1 className="text-lg font-bold">{organization.name}</h1>
          <p className="text-xs text-muted-foreground">
            {organization.branches.length} · {cities.join(', ') || '—'}
          </p>
        </div>

        <div className="flex flex-wrap gap-1.5">
          <Badge variant={STATUS_VARIANT[organization.status] ?? 'outline'}>
            {STATUS_LABEL[organization.status] ? t(STATUS_LABEL[organization.status]) : organization.status}
          </Badge>
          {isPastDue ? <Badge variant="destructive">{t('platform.organization.passport.debtChip')}</Badge> : null}
        </div>

        <dl className="flex flex-col gap-2">
          <Row label={t('platform.organization.subscriptionForm.plan')}>
            {PLAN_LABEL[organization.planCode] ? t(PLAN_LABEL[organization.planCode]) : organization.planCode}
          </Row>
          <Row label={t('platform.organization.passport.price')}>
            {subscription === null ? <Skeleton className="h-4 w-16" /> : formatCurrency(minorToMajor(subscription.amountMinorUnits), subscription.currencyCode)}
          </Row>
          <Row label={t('platform.organization.passport.nextInvoice')}>
            {subscription === null ? <Skeleton className="h-4 w-20" /> : subscription.nextInvoiceUtc !== null ? formatDate(subscription.nextInvoiceUtc) : '—'}
          </Row>
          <Row label={t('platform.organization.invites.colOwner')}>
            {access.canManageAccess ? (owner ?? '—') : '—'}
          </Row>
          <Row label={t('platform.organization.passport.updateChannel')}>
            {organization.updateChannel}{organization.pinnedClientVersion !== null ? ` · ${organization.pinnedClientVersion}` : ''}
          </Row>
        </dl>

        <div className="flex flex-col gap-2">
          {access.canManageBilling ? (
            <Button variant="outline" size="sm" onClick={() => setOpenDialog('subscription')}>
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
            <Button variant={nextStatus === 'suspended' ? 'destructive' : 'default'} size="sm" onClick={() => setStatusConfirmOpen(true)}>
              {nextStatus === 'suspended' ? t('platform.organization.passport.action.suspend') : t('platform.organization.passport.action.activate')}
            </Button>
          ) : null}
        </div>
      </CardContent>

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
          onUpdated={next => { setSubscription(next); setOpenDialog(null); refresh(); }}
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
          onTransferred={() => { setOpenDialog(null); refresh(); }}
        />
      ) : null}
    </Card>
  );
}

function Row({ label, children }: { label: string; children: ReactNode }) {
  return (
    <div className="flex items-center justify-between gap-3">
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="text-right font-medium">{children}</dd>
    </div>
  );
}
