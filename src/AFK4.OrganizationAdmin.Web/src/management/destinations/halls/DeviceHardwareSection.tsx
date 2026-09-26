import { useEffect, useState } from 'react';
import { useI18n, type MessageKey } from '@afk4/i18n';
import { HardwareComponentNames, type DeviceHardwareDto, type HardwareSnapshotDto } from '@afk4/contracts';
import { Skeleton } from '../../../operatorPrimitives';
import { projectOperatorError } from '../../../apiErrors';
import type { createAuthenticatedOperatorClients } from '../../../operatorHelpers';

type Clients = ReturnType<typeof createAuthenticatedOperatorClients>;

const COMPONENT_LABELS: Record<string, MessageKey> = {
  [HardwareComponentNames.Cpu]: 'op.hardware.cpu',
  [HardwareComponentNames.Memory]: 'op.hardware.memory',
  [HardwareComponentNames.Gpu]: 'op.hardware.gpu',
  [HardwareComponentNames.Motherboard]: 'op.hardware.motherboard',
  [HardwareComponentNames.Disk]: 'op.hardware.disks',
  [HardwareComponentNames.PhysicalDisk]: 'op.hardware.physicalDisks',
  [HardwareComponentNames.Monitor]: 'op.hardware.monitors'
};

/**
 * Железо ПК (P9): что стоит сейчас и что поменялось с принятого. Поменяли видеокарту — клуб видит
 * «было → стало» и принимает новое как норму; не меняли — значит, её вынули.
 */
export function DeviceHardwareSection({ clients, deviceId, canAccept, onAccepted }: {
  clients: Clients | null;
  deviceId: string;
  canAccept: boolean;
  onAccepted: () => void;
}) {
  const { t, formatDate } = useI18n();
  const [hardware, setHardware] = useState<DeviceHardwareDto | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [busy, setBusy] = useState(false);

  useEffect(() => {
    if (clients === null) return undefined;
    let active = true;
    setHardware(null);
    setError(null);
    // Через Promise.resolve: клиент без метода — отказ в строке раздела, а не упавшая карточка.
    Promise.resolve()
      .then(() => clients.devices.getHardware(deviceId))
      .then((next) => { if (active) setHardware(next); })
      .catch((reason) => { if (active) setError(projectOperatorError(reason, t).detail); });
    return () => { active = false; };
  }, [clients, deviceId]);

  const accept = async () => {
    if (clients === null) return;
    setBusy(true);
    try {
      setHardware(await clients.devices.acceptHardware(deviceId));
      onAccepted();
    } catch (reason) {
      setError(projectOperatorError(reason, t).detail);
    } finally {
      setBusy(false);
    }
  };

  if (error !== null) return <p className="ui-inline-error" role="alert">{error}</p>;
  if (hardware === null) return <Skeleton variant="text" lines={4} />;
  if (hardware.current === null) return <p className="mgmt-drawer-hint">{t('op.hardware.none')}</p>;

  return (
    <div className="device-hardware">
      {hardware.changes.length > 0 && (
        <div className="device-hardware-changes" role="status">
          <strong>{t('op.hardware.changed')}</strong>
          <ul>
            {hardware.changes.map((change) => (
              <li key={change.component}>
                <span>{t(COMPONENT_LABELS[change.component] ?? 'op.hardware.other')}</span>
                <s>{change.was ?? t('op.hardware.nothing')}</s>
                <b>{change.now ?? t('op.hardware.nothing')}</b>
              </li>
            ))}
          </ul>
          {canAccept && (
            <button type="button" className="ui-btn ui-btn--primary ui-btn--sm" disabled={busy} onClick={() => void accept()}>
              {t('op.hardware.accept')}
            </button>
          )}
          <p className="mgmt-drawer-hint">{t('op.hardware.acceptHint')}</p>
        </div>
      )}
      <HardwareList snapshot={hardware.current} />
      <p className="mgmt-drawer-hint">
        {t('op.hardware.reportedAt', { time: formatDate(hardware.reportedAtUtc!) })}
        {' · '}
        {hardware.acceptedByName
          ? t('op.hardware.acceptedBy', { name: hardware.acceptedByName, time: formatDate(hardware.acceptedAtUtc!) })
          : t('op.hardware.acceptedFirst')}
      </p>
    </div>
  );
}

function HardwareList({ snapshot }: { snapshot: HardwareSnapshotDto }) {
  const { t } = useI18n();
  const rows: [MessageKey, string | null][] = [
    ['op.hardware.cpu', snapshot.cpu ? `${snapshot.cpu} · ${t('op.hardware.threads', { count: snapshot.cpuThreads })}` : null],
    ['op.hardware.memory', snapshot.memoryGb > 0 ? `${snapshot.memoryGb} GB` : null],
    ['op.hardware.gpu', snapshot.gpus.map((gpu) => (gpu.memoryGb ? `${gpu.name} · ${gpu.memoryGb} GB` : gpu.name)).join(', ') || null],
    ['op.hardware.motherboard', snapshot.motherboard],
    ['op.hardware.disks', snapshot.disks.map((disk) => `${disk.name} ${disk.sizeGb} GB`).join(', ') || null]
  ];
  // null — агент этого не присылает (старая версия или не прочиталось): строки нет, а не «—».
  if (snapshot.physicalDisks) {
    rows.push(['op.hardware.physicalDisks', snapshot.physicalDisks
      .map((disk) => [disk.model, `${disk.sizeGb} GB`, disk.interface].filter(Boolean).join(' · '))
      .join(', ') || null]);
  }
  if (snapshot.monitors) {
    rows.push(['op.hardware.monitors', snapshot.monitors
      .map((monitor) => (monitor.serial ? `${monitor.name} (${monitor.serial})` : monitor.name))
      .join(', ') || null]);
  }
  rows.push(['op.hardware.os', snapshot.os]);
  return (
    <div className="settings-device-detail-grid">
      {rows.map(([label, value]) => (
        <span key={label}><strong>{t(label)}</strong><b>{value ?? '—'}</b></span>
      ))}
    </div>
  );
}
