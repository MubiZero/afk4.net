import { useEffect, useState } from 'react';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { LoadingCards, ErrorState } from '@/components/ui/states';
import { useToast } from '@/components/ui/toast';
import { useI18n } from '@/i18n/I18nProvider';
import type { RolesApi } from '@/api/platformClients/roles';
import type { PlatformRole } from '@/api/types';
import { describeRoleActionError, groupPermissions } from './rolesModel';

type Client = Pick<RolesApi, 'listRoles' | 'listPermissions' | 'createRole' | 'updateRole' | 'deleteRole'>;

interface Draft {
  roleName: string;
  displayName: string;
  description: string;
  permissions: Set<string>;
}

function draftFrom(role: PlatformRole): Draft {
  return {
    roleName: role.roleName,
    displayName: role.displayName,
    description: role.description,
    permissions: new Set(role.permissions)
  };
}

export function RolesSection({ client }: { client: Client }) {
  const { t } = useI18n();
  const { toast } = useToast();
  const [roles, setRoles] = useState<PlatformRole[] | null>(null);
  const [permissions, setPermissions] = useState<string[]>([]);
  const [failed, setFailed] = useState(false);
  const [tick, setTick] = useState(0);
  const [draft, setDraft] = useState<Draft | null>(null);
  const [pending, setPending] = useState(false);

  useEffect(() => {
    let cancelled = false;
    setFailed(false);
    Promise.all([client.listRoles(), client.listPermissions()])
      .then(([loadedRoles, loadedPermissions]) => {
        if (cancelled) return;
        setRoles(loadedRoles);
        setPermissions(loadedPermissions);
      })
      .catch(() => { if (!cancelled) setFailed(true); });
    return () => { cancelled = true; };
  }, [client, tick]);

  function reload() {
    setDraft(null);
    setTick(value => value + 1);
  }

  async function save() {
    if (draft === null || pending) return;
    const existing = roles?.some(role => role.roleName === draft.roleName) ?? false;
    setPending(true);
    try {
      const payload = {
        displayName: draft.displayName.trim(),
        description: draft.description.trim(),
        permissions: [...draft.permissions]
      };
      if (existing) {
        await client.updateRole(draft.roleName, payload);
      } else {
        await client.createRole(draft.roleName.trim().toLowerCase(), payload);
      }
      toast({ title: t('platform.settings.roles.saved'), variant: 'success' });
      reload();
    } catch (cause) {
      toast({ title: describeRoleActionError(cause, t), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  async function remove(role: PlatformRole) {
    if (pending) return;
    setPending(true);
    try {
      await client.deleteRole(role.roleName);
      toast({ title: t('platform.settings.roles.deleted'), variant: 'success' });
      reload();
    } catch (cause) {
      toast({ title: describeRoleActionError(cause, t), variant: 'error' });
    } finally {
      setPending(false);
    }
  }

  if (failed) return <ErrorState message={t('platform.settings.roles.error.load')} retryLabel={t('state.retry')} onRetry={reload} />;
  if (roles === null) return <LoadingCards count={1} />;

  const nameIsValid = draft !== null
    && draft.roleName.trim().length > 0
    && draft.displayName.trim().length > 0;
  const hasPermissions = draft !== null && draft.permissions.size > 0;

  return (
    <Card>
      <CardHeader>
        <CardTitle>{t('platform.settings.roles.title')}</CardTitle>
        <Button
          variant="outline"
          onClick={() => setDraft({ roleName: '', displayName: '', description: '', permissions: new Set() })}
        >
          {t('platform.settings.roles.create')}
        </Button>
      </CardHeader>
      <CardContent>
        <p className="mgmt-drawer-hint">{t('platform.settings.roles.description')}</p>

        {roles.map(role => (
          <div key={role.roleName} className="pc-kv">
            <span>
              {role.displayName} <code>{role.roleName}</code>
              {role.isBuiltIn ? <Badge variant="outline">{t('platform.settings.roles.builtIn')}</Badge> : null}
              {role.grantsAllPermissions ? <Badge variant="success">{t('platform.settings.roles.fullAccess')}</Badge> : null}
            </span>
            <span>
              <span className="pc-num">{t('platform.settings.roles.holders', { count: role.adminCount })}</span>
              <Button variant="outline" disabled={pending} onClick={() => setDraft(draftFrom(role))}>
                {t('platform.settings.roles.edit')}
              </Button>
              {/* Удаление показывается только у не встроенной роли: рычаг, который заведомо
                  ответит отказом, хуже отсутствующего. */}
              {role.isBuiltIn ? null : (
                <Button variant="outline" disabled={pending} onClick={() => remove(role)}>
                  {t('platform.settings.roles.delete')}
                </Button>
              )}
            </span>
          </div>
        ))}

        {draft === null ? null : (
          <div className="mgmt-form">
            {roles.some(role => role.roleName === draft.roleName) ? null : (
              <label>
                {t('platform.settings.roles.name')}
                <Input
                  value={draft.roleName}
                  onChange={event => setDraft({ ...draft, roleName: event.target.value })}
                />
              </label>
            )}
            <label>
              {t('platform.settings.roles.displayName')}
              <Input
                value={draft.displayName}
                onChange={event => setDraft({ ...draft, displayName: event.target.value })}
              />
            </label>
            <label>
              {t('platform.settings.roles.descriptionField')}
              <Input
                value={draft.description}
                onChange={event => setDraft({ ...draft, description: event.target.value })}
              />
            </label>

            {/* У роли с полным доступом состав не редактируется: она описывается флагом, а не
                списком, чтобы получать и права, которых ещё не существует. */}
            {draft.permissions.size > 0 && roles.find(role => role.roleName === draft.roleName)?.grantsAllPermissions
              ? <p className="mgmt-drawer-hint">{t('platform.settings.roles.fullAccessNotEditable')}</p>
              : (
                <fieldset>
                  <legend>{t('platform.settings.roles.permissions')}</legend>
                  {groupPermissions(permissions).map(([group, groupPermissionNames]) => (
                    <div key={group}>
                      <strong>{group}</strong>
                      {groupPermissionNames.map(permission => (
                        <label key={permission}>
                          <input
                            type="checkbox"
                            checked={draft.permissions.has(permission)}
                            onChange={event => {
                              const next = new Set(draft.permissions);
                              if (event.target.checked) next.add(permission); else next.delete(permission);
                              setDraft({ ...draft, permissions: next });
                            }}
                          />
                          {permission}
                        </label>
                      ))}
                    </div>
                  ))}
                </fieldset>
              )}

            {nameIsValid ? null : <p className="mgmt-drawer-hint">{t('platform.settings.roles.nameRequired')}</p>}
            {hasPermissions ? null : <p className="mgmt-drawer-hint">{t('platform.settings.roles.permissionsRequired')}</p>}

            <Button disabled={pending || !nameIsValid || !hasPermissions} onClick={save}>
              {t('platform.settings.roles.save')}
            </Button>
            <Button variant="outline" disabled={pending} onClick={() => setDraft(null)}>
              {t('platform.settings.roles.cancel')}
            </Button>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
