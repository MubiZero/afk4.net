import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { useI18n } from '@/i18n/I18nProvider';
import type { StaffSession } from '@/auth/staffTokenStore';
import { groupPermissions } from './profileModel';

export function ProfileScreen({ session, branches, roleLabel, onSignOut }: {
  session: StaffSession;
  branches: { branchId: string; name: string }[];
  roleLabel: string;
  onSignOut: () => void;
}) {
  const { t } = useI18n();
  const groups = groupPermissions(session.permissions);

  return (
    <div className="flex max-w-3xl flex-col gap-4">
      <Card>
        <CardHeader><CardTitle>{t('profile.identity.title')}</CardTitle></CardHeader>
        <CardContent className="grid grid-cols-1 gap-3 sm:grid-cols-2">
          <Field label={t('profile.field.displayName')} value={session.displayName} />
          <Field label={t('profile.field.role')} value={roleLabel} />
          <Field label={t('profile.field.organization')} value={session.organizationId} />
          <Field label={t('profile.field.staffId')} value={session.staffUserId} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>{t('profile.branches.title')}</CardTitle></CardHeader>
        <CardContent>
          {branches.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t('profile.branches.empty')}</p>
          ) : (
            <ul className="flex flex-col gap-1">
              {branches.map(b => <li key={b.branchId} className="text-sm">{b.name}</li>)}
            </ul>
          )}
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>{t('profile.permissions.title')}</CardTitle></CardHeader>
        <CardContent className="flex flex-col gap-3">
          {groups.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t('profile.permissions.empty')}</p>
          ) : (
            groups.map(group => (
              <div key={group.key} className="flex flex-col gap-1">
                <div className="text-xs font-medium uppercase text-muted-foreground">{group.key}</div>
                <div className="flex flex-wrap gap-1">
                  {group.permissions.map(p => <Badge key={p} variant="secondary">{p}</Badge>)}
                </div>
              </div>
            ))
          )}
        </CardContent>
      </Card>

      <p className="text-xs text-muted-foreground">{t('profile.editUnavailable')}</p>
      <div><Button variant="outline" onClick={onSignOut}>{t('shell.signOut')}</Button></div>
    </div>
  );
}

function Field({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex flex-col gap-0.5">
      <span className="text-xs text-muted-foreground">{label}</span>
      <span className="text-sm font-medium break-all">{value}</span>
    </div>
  );
}
