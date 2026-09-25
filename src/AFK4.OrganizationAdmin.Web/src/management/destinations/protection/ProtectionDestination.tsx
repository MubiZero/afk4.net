import { useEffect, useState } from 'react';
import { AppWindow, Globe, HardDrive } from 'lucide-react';
import { useI18n } from '@afk4/i18n';
import { ManagementScreen, type SaveState } from '../../ManagementScreen';
import { SetupFieldsSkeleton, SetupRuleSkeleton, SetupSection, SetupSectionSkeleton } from '../../kit/SetupSection';
import { RuleSwitch } from '../../kit/RuleSwitch';
import { SkeletonLine } from '../../../LoadingSkeleton';
import { projectOperatorError, type OperatorErrorProjection } from '../../../apiErrors';
import { createAuthenticatedOperatorClients, emptyFeedback } from '../../../operatorHelpers';
import { useFeedbackToasts } from '../../../useFeedbackToasts';
import type { Feedback, LoadStatus } from '../../../operatorTypes';
import type { BranchProtectionProfileDto } from '../../../api/clients/settings';
import {
  buildProtectionRequest,
  hideableDrives,
  protectionDefaults,
  protectionToForm,
  toggleDrive,
  type ProtectionForm
} from './protectionModel';
import { managementScreenState } from '../types';
import type { DestinationProps } from '../types';

/**
 * «Защита ПК» (спека оболочки, §6.3): что агент запрещает на всех игровых ПК филиала. Каждое
 * правило подписано тем, что оно делает на самом деле: «скрыть диск» прячет его в Проводнике, но
 * не запрещает, и клуб должен это знать, а не узнать от гостя.
 */
export function ProtectionDestination({ backend, onDirtyChange }: DestinationProps) {
  const { t, formatDate } = useI18n();
  const [form, setForm] = useState<ProtectionForm>(protectionDefaults);
  const [baseline, setBaseline] = useState<ProtectionForm>(protectionDefaults);
  const [updatedAtUtc, setUpdatedAtUtc] = useState<string | null>(null);
  const [loadStatus, setLoadStatus] = useState<LoadStatus>(backend === null ? 'fixture' : 'loading');
  const [loadFailure, setLoadFailure] = useState<OperatorErrorProjection | undefined>();
  const [dirty, setDirty] = useState(false);
  const [saving, setSaving] = useState(false);
  const [saved, setSaved] = useState(false);
  const [feedback, setFeedback] = useState<Feedback>(emptyFeedback);
  const [reloadNonce, setReloadNonce] = useState(0);
  useFeedbackToasts(feedback);

  const apply = (profile: BranchProtectionProfileDto) => {
    const mapped = protectionToForm(profile);
    setForm(mapped);
    setBaseline(mapped);
    setUpdatedAtUtc(profile.updatedAtUtc);
    setDirty(false);
  };

  useEffect(() => {
    if (backend === null) {
      setLoadStatus('fixture');
      return undefined;
    }
    let active = true;
    setLoadStatus('loading');
    setLoadFailure(undefined);
    createAuthenticatedOperatorClients(backend.config, backend.session).settings
      .getProtectionProfile(backend.branchId)
      .then((profile) => {
        if (!active) return;
        apply(profile);
        setLoadStatus('backend');
      })
      .catch((error) => {
        if (!active) return;
        setLoadStatus('failed');
        setLoadFailure(projectOperatorError(error, t));
      });
    return () => { active = false; };
  }, [backend?.branchId, backend?.config.platformBaseUrl, backend?.session.accessToken, reloadNonce]);

  useEffect(() => { onDirtyChange?.(dirty); }, [dirty, onDirtyChange]);

  const onField = <K extends keyof ProtectionForm>(key: K, value: ProtectionForm[K]) => {
    setForm((prev) => ({ ...prev, [key]: value }));
    setDirty(true);
    setSaved(false);
  };

  const save = async () => {
    if (backend === null) return;
    const label = t('op.management.dest.protection');
    setSaving(true);
    setFeedback({ label, state: 'pending' });
    try {
      const clients = createAuthenticatedOperatorClients(backend.config, backend.session);
      apply(await clients.settings.updateProtectionProfile(backend.branchId, buildProtectionRequest(backend.session.organizationId, form)));
      setSaved(true);
      setFeedback({ label, state: 'confirmed' });
    } catch (error) {
      setFeedback({ label, state: 'failed', detail: projectOperatorError(error, t).detail });
    } finally {
      setSaving(false);
    }
  };

  const discard = () => {
    setForm(baseline);
    setDirty(false);
    setSaved(false);
    setFeedback(emptyFeedback);
  };

  const disabled = backend === null || saving;
  const saveState: SaveState = saving ? 'saving' : dirty ? 'dirty' : saved ? 'saved' : 'clean';

  return (
    <ManagementScreen
      title={t('op.management.dest.protection')}
      subtitle={t('op.management.dest.protection.subtitle')}
      contentWidth="wide"
      state={managementScreenState(loadStatus)}
      skeleton={
        <>
          <p className="payset-origin-note"><SkeletonLine width="22em" /></p>
          <div className="payset-columns">
            <SetupSectionSkeleton lead={t('op.protection.zone.drives.lead')}>
              <SetupRuleSkeleton hint={t('op.protection.usb.hint')} />
              <div className="payset-divider" />
              <SetupFieldsSkeleton hints={[t('op.protection.hiddenDrives.hint')]} single />
            </SetupSectionSkeleton>
            <SetupSectionSkeleton lead={t('op.protection.zone.browsers.lead')}>
              <SetupRuleSkeleton hint={t('op.protection.downloads.hint')} />
              <SetupRuleSkeleton hint={t('op.protection.incognito.hint')} />
              <div className="payset-divider" />
              <SetupFieldsSkeleton hints={[t('op.protection.urls.hint')]} single />
            </SetupSectionSkeleton>
            <SetupSectionSkeleton lead={t('op.protection.zone.windows.lead')}>
              <SetupRuleSkeleton hint={t('op.protection.run.hint')} />
              <div className="payset-divider" />
              <SetupFieldsSkeleton hints={[t('op.protection.windowTitles.hint'), t('op.protection.windowClasses.hint')]} />
            </SetupSectionSkeleton>
          </div>
        </>
      }
      failure={loadFailure}
      onRetry={() => setReloadNonce((nonce) => nonce + 1)}
      save={{ state: saveState, onSave: () => void save(), onDiscard: discard, disabled }}
    >
      <p className="payset-origin-note">
        {updatedAtUtc === null
          ? t('op.protection.defaultsNote')
          : t('op.protection.updatedAt', { date: formatDate(updatedAtUtc) })}
        {' '}
        {t('op.protection.appliesNote')}
      </p>

      <div className="payset-columns">
        <SetupSection Icon={HardDrive} title={t('op.protection.zone.drives')} lead={t('op.protection.zone.drives.lead')}>
          <RuleSwitch
            checked={form.blockRemovableStorage}
            disabled={disabled}
            name={t('op.protection.usb')}
            hint={t('op.protection.usb.hint')}
            onChange={(value) => onField('blockRemovableStorage', value)}
          />

          <div className="payset-divider" />

          <fieldset className="protect-drives">
            <legend>{t('op.protection.hiddenDrives')}</legend>
            <div className="protect-drive-grid">
              {hideableDrives.map((drive) => (
                <button
                  key={drive}
                  type="button"
                  className="protect-drive"
                  aria-pressed={form.hiddenDrives.includes(drive)}
                  aria-label={t('op.protection.hiddenDrives.drive', { drive })}
                  disabled={disabled}
                  onClick={() => onField('hiddenDrives', toggleDrive(form.hiddenDrives, drive))}
                >
                  {drive}:
                </button>
              ))}
            </div>
            <p className="payset-field-hint">{t('op.protection.hiddenDrives.hint')}</p>
          </fieldset>
        </SetupSection>

        <SetupSection Icon={Globe} title={t('op.protection.zone.browsers')} lead={t('op.protection.zone.browsers.lead')}>
          <div className="payset-rules">
            <RuleSwitch
              checked={form.blockBrowserDownloads}
              disabled={disabled}
              name={t('op.protection.downloads')}
              hint={t('op.protection.downloads.hint')}
              onChange={(value) => onField('blockBrowserDownloads', value)}
            />
            <RuleSwitch
              checked={form.blockBrowserIncognito}
              disabled={disabled}
              name={t('op.protection.incognito')}
              hint={t('op.protection.incognito.hint')}
              onChange={(value) => onField('blockBrowserIncognito', value)}
            />
          </div>

          <div className="payset-divider" />

          <ListField
            id="protection-urls"
            label={t('op.protection.urls')}
            hint={t('op.protection.urls.hint')}
            placeholder={'casino.example\n*.betting.example'}
            value={form.urlBlocklist}
            disabled={disabled}
            onChange={(value) => onField('urlBlocklist', value)}
          />
        </SetupSection>

        <SetupSection Icon={AppWindow} title={t('op.protection.zone.windows')} lead={t('op.protection.zone.windows.lead')}>
          <RuleSwitch
            checked={form.disableRunDialog}
            disabled={disabled}
            name={t('op.protection.run')}
            hint={t('op.protection.run.hint')}
            onChange={(value) => onField('disableRunDialog', value)}
          />

          <div className="payset-divider" />

          <ListField
            id="protection-window-titles"
            label={t('op.protection.windowTitles')}
            hint={t('op.protection.windowTitles.hint')}
            placeholder={'Командная строка\nРедактор реестра'}
            value={form.blockedTitles}
            disabled={disabled}
            onChange={(value) => onField('blockedTitles', value)}
          />
          <ListField
            id="protection-window-classes"
            label={t('op.protection.windowClasses')}
            hint={t('op.protection.windowClasses.hint')}
            placeholder="ConsoleWindowClass"
            value={form.blockedClasses}
            disabled={disabled}
            onChange={(value) => onField('blockedClasses', value)}
          />

          <p className="protect-base-note">{t('op.protection.baseNote')}</p>
        </SetupSection>
      </div>
    </ManagementScreen>
  );
}

function ListField({ id, label, hint, placeholder, value, disabled, onChange }: {
  id: string;
  label: string;
  hint: string;
  placeholder: string;
  value: string;
  disabled: boolean;
  onChange: (value: string) => void;
}) {
  return (
    <div className="payset-field protect-list">
      <label htmlFor={id}>{label}</label>
      <textarea
        id={id}
        rows={4}
        spellCheck={false}
        placeholder={placeholder}
        value={value}
        disabled={disabled}
        onChange={(event) => onChange(event.currentTarget.value)}
      />
      <p className="payset-field-hint">{hint}</p>
    </div>
  );
}
