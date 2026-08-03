import { useState } from 'react';
import { Badge } from '@/components/ui/badge';
import { useI18n } from '@/i18n/I18nProvider';
import { minorToMajor } from '@/lib/money';
import { cn } from '@/lib/utils';
import { PLAN_LABEL } from '@/platform/organizations/organizationsModel';
import type { PulseAlertLevel, PulseClub, PulseOrganization } from '@/api/types';
import { aggregateOccupancy, alertDetailText, alertLabel, summarizeClubAlerts } from './pulseModel';

const BORDER_CLASS: Record<PulseAlertLevel, string> = {
  normal: 'border-l-primary',
  attention: 'border-l-warning',
  critical: 'border-l-danger'
};

interface OrganizationPulseRowProps {
  organization: PulseOrganization;
  defaultExpanded: boolean;
  onOpen: (organizationId: string) => void;
}

export function OrganizationPulseRow({ organization, defaultExpanded, onOpen }: OrganizationPulseRowProps) {
  const { t, formatCurrency } = useI18n();
  const [expanded, setExpanded] = useState(defaultExpanded);

  const occupancy = aggregateOccupancy(organization.clubs);
  const alertSummary = organization.alertLevel !== 'normal' ? summarizeClubAlerts(organization.clubs) : null;
  const aggregateText = alertSummary !== null
    ? t(
        alertSummary.kind === 'silent' ? 'platform.clubs.aggregate.silent' : 'platform.clubs.aggregate.attention',
        { affected: alertSummary.affected, total: alertSummary.total }
      )
    : t('platform.clubs.row.seatsOccupied', { occupied: occupancy.seatsOccupied, total: occupancy.seatsTotal });

  const planLabel = PLAN_LABEL[organization.planCode] !== undefined ? t(PLAN_LABEL[organization.planCode]) : organization.planCode;

  return (
    <div
      data-testid="pulse-row"
      data-alert-level={organization.alertLevel}
      className={cn('overflow-hidden rounded-lg border border-l-4 border-border bg-card', BORDER_CLASS[organization.alertLevel])}
    >
      <div
        role="button"
        tabIndex={0}
        aria-expanded={expanded}
        onClick={() => setExpanded(value => !value)}
        onKeyDown={event => {
          if (event.key !== 'Enter' && event.key !== ' ') return;
          event.preventDefault();
          setExpanded(value => !value);
        }}
        className="flex w-full cursor-pointer flex-wrap items-center gap-3 px-4 py-3 text-left hover:bg-accent focus-visible:bg-accent"
      >
        <span className="min-w-0 flex-1">
          <a
            href={`/admin/organizations/${encodeURIComponent(organization.organizationId)}`}
            onClick={event => { event.preventDefault(); event.stopPropagation(); onOpen(organization.organizationId); }}
            className="block font-semibold text-foreground underline-offset-4 hover:underline"
          >
            {organization.name}
          </a>
          <span className="block text-xs text-muted-foreground">{planLabel}</span>
        </span>
        <span className="text-sm text-muted-foreground">{aggregateText}</span>
        {organization.outstandingMinorUnits > 0 ? (
          <Badge variant="outline">
            {t('platform.clubs.row.debtLabel')}: {formatCurrency(minorToMajor(organization.outstandingMinorUnits), organization.currencyCode)}
          </Badge>
        ) : null}
        {organization.alerts.map(alert => (
          <Badge
            key={alert.kind}
            variant={alert.level === 'critical' ? 'destructive' : 'secondary'}
            title={alertDetailText(alert, t)}
          >
            {t(alertLabel(alert))}
          </Badge>
        ))}
      </div>
      {expanded ? (
        <div className="divide-y divide-border border-t border-border">
          {organization.clubs.length === 0
            ? <p className="px-4 py-3 text-sm text-muted-foreground">{t('platform.clubs.row.noClubs')}</p>
            : organization.clubs.map(club => <ClubRow key={club.branchId} club={club} />)}
        </div>
      ) : null}
    </div>
  );
}

function ClubRow({ club }: { club: PulseClub }) {
  const { t } = useI18n();
  return (
    <div className="flex flex-wrap items-center gap-3 px-4 py-2.5 text-sm">
      <span className="min-w-0 flex-1">
        <span className="font-medium text-foreground">{club.name}</span>
        <span className="text-muted-foreground"> · {club.city}</span>
      </span>
      <span className="text-muted-foreground">{t('platform.clubs.club.devices', { online: club.devicesOnline, total: club.devicesTotal })}</span>
      <span className="text-muted-foreground">{t('platform.clubs.club.seats', { occupied: club.seatsOccupied, total: club.seatsTotal })}</span>
      <Badge variant={club.shiftOpen ? 'success' : 'outline'}>
        {t(club.shiftOpen ? 'platform.clubs.club.shiftOpen' : 'platform.clubs.club.shiftClosed')}
      </Badge>
      {club.alerts.map(alert => (
        <Badge key={alert.kind} variant={alert.level === 'critical' ? 'destructive' : 'secondary'} title={alertDetailText(alert, t)}>
          {t(alertLabel(alert))}
        </Badge>
      ))}
    </div>
  );
}
