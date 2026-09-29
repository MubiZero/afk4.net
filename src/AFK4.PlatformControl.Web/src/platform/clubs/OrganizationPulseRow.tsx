import { useState } from 'react';
import { ChevronRight } from 'lucide-react';
import { Badge } from '@/components/ui/badge';
import { useI18n } from '@/i18n/I18nProvider';
import { formatMoney } from '@afk4/money';
import { PLAN_LABEL } from '@/platform/organizations/organizationsModel';
import type { PulseAlert, PulseClub, PulseOrganization } from '@/api/types';
import { ALERT_BADGE, aggregateOccupancy, alertDetailText, alertLabel, summarizeClubAlerts } from './pulseModel';

interface OrganizationPulseRowProps {
  organization: PulseOrganization;
  defaultExpanded: boolean;
  onOpen: (organizationId: string) => void;
}

export function OrganizationPulseRow({ organization, defaultExpanded, onOpen }: OrganizationPulseRowProps) {
  const { t } = useI18n();
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
                {t('platform.clubs.row.debtLabel')} {formatMoney(organization.outstandingMinorUnits, organization.currencyCode)}
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

// Чип — одно слово тревоги; подробность («последний сигнал 42 минуты назад») — рядом текстом, а
// не только во всплывающей подсказке: на тач-экране и с клавиатуры её не увидеть. В строке клуба
// подробность не пишется — там уже стоит сводка по филиалам.
function AlertChip({ alert, withDetail = false }: { alert: PulseAlert; withDetail?: boolean }) {
  const { t } = useI18n();
  const detail = withDetail ? alertDetailText(alert, t) : undefined;
  return (
    <span className="pulse-alert">
      <Badge variant={ALERT_BADGE[alert.level]}>{t(alertLabel(alert))}</Badge>
      {detail !== undefined ? <span className="pulse-alert-detail">{detail}</span> : null}
    </span>
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
      {/* Открытая смена — обычное дело, и зелёный чип в каждой строке был шумом; отмечаем только
          закрытую. Застрявшую смену называет тревога «Смена не закрыта». */}
      {club.shiftOpen ? null : <Badge variant="outline">{t('platform.clubs.club.shiftClosed')}</Badge>}
      {club.alerts.map(alert => <AlertChip key={alert.kind} alert={alert} withDetail />)}
    </li>
  );
}
