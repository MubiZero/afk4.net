import { useI18n, type MessageKey } from '@afk4/i18n';
import { ProtectionItemNames, ProtectionItemStatusNames, type DeviceProtectionReportDto } from '@afk4/contracts';

const ITEM_LABELS: Record<string, MessageKey> = {
  [ProtectionItemNames.KioskBaseline]: 'op.protection.report.item.kioskBaseline',
  [ProtectionItemNames.RemovableStorage]: 'op.protection.report.item.removableStorage',
  [ProtectionItemNames.BrowserDownloads]: 'op.protection.report.item.browserDownloads',
  [ProtectionItemNames.BrowserIncognito]: 'op.protection.report.item.browserIncognito',
  [ProtectionItemNames.BrowserUrlBlocklist]: 'op.protection.report.item.browserUrlBlocklist',
  [ProtectionItemNames.RunDialog]: 'op.protection.report.item.runDialog',
  [ProtectionItemNames.HiddenDrives]: 'op.protection.report.item.hiddenDrives'
};

const STATUS_LABELS: Record<string, MessageKey> = {
  [ProtectionItemStatusNames.Applied]: 'op.protection.report.status.applied',
  [ProtectionItemStatusNames.ExplorerOnly]: 'op.protection.report.status.explorerOnly',
  [ProtectionItemStatusNames.Failed]: 'op.protection.report.status.failed',
  [ProtectionItemStatusNames.Unsupported]: 'op.protection.report.status.unsupported',
  [ProtectionItemStatusNames.Released]: 'op.protection.report.status.released'
};

/**
 * Что ПК доложил о защите (спека оболочки, §6.3) — как есть: «действует», «скрыт только в
 * Проводнике», «не применилось». Отчёт старой версии помечен: правило в Панели сохранено, а до
 * этого ПК ещё не доехало.
 */
export function DeviceProtectionReport({ report, branchVersion }: {
  report: DeviceProtectionReportDto | null | undefined;
  branchVersion: number;
}) {
  const { t, formatDate } = useI18n();

  if (!report) {
    return <p className="mgmt-drawer-hint">{t('op.protection.report.none')}</p>;
  }

  return (
    <div className="device-protection">
      <p className="mgmt-drawer-hint">
        {t('op.protection.report.appliedAt', { version: report.version, time: formatDate(report.appliedAtUtc) })}
      </p>
      {report.version < branchVersion && (
        <p className="device-protection-behind" role="status">
          {t('op.protection.report.behind', { version: report.version, branch: branchVersion })}
        </p>
      )}
      <ul className="device-protection-items">
        {report.items.map((item) => (
          <li key={item.item} data-status={item.status}>
            <span>{ITEM_LABELS[item.item] ? t(ITEM_LABELS[item.item]) : item.item}</span>
            <b title={item.detail ?? undefined}>{STATUS_LABELS[item.status] ? t(STATUS_LABELS[item.status]) : item.status}</b>
          </li>
        ))}
      </ul>
    </div>
  );
}
