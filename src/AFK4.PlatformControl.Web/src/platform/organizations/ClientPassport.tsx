import { useEffect, useRef, useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Skeleton } from '@/components/ui/skeleton';
import { PartialFailure } from '@/components/ui/states';
import { ConfirmDialog } from '@/components/shared/ConfirmDialog';
import { useToast } from '@/components/ui/toast';
import { Inspector, Money, useBlockedReason, type Fact, type RowAction } from '@afk4/ui/react';
import { describeApiError } from '@/api/describeApiError';
import { useI18n } from '@/i18n/I18nProvider';
import type { DebtApi } from '@/api/platformClients/debt';
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
type OwnerInvitesClient = Pick<OrganizationOwnerInvitesApi, 'listOrganizationOwnerInvites'>;
type DebtClient = Pick<DebtApi, 'listDebt'>;
type PlansClient = Pick<PlansApi, 'listPlans'>;

// Счёт по подписке выставляется на вкладке «Счета», рядом со списком счетов: раньше та же кнопка
// стояла и здесь, и человек не знал, какая из двух настоящая.
export interface ClientPassportClients {
  organizations: OrganizationsClient;
  subscriptions: SubscriptionsClient;
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
type StatusTarget = 'active' | 'suspended' | 'deletion_pending';

export function ClientPassport({ client, organization, access, onUpdated }: Props) {
  const { t, formatDate } = useI18n();
  const { toast } = useToast();

  const [openDialog, setOpenDialog] = useState<DialogKind>(null);
  const [statusTarget, setStatusTarget] = useState<StatusTarget | null>(null);
  const [statusPending, setStatusPending] = useState(false);
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
  // Пока подписка в пути, о запросе говорит скелетон цены. А когда она не пришла, полоса сверху
  // говорит только «часть сведений не загрузилась» — какая кнопка из-за этого серая, надо сказать.
  const subscriptionBlocked = useBlockedReason(
    subscriptionPart.status === 'failed' ? t('platform.organization.passport.blocked.subscriptionNotLoaded') : null);

  const cities = Array.from(new Set(organization.branches.map(branch => branch.city)));
  const isPastDue = organization.subscriptionStatus === 'past_due';
  // Отсрочка не откатывает уже случившийся past_due (§7/§8) — это законное состояние, а не сбой,
  // и держать тревожный чип рядом со спокойным «Отсрочка до …» ломает инвариант «клуб под
  // отсрочкой — спокойное состояние». Гасим чип, только когда точно знаем про активную отсрочку;
  // при неизвестном статусе долга безопаснее оставить чип как есть.
  const isUnderActiveGrace = debtStatus === 'ready' && debtRow !== null && debtRow.graceUntilUtc !== null;

  // Статус клуба меняется только здесь (решение владельца 29.09: статус — в паспорте). Работающий
  // клуб можно приостановить или начать его уход; приостановленный и уходящий — вернуть в работу.
  const statusActions: { status: StatusTarget; label: string; danger: boolean }[] = !access.canManageStatus ? [] : organization.status === 'active'
    ? [
      { status: 'suspended', label: t('platform.organization.passport.action.suspend'), danger: true },
      { status: 'deletion_pending', label: t('platform.organization.passport.action.startOffboarding'), danger: true }
    ]
    : [
      { status: 'active', label: t('platform.organization.passport.action.activate'), danger: false },
      ...(organization.status === 'suspended'
        ? [{ status: 'deletion_pending' as const, label: t('platform.organization.passport.action.startOffboarding'), danger: true }]
        : [])
    ];

  async function applyStatus(reason: string) {
    if (statusTarget === null) return;
    setStatusPending(true);
    try {
      const next = await client.organizations.updateStatus(organization.organizationId, statusTarget, reason);
      onUpdated(next);
      setStatusTarget(null);
      toast({ title: t('platform.organization.passport.statusUpdated'), variant: 'success' });
    } catch (cause) {
      toast({ title: describeApiError(cause, t), variant: 'error' });
    } finally {
      setStatusPending(false);
    }
  }

  // Редкое и опасное — в «⋯»: передача владельцу и смена статуса. Раньше это были ещё три кнопки
  // во всю ширину под главной, и «Приостановить» красным стояло вплотную к «Передать».
  const menuActions: RowAction[] = [
    ...(access.canTransferOwner
      ? [{ id: 'ownerTransfer', label: t('platform.organization.passport.action.transferOwner'), onSelect: () => setOpenDialog('ownerTransfer') }]
      : []),
    ...statusActions.map(action => ({ id: `status-${action.status}`, label: action.label, danger: action.danger, onSelect: () => setStatusTarget(action.status) }))
  ];

  const secondary = [
    access.canManageSubscriptions ? (
      <Button key="grace" variant="outline" size="sm" onClick={() => setOpenDialog('grace')}>
        {t('platform.organization.passport.action.paymentGrace')}
      </Button>
    ) : null,
    access.canManageProfile ? (
      <Button key="profile" variant="outline" size="sm" onClick={() => setOpenDialog('profile')}>
        {t('platform.organization.passport.action.editProfile')}
      </Button>
    ) : null
  ].filter(node => node !== null);

  const priceValue = subscriptionPart.status === 'loading' ? <Skeleton className="pc-skel-value" />
    : subscription === null ? t('platform.organization.passport.unknownValue')
    : <Money minorUnits={subscription.amountMinorUnits} currencyCode={subscription.currencyCode} />;
  const nextInvoiceHint = subscription === null ? undefined
    : subscription.nextInvoiceUtc !== null
      ? t('platform.organization.passport.nextInvoiceOn', { date: formatDate(subscription.nextInvoiceUtc) })
      : t('platform.organization.passport.noNextInvoice');

  const facts: Fact[] = [
    { label: t('platform.organization.subscriptionForm.plan'), value: PLAN_LABEL[organization.planCode] !== undefined ? t(PLAN_LABEL[organization.planCode]) : organization.planCode },
    { label: t('platform.organization.passport.debt.label'), value: <OrganizationDebtBlock row={debtRow} status={debtStatus} /> },
    // «—» здесь значит «владельца нет», и подменять им несостоявшийся запрос нельзя: отсутствие
    // владельца — повод завести код приглашения, а неудача — повод повторить.
    { label: t('platform.organization.invites.colOwner'), value: ownerPart.status === 'failed' ? t('platform.organization.passport.unknownValue') : (owner ?? '—') },
    // Соседняя секция канала обновлений называет его словами; паспорт печатал сырое stable/beta.
    { label: t('platform.organization.passport.updateChannel'), value: `${t(channelLabelKey(organization.updateChannel))}${organization.pinnedClientVersion !== null ? ` · ${organization.pinnedClientVersion}` : ''}` },
    { label: t('platform.organization.passport.referral.code'), value: organization.referral.code ?? t('platform.organization.passport.referral.codeNotIssued') },
    // «Приведён кем-то» — не общий случай: строка «—» у всех клубов, что пришли сами, была бы шумом.
    ...(organization.referral.referredByOrganizationId !== null ? [{
      label: t('platform.organization.passport.referral.referredBy'),
      value: `${organization.referral.referredByOrganizationName ?? '—'} · ${organization.referral.rewardedAtUtc !== null
        ? t('platform.organization.passport.referral.rewardedOn', { date: formatDate(organization.referral.rewardedAtUtc) })
        : t('platform.organization.passport.referral.notRewardedYet')}`
    }] : []),
    ...(organization.referral.referred.length > 0 ? [{
      label: t('platform.organization.passport.referral.referredClubs'),
      value: organization.referral.referred
        .map(club => club.rewarded ? club.name : `${club.name} (${t('platform.organization.passport.referral.notRewarded')})`)
        .join(', ')
    }] : [])
  ];

  // Имя клуба уже стоит заголовком страницы, поэтому паспорт озаглавлен «О клубе», а не вторым
  // «Orion Gaming». Статус — только здесь: во вкладке «Лимиты» его второго переключателя больше нет.
  return (
    <>
      <Inspector
        className="pc-passport"
        title={t('platform.organization.passport.title')}
        status={(
          <>
            <Badge variant={STATUS_VARIANT[organization.status] ?? 'outline'}>
              {STATUS_LABEL[organization.status] !== undefined ? t(STATUS_LABEL[organization.status]) : organization.status}
            </Badge>
            {isPastDue && !isUnderActiveGrace ? <Badge variant="destructive">{t('platform.organization.passport.debtChip')}</Badge> : null}
          </>
        )}
        subtitle={`${t('platform.organization.passport.branchCount', { count: organization.branches.length })}${cities.length > 0 ? ` · ${cities.join(', ')}` : ''}`}
        menu={menuActions.length > 0 ? { label: t('platform.organization.passport.more'), actions: menuActions } : undefined}
        figure={{ label: t('platform.organization.passport.price'), value: priceValue, hint: nextInvoiceHint }}
      >
        {somethingFailed ? (
          <PartialFailure
            title={t('platform.organization.passport.partialError')}
            retryLabel={t('state.retry')}
            onRetry={reloadParts}
          />
        ) : null}

        {access.canManageSubscriptions || secondary.length > 0 ? (
          <Inspector.Actions
            // Диалог условий строится вокруг текущей подписки: пока её нет в руках, открывать нечего.
            primary={access.canManageSubscriptions ? (
              <Button
                block
                disabled={subscription === null}
                aria-describedby={subscriptionBlocked.describedBy}
                onClick={() => setOpenDialog('subscription')}
              >
                {t('platform.organization.passport.action.editSubscription')}
              </Button>
            ) : undefined}
            hint={subscriptionBlocked.hint}
            secondary={secondary.length === 2 ? [secondary[0], secondary[1]] : secondary.length === 1 ? [secondary[0]] : []}
          />
        ) : null}

        <Inspector.Facts items={facts} />
      </Inspector>

      <ConfirmDialog
        open={statusTarget !== null}
        title={statusTarget === 'suspended' ? t('platform.organization.passport.suspendTitle')
          : statusTarget === 'deletion_pending' ? t('platform.organization.passport.offboardTitle')
          : t('platform.organization.passport.activateTitle')}
        description={statusTarget === 'deletion_pending' ? t('platform.organization.passport.offboardBody') : undefined}
        confirmLabel={t('platform.organization.statusForm.confirm')}
        cancelLabel={t('platform.organization.statusForm.cancel')}
        reasonLabel={statusTarget !== 'active' ? t('platform.organization.statusForm.reason') : undefined}
        destructive={statusTarget !== 'active'}
        pending={statusPending}
        onConfirm={reason => void applyStatus(reason)}
        onOpenChange={open => { if (!open) setStatusTarget(null); }}
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
    </>
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
