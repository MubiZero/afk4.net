import { useEffect, useMemo, useState } from 'react';
import type { JSX } from 'react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen } from '../../management/ManagementScreen';
import { createAuthenticatedOperatorClients } from '../../operatorHelpers';
import type { OperatorBackendContext } from '../../operatorTypes';
import { getInstallerUrl } from './installModel';
import { InstallCodesSection } from './InstallCodesSection';

// Гайд по установке нового ПК: ссылка на дистрибутив (когда задана в конфиге релиза), пошаговый
// флоу Мастера — для одного ПК, и код тихой установки — для зала, который ставят разом скриптом.
export function InstallDestination({ backend }: { backend: OperatorBackendContext | null }): JSX.Element {
  const { t } = useI18n();
  const installerUrl = backend === null ? null : getInstallerUrl(backend.config);
  const [branches, setBranches] = useState<{ branchId: string; name: string }[]>([]);

  const clients = useMemo(
    () => (backend === null ? null : createAuthenticatedOperatorClients(backend.config, backend.session)),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [backend?.config.platformBaseUrl, backend?.session.accessToken]
  );

  useEffect(() => {
    if (clients === null) return undefined;
    let active = true;
    clients.orgBranches
      .getOwnerBranches()
      .then((list) => {
        if (active) setBranches(list);
      })
      .catch(() => {
        /* без списка филиалов код выдать некуда, но гайд по мастеру от этого не ломается */
      });
    return () => {
      active = false;
    };
  }, [clients]);

  const steps = [
    t('op.network.install.step.run'),
    t('op.network.install.step.signIn'),
    t('op.network.install.step.branch'),
    t('op.network.install.step.role'),
    t('op.network.install.step.name'),
    t('op.network.install.step.done')
  ];

  return (
    <ManagementScreen title={t('op.network.dest.install')} subtitle={t('op.network.dest.install.subtitle')} contentWidth="full">
      <section className="management-panel network-install-get">
        <h3>{t('op.network.install.get.title')}</h3>
        <p>{t('op.network.install.get.lead')}</p>
        {installerUrl !== null ? (
          <a className="ui-btn ui-btn--primary" href={installerUrl} download>
            {t('op.network.install.download')}
          </a>
        ) : (
          <p className="network-install-nolink">{t('op.network.install.noUrl')}</p>
        )}
      </section>

      <section className="management-panel network-install-steps">
        <h3>{t('op.network.install.steps.title')}</h3>
        <ol className="network-install-step-list">
          {steps.map((step, i) => (
            <li key={i}>{step}</li>
          ))}
        </ol>
      </section>

      <InstallCodesSection
        clients={clients}
        branches={branches}
        preferredBranchId={backend?.branchId ?? null}
        installerUrl={installerUrl}
      />
    </ManagementScreen>
  );
}
