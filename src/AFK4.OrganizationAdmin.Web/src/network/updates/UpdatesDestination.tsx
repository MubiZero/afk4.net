import { useMemo, useState } from 'react';
import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../../management/ManagementScreen';
import { EmptyState } from '../../operatorPrimitives';
import { createAuthenticatedOperatorClients, emptyFeedback, triggerFeedback } from '../../operatorHelpers';
import { useFeedbackToasts } from '../../useFeedbackToasts';
import { projectOperatorError } from '../../apiErrors';
import { isHostBridgeAvailable, postHostRequest } from '../../hostBridge';
import type { Feedback, OperatorBackendContext } from '../../operatorTypes';
import type { OrganizationAdminUpdatePreferenceDto } from '../../api/clients/updates';
import { useUpdateStatus, type UpdateStatusClient } from './useUpdateStatus';
import {
  canRestartNow,
  findAdminRollout,
  isWindowValid,
  latestDeviceStatus,
  safeUpdateMessage,
  toTimeInput,
  toTimeRequest,
  updateStatusLabelKey,
  updateStatusTone
} from './updatesModel';

// «Сеть → Обновления»: что происходит с обновлением этого рабочего места и когда клубу удобно его
// обновлять. Публикацию пакетов и раскатку ведёт платформа — здесь только то, на что клуб влияет
// сам: окно обслуживания и «перезапустить сейчас».
// `client` подставляется в тестах: иначе экран зависел бы от общего фабричного хелпера, который
// соседние тест-файлы подменяют на весь процесс (`mock.module` в bun течёт за пределы файла).
export function UpdatesDestination({
  backend,
  client: injectedClient
}: {
  backend: OperatorBackendContext | null;
  client?: UpdateStatusClient;
}): JSX.Element {
  const { t } = useI18n();
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  useFeedbackToasts(feedback);

  const resolvedClient = useMemo<UpdateStatusClient>(() => {
    if (injectedClient !== undefined) return injectedClient;
    if (backend === null) {
      return {
        getRolloutStatuses: async () => { throw new Error('no backend'); },
        getPreference: async () => { throw new Error('no backend'); },
        updatePreference: async () => { throw new Error('no backend'); }
      };
    }
    const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
    return {
      getRolloutStatuses: (branchId) => clients.updates.getRolloutStatuses(branchId),
      getPreference: (branchId) => clients.updates.getPreference(branchId),
      updatePreference: (branchId, request) => clients.updates.updatePreference(branchId, request)
    };
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, [injectedClient, backend?.config.platformBaseUrl, backend?.session.accessToken]);

  const state = useUpdateStatus(resolvedClient, backend?.branchId ?? '');
  const preference = state.status === 'ready' ? state.preference : null;
  const rollout = state.status === 'ready' ? findAdminRollout(state.rollouts) : null;
  const deviceStatus = latestDeviceStatus(rollout);

  // Черновик правки, а не копия сохранённого: пока его нет, поля показывают то, что на сервере.
  // Копировать через эффект нельзя — первый кадр вышел бы с пустыми полями, и правка пользователя
  // затиралась бы при каждом обновлении данных.
  const [draft, setDraft] = useState<{ start: string; end: string } | null>(null);
  const [saving, setSaving] = useState(false);
  const start = draft?.start ?? toTimeInput(preference?.maintenanceWindowStart);
  const end = draft?.end ?? toTimeInput(preference?.maintenanceWindowEnd);
  const setStart = (value: string) => setDraft({ start: value, end });
  const setEnd = (value: string) => setDraft({ start, end: value });

  const screenState = backend === null || state.status === 'loading' ? 'loading' : state.status === 'error' ? 'error' : 'ready';

  async function saveWindow(): Promise<void> {
    if (backend === null || state.status !== 'ready') return;
    setSaving(true);
    setFeedback({ label: t('op.network.updates.window.save'), state: 'pending' });
    try {
      const next: OrganizationAdminUpdatePreferenceDto = await resolvedClient.updatePreference(backend.branchId, {
        organizationId: backend.session.organizationId,
        maintenanceWindowStart: toTimeRequest(start),
        maintenanceWindowEnd: toTimeRequest(end)
      });
      state.applyPreference(next);
      setDraft(null);
      triggerFeedback(setFeedback, t('op.network.updates.window.saved'), 'confirmed');
    } catch (error) {
      triggerFeedback(setFeedback, t('op.network.updates.window.save'), 'failed', projectOperatorError(error, t).detail);
    } finally {
      setSaving(false);
    }
  }

  async function restartAndUpdate(): Promise<void> {
    if (rollout === null) return;
    setFeedback({ label: t('op.network.updates.restart'), state: 'pending' });
    try {
      await postHostRequest('update:restartAndInstall', {
        updateRolloutId: rollout.updateRolloutId,
        updatePackageId: rollout.updatePackageId
      });
      triggerFeedback(setFeedback, t('op.network.updates.restartAccepted'), 'confirmed');
    } catch (error) {
      triggerFeedback(setFeedback, t('op.network.updates.restart'), 'failed', projectOperatorError(error, t).detail);
    }
  }

  const detail = safeUpdateMessage(deviceStatus?.message);
  const statusLabelKey = deviceStatus === null ? null : updateStatusLabelKey(deviceStatus.status);
  const windowDirty = preference !== null
    && (start !== toTimeInput(preference.maintenanceWindowStart) || end !== toTimeInput(preference.maintenanceWindowEnd));

  return (
    <ManagementScreen
      title={t('op.network.dest.updates')}
      subtitle={t('op.network.dest.updates.subtitle')}
      contentWidth="form"
      state={screenState}
      onRetry={state.status === 'error' ? state.retry : undefined}
    >
      {state.status === 'ready' && (
        <>
          <section className="management-panel network-updates-state">
            <div className="mgmt-section-title"><span>{t('op.network.updates.app.title')}</span></div>
            {rollout === null ? (
              <EmptyState title={t('op.network.updates.app.upToDate')} description={t('op.network.updates.app.upToDateHint')} />
            ) : (
              <>
                <dl className="network-updates-facts">
                  <div>
                    <dt>{t('op.network.updates.installedVersion')}</dt>
                    <dd>{deviceStatus?.installedVersion || '—'}</dd>
                  </div>
                  <div>
                    <dt>{t('op.network.updates.targetVersion')}</dt>
                    <dd>{deviceStatus?.targetVersion || rollout.version}</dd>
                  </div>
                  <div>
                    <dt>{t('op.network.updates.state')}</dt>
                    <dd>
                      {deviceStatus === null ? '—' : (
                        <span className={`ui-chip ui-chip--status ${updateStatusTone(deviceStatus.status)}`}>
                          {statusLabelKey === null ? deviceStatus.status : t(statusLabelKey)}
                        </span>
                      )}
                    </dd>
                  </div>
                </dl>
                {detail !== null && (
                  <p className="network-updates-detail" role={deviceStatus?.status === 'failed' ? 'alert' : undefined}>{detail}</p>
                )}
                {canRestartNow(deviceStatus) && (
                  isHostBridgeAvailable() ? (
                    <div className="network-updates-actions">
                      <button type="button" className="ui-btn ui-btn--primary" onClick={() => void restartAndUpdate()}>
                        {t('op.network.updates.restart')}
                      </button>
                      <span className="network-updates-hint">{t('op.network.updates.restartHint')}</span>
                    </div>
                  ) : (
                    <p className="network-updates-hint">{t('op.network.updates.restartOnDevice')}</p>
                  )
                )}
              </>
            )}
          </section>

          <section className="management-panel network-updates-window">
            <div className="mgmt-form">
              <div className="mgmt-section-title"><span>{t('op.network.updates.window.title')}</span></div>
              <p className="network-updates-lead">{t('op.network.updates.window.lead')}</p>
              <div className="network-updates-window-grid">
                <label>
                  {t('op.network.updates.window.from')}
                  <input
                    type="time"
                    value={start}
                    disabled={saving}
                    onChange={(event) => setStart(event.currentTarget.value)}
                  />
                </label>
                <label>
                  {t('op.network.updates.window.to')}
                  <input
                    type="time"
                    value={end}
                    disabled={saving}
                    onChange={(event) => setEnd(event.currentTarget.value)}
                  />
                </label>
                <div className="mgmt-meta-row">
                  <span className="mgmt-meta-label">{t('op.network.updates.window.timeZone')}</span>
                  <span className="mgmt-meta-value">{state.preference.timeZone || '—'}</span>
                </div>
              </div>
              <div className="network-updates-actions">
                <button
                  type="button"
                  className="ui-btn ui-btn--primary"
                  disabled={!windowDirty || !isWindowValid(start, end) || saving}
                  onClick={() => void saveWindow()}
                >
                  {t('op.network.updates.window.save')}
                </button>
                {!isWindowValid(start, end) && (
                  <span className="network-updates-hint">{t('op.network.updates.window.invalid')}</span>
                )}
              </div>
            </div>
          </section>
        </>
      )}
    </ManagementScreen>
  );
}
