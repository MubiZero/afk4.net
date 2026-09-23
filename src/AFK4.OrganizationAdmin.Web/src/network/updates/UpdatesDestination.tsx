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
import { SectionState } from '../SectionState';
import { SkeletonControl, SkeletonLine, SkeletonTiles } from '../../LoadingSkeleton';
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

  const { rollouts, preference: preferenceSection } = useUpdateStatus(resolvedClient, backend?.branchId ?? '');
  const preference = preferenceSection.status === 'ready' ? preferenceSection.data : null;
  const rollout = rollouts.status === 'ready' ? findAdminRollout(rollouts.data) : null;
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

  // Весь экран меняется на ожидание или ошибку, только пока показать нечего: как только пришла
  // хотя бы одна панель, она остаётся, а соседняя говорит за себя сама.
  const nothingShown = rollouts.status !== 'ready' && preferenceSection.status !== 'ready';
  const screenState = backend === null ? 'loading'
    : !nothingShown ? 'ready'
    : rollouts.status === 'loading' || preferenceSection.status === 'loading' ? 'loading'
    : 'error';
  const retryAll = () => {
    if (rollouts.status === 'error') rollouts.retry();
    if (preferenceSection.status === 'error') preferenceSection.retry();
  };

  async function saveWindow(): Promise<void> {
    if (backend === null || preferenceSection.status !== 'ready') return;
    setSaving(true);
    setFeedback({ label: t('op.network.updates.window.save'), state: 'pending' });
    try {
      const next: OrganizationAdminUpdatePreferenceDto = await resolvedClient.updatePreference(backend.branchId, {
        organizationId: backend.session.organizationId,
        maintenanceWindowStart: toTimeRequest(start),
        maintenanceWindowEnd: toTimeRequest(end)
      });
      preferenceSection.apply(next);
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

  const rolloutsSkeleton = <SkeletonTiles count={3} className="network-updates-facts" />;
  // Фрагмент, а не обёртка: части встают прямо в сетку .mgmt-form, с её же зазорами. Пояснение —
  // настоящим текстом: оно известно до ответа, а от его длины зависит высота панели.
  const windowSkeleton = (
    <>
      <p className="network-updates-lead">{t('op.network.updates.window.lead')}</p>
      <div className="network-updates-window-grid" data-skeleton="form" aria-hidden="true">
        <label><SkeletonLine width="4em" /><SkeletonControl /></label>
        <label><SkeletonLine width="4em" /><SkeletonControl /></label>
        <div className="mgmt-meta-row"><SkeletonLine width="10em" /></div>
      </div>
      <div className="network-updates-actions" aria-hidden="true"><SkeletonControl width="10rem" /></div>
    </>
  );

  return (
    <ManagementScreen
      title={t('op.network.dest.updates')}
      subtitle={t('op.network.dest.updates.subtitle')}
      contentWidth="form"
      state={screenState}
      skeleton={
        <>
          <section className="management-panel network-updates-state">
            <div className="mgmt-section-title"><SkeletonLine width="10em" /></div>
            {rolloutsSkeleton}
          </section>
          <section className="management-panel network-updates-window">
            <div className="mgmt-form">
              <div className="mgmt-section-title"><SkeletonLine width="10em" /></div>
              {windowSkeleton}
            </div>
          </section>
        </>
      }
      failure={rollouts.status === 'error' ? projectOperatorError(rollouts.error, t) : undefined}
      onRetry={retryAll}
    >
      {screenState === 'ready' && (
        <>
          <section className="management-panel network-updates-state">
            <div className="mgmt-section-title"><span>{t('op.network.updates.app.title')}</span></div>
            <SectionState section={rollouts} failedTitle={t('op.network.updates.app.loadFailed')} skeleton={rolloutsSkeleton} />
            {rollouts.status !== 'ready' ? null : rollout === null ? (
              <EmptyState title={t('op.network.updates.app.upToDate')} next={{ kind: 'calm', hint: t('op.network.updates.app.upToDateHint') }} />
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
              <SectionState section={preferenceSection} failedTitle={t('op.network.updates.window.loadFailed')} skeleton={windowSkeleton} />
              {preferenceSection.status === 'ready' && (
                <>
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
                      <span className="mgmt-meta-value">{preferenceSection.data.timeZone || '—'}</span>
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
                </>
              )}
            </div>
          </section>
        </>
      )}
    </ManagementScreen>
  );
}
