import { useEffect, useState } from 'react';
import { useI18n } from '@afk4/i18n';
import type { ClubPlanDevicesDto } from '@afk4/contracts';
import { projectOperatorError } from '../../apiErrors';

export interface FreePlanDevicesClient {
  getDevices(): Promise<ClubPlanDevicesDto>;
  setDevices(deviceIds: string[]): Promise<ClubPlanDevicesDto>;
}

/**
 * ПК сверх предела бесплатного тарифа (спека тарифов клуба, §5a): новые сессии идут только на
 * десяти, остальные — «вне тарифа». Какие десять — решает владелец; пока не решил, работают
 * подключённые раньше. Выбор сохраняется только после ответа сервера.
 */
export function FreePlanDevices({
  client,
  canManage,
  onChanged
}: {
  client: FreePlanDevicesClient;
  canManage: boolean;
  onChanged: () => void;
}) {
  const { t } = useI18n();
  const [devices, setDevices] = useState<ClubPlanDevicesDto | null>(null);
  const [chosen, setChosen] = useState<Set<string> | null>(null);
  const [saving, setSaving] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let active = true;
    client.getDevices().then((result) => { if (active) setDevices(result); }).catch(() => { if (active) setError(t('op.network.plan.outside.loadFailed')); });
    return () => { active = false; };
  }, [client, t]);

  if (devices === null || devices.limit === null || devices.limit === undefined) {
    return error ? <p className="ui-inline-error" role="alert">{error}</p> : null;
  }

  const limit = devices.limit;
  const works = devices.devices.filter((device) => device.works).length;
  const editing = chosen !== null;
  const start = () => setChosen(new Set(devices.devices.filter((device) => device.works).map((device) => device.deviceId)));
  const toggle = (deviceId: string) => setChosen((current) => {
    if (current === null) return current;
    const next = new Set(current);
    if (next.has(deviceId)) next.delete(deviceId);
    else if (next.size < limit) next.add(deviceId);
    return next;
  });
  const save = async () => {
    if (chosen === null) return;
    setSaving(true);
    setError(null);
    try {
      setDevices(await client.setDevices([...chosen]));
      setChosen(null);
      onChanged();
    } catch (failure) {
      setError(projectOperatorError(failure, t).detail);
    } finally {
      setSaving(false);
    }
  };

  return (
    <div className="network-plan-outside">
      <strong>{t('op.network.plan.outside.title', { works, devices: devices.devices.length })}</strong>
      <p>{t('op.network.plan.outside.lead', { limit })}</p>
      <ul className="network-plan-outside-list">
        {devices.devices.map((device) => {
          const on = editing ? chosen.has(device.deviceId) : device.works;
          return (
            <li key={device.deviceId} className={on ? 'is-on' : undefined}>
              <label>
                <input
                  type="checkbox"
                  checked={on}
                  disabled={!editing || saving || (!on && chosen !== null && chosen.size >= limit)}
                  onChange={() => toggle(device.deviceId)}
                />
                <span className="network-plan-outside-name">{device.name}</span>
                <span className="network-plan-outside-hall">{device.branchName}</span>
                {!editing && !device.works ? <span className="network-plan-outside-tag">{t('op.floor.outsidePlan')}</span> : null}
              </label>
            </li>
          );
        })}
      </ul>
      {canManage ? (
        <div className="network-plan-actions">
          {editing ? (
            <>
              <button type="button" className="ui-btn ui-btn--primary" disabled={saving} onClick={() => void save()}>
                {t('op.network.plan.outside.save', { chosen: chosen.size, limit })}
              </button>
              <button type="button" className="ui-btn" disabled={saving} onClick={() => setChosen(null)}>{t('op.network.plan.outside.cancel')}</button>
            </>
          ) : (
            <button type="button" className="ui-btn" onClick={start}>{t('op.network.plan.outside.choose')}</button>
          )}
        </div>
      ) : null}
      {error && <p className="ui-inline-error" role="alert">{error}</p>}
    </div>
  );
}
