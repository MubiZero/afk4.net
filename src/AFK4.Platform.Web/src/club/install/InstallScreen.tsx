import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { EmptyState } from '@/components/ui/states';
import { useI18n } from '@/i18n/I18nProvider';
import type { OwnerCodeApi } from '@/api/clients/ownerCode';
import { OwnerCodePanel } from './OwnerCodePanel';
import { getSetupMsiUrl } from './installModel';

type Client = Pick<OwnerCodeApi, 'getOwnerCode' | 'generateOwnerCode' | 'rotateOwnerCode'>;

export function InstallScreen({ client, canManage, branches }: {
  client: Client;
  canManage: boolean;
  branches: { branchId: string; name: string; city?: string }[];
}) {
  const { t } = useI18n();
  const msiUrl = getSetupMsiUrl();

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-start justify-between gap-3">
        <div>
          <h2 className="text-lg font-semibold">{t('install.title')}</h2>
          <p className="text-sm text-muted-foreground">{t('install.subtitle')}</p>
        </div>
        <Button asChild>
          <a href={msiUrl} download>{t('install.download')}</a>
        </Button>
      </div>

      <OwnerCodePanel client={client} canManage={canManage} />

      <Card>
        <CardHeader><CardTitle>{t('install.wizard.title')}</CardTitle></CardHeader>
        <CardContent className="flex flex-col gap-3">
          <ol className="list-decimal space-y-1 pl-5 text-sm">
            <li>{t('install.wizard.step1')}</li>
            <li>{t('install.wizard.step2')}</li>
            <li>{t('install.wizard.step3')}</li>
            <li>{t('install.wizard.step4')}</li>
          </ol>
          <pre className="rounded-md bg-muted px-3 py-2 font-mono text-xs">msiexec /i AFK4-Agent.msi</pre>
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>{t('install.branches.title')}</CardTitle></CardHeader>
        <CardContent>
          {branches.length === 0 ? (
            <EmptyState message={t('install.branches.empty')} />
          ) : (
            <ul className="grid grid-cols-1 gap-2 sm:grid-cols-2">
              {branches.map(b => (
                <li key={b.branchId} className="rounded-md border px-3 py-2">
                  <div className="text-sm font-medium">{b.name}</div>
                  {b.city !== undefined && b.city.length > 0 && <div className="text-xs text-muted-foreground">{b.city}</div>}
                </li>
              ))}
            </ul>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
