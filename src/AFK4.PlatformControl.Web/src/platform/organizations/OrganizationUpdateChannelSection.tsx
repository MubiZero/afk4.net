import { useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select } from '@/components/ui/select';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { OrganizationsApi } from '@/api/platformClients/organizations';
import type { OrganizationDetail } from '@/api/types';

type Client = Pick<OrganizationsApi, 'updateUpdateChannel'>;

const CHANNEL_OPTIONS = ['stable', 'beta', 'canary'] as const;

interface Props {
  client: Client;
  organization: OrganizationDetail;
  onUpdated: (next: OrganizationDetail) => void;
}

export function OrganizationUpdateChannelSection({ client, organization, onUpdated }: Props) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [channel, setChannel] = useState(organization.updateChannel);
  const [pinnedClientVersion, setPinnedClientVersion] = useState(organization.pinnedClientVersion ?? '');
  const [pending, setPending] = useState(false);

  const dirty = channel !== organization.updateChannel || pinnedClientVersion.trim() !== (organization.pinnedClientVersion ?? '');

  async function submit() {
    setPending(true);
    try {
      const next = await client.updateUpdateChannel(organization.organizationId, {
        channel,
        pinnedClientVersion: pinnedClientVersion.trim() === '' ? null : pinnedClientVersion.trim()
      });
      onUpdated(next);
      toast({ title: t('platform.organization.updateChannelForm.updated'), variant: 'success' });
    } catch {
      toast({ title: t('platform.organization.action.error'), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  return (
    <Card>
      <CardHeader><CardTitle>{t('platform.organization.updateChannelForm.title')}</CardTitle></CardHeader>
      <CardContent>
        <label className="ui-field">
          <span>{t('platform.organization.updateChannelForm.channel')}</span>
          <Select value={channel} onChange={event => setChannel(event.target.value)}>
              {CHANNEL_OPTIONS.map(option => <option key={option} value={option}>{option}</option>)}
          </Select>
        </label>
        <label className="ui-field">
          <span>{t('platform.organization.updateChannelForm.pinnedVersion')}</span>
          <Input aria-label={t('platform.organization.updateChannelForm.pinnedVersion')} value={pinnedClientVersion} onChange={e => setPinnedClientVersion(e.target.value)} />
          <span className="pc-field-hint">{t('platform.organization.updateChannelForm.pinnedVersionHint')}</span>
        </label>
        <div>
          <Button disabled={pending || !dirty} onClick={() => void submit()}>{t('platform.organization.updateChannelForm.apply')}</Button>
        </div>
      </CardContent>
    </Card>
  );
}
