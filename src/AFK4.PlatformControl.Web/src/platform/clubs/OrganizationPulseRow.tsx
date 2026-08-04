import { useState } from 'react';
import { ChevronRight } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { useI18n } from '@/i18n/I18nProvider';
import { minorToMajor } from '@/lib/money';
import { PLAN_LABEL } from '@/platform/organizations/organizationsModel';
import type { PulseAlert, PulseAlertLevel, PulseClub, PulseOrganization } from '@/api/types';
import { aggregateOccupancy, alertDetailText, alertLabel, summarizeClubAlerts } from './pulseModel';

const ALERT_VARIANT: Record<PulseAlertLevel, 'secondary' | 'warning' | 'destructive'> = {
  normal: 'secondary',
  attention: 'warning',
  critical: 'destructive'
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
    <li className="pulse-net" data-testid="pulse-row" data-alert-level={organization.alertLevel}>
      <div className="pulse-net-head">
        {/* Раскрытие — отдельная мишень с явной иконкой. Строка рядом ведёт в карточку клиента:
            одна строка = одно действие, второе действие = отдельная кнопка. */}
        <button
          type="button"
          className="pulse-chevron"
          aria-expanded={expanded}
          aria-label={t(expanded ? 'platform.clubs.row.collapse' : 'platform.clubs.row.expand', { name: organization.name })}
          onClick={() => setExpanded(value => !value)}
        >
          <ChevronRight size={16} aria-hidden="true" />
        </button>

        <button type="button" className="ctable-row pulse-row" onClick={() => onOpen(organization.organizationId)}>
          <span className="pulse-name">
            <strong>{organization.name}</strong>
            <span>{planLabel}</span>
          </span>
          <span className="pulse-summary">{aggregateText}</span>
          <span className="pulse-chips">
            {organization.outstandingMinorUnits > 0 ? (
              <Badge variant="warning">
                {t('platform.clubs.row.debtLabel')}: {formatCurrency(minorToMajor(organization.outstandingMinorUnits), organization.currencyCode)}
              </Badge>
            ) : null}
            {organization.alerts.map(alert => <AlertChip key={alert.kind} alert={alert} />)}
          </span>
        </button>
      </div>

      {expanded ? (
        <ul className="pulse-clubs">
          {organization.clubs.length === 0
            ? <li className="pulse-empty">{t('platform.clubs.row.noClubs')}</li>
            : organization.clubs.map(club => <ClubRow key={club.branchId} club={club} />)}
        </ul>
      ) : null}
    </li>
  );
}

function AlertChip({ alert }: { alert: PulseAlert }) {
  const { t } = useI18n();
  const detail = alertDetailText(alert, t);
  return (
    <Badge variant={ALERT_VARIANT[alert.level]} title={detail}>
      {t(alertLabel(alert))}
    </Badge>
  );
}

function ClubRow({ club }: { club: PulseClub }) {
  const { t } = useI18n();
  return (
    <li className="pulse-club">
      <span className="pulse-club-name">
        {club.name}
        <span> · {club.city}</span>
      </span>
      <span className="pulse-metric">{t('platform.clubs.club.devices', { online: club.devicesOnline, total: club.devicesTotal })}</span>
      <span className="pulse-metric">{t('platform.clubs.club.seats', { occupied: club.seatsOccupied, total: club.seatsTotal })}</span>
      <Badge variant={club.shiftOpen ? 'success' : 'outline'}>
        {t(club.shiftOpen ? 'platform.clubs.club.shiftOpen' : 'platform.clubs.club.shiftClosed')}
      </Badge>
      {club.alerts.map(alert => <AlertChip key={alert.kind} alert={alert} />)}
    </li>
  );
}
