import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { useI18n } from '@/i18n/I18nProvider';
import type { PlatformAdminSession } from '@/auth/tokenStore';
import { groupPermissions } from './profileModel';

export function ProfileScreen({ session, onSignOut }: {
  session: PlatformAdminSession;
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
          <Field label={t('platform.profile.field.userName')} value={session.userName} />
          <Field label={t('platform.profile.field.adminId')} value={session.platformAdminId} />
        </CardContent>
      </Card>

      <Card>
        <CardHeader><CardTitle>{t('platform.profile.roles.title')}</CardTitle></CardHeader>
        <CardContent>
          {session.roles.length === 0 ? (
            <p className="text-sm text-muted-foreground">{t('platform.profile.roles.empty')}</p>
          ) : (
            <div className="flex flex-wrap gap-1">
              {session.roles.map(r => <Badge key={r} variant="secondary">{r}</Badge>)}
            </div>
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
