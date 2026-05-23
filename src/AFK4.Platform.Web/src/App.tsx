import { useEffect, useMemo, useState } from 'react';
import { PlatformApiClient } from './api/platformApi';
import type { OwnerInvite } from './api/types';
import { readSession, type PlatformAdminSession } from './auth/tokenStore';
import { SignIn } from './components/SignIn';
import { TenantList } from './components/TenantList';
import { TenantDetailView } from './components/TenantDetail';
import { NewTenant } from './components/NewTenant';

type View =
  | { kind: 'list' }
  | { kind: 'new' }
  | { kind: 'detail'; organizationId: string; initialInvite: OwnerInvite | null };

export interface AppProps {
  apiBaseUrl: string;
}

export default function App({ apiBaseUrl }: AppProps) {
  const [session, setSession] = useState<PlatformAdminSession | null>(() => readSession());
  const [view, setView] = useState<View>({ kind: 'list' });

  const client = useMemo(
    () =>
      new PlatformApiClient({
        baseUrl: apiBaseUrl,
        session,
        onSessionChanged: next => setSession(next)
      }),
    // eslint-disable-next-line react-hooks/exhaustive-deps
    [apiBaseUrl]
  );

  useEffect(() => {
    if (session === null && view.kind !== 'list') {
      setView({ kind: 'list' });
    }
  }, [session, view.kind]);

  if (session === null) {
    return <SignIn client={client} onSignedIn={() => setSession(client.getSession())} />;
  }

  return (
    <>
      <header className="app-header">
        <div className="app-title">AFK4 Control Plane</div>
        <div className="app-session">
          <button type="button" className="link" onClick={() => setView({ kind: 'list' })}>Tenants</button>
          <span className="muted">{session.displayName} ({session.userName})</span>
          <button type="button" onClick={() => void client.signOut()}>Sign out</button>
        </div>
      </header>
      <main>
        {view.kind === 'list' && (
          <TenantList
            client={client}
            onOpenTenant={id => setView({ kind: 'detail', organizationId: id, initialInvite: null })}
            onCreateTenant={() => setView({ kind: 'new' })}
          />
        )}
        {view.kind === 'new' && (
          <NewTenant
            client={client}
            onCreated={response =>
              setView({
                kind: 'detail',
                organizationId: response.tenant.organizationId,
                initialInvite: response.ownerInvite
              })
            }
            onCancel={() => setView({ kind: 'list' })}
          />
        )}
        {view.kind === 'detail' && (
          <TenantDetailView
            client={client}
            organizationId={view.organizationId}
            initialInvite={view.initialInvite}
            onBack={() => setView({ kind: 'list' })}
          />
        )}
      </main>
    </>
  );
}
