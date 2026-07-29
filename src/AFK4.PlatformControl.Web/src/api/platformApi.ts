import { isAccessTokenExpired, type PlatformAdminSession } from '../auth/tokenStore';
import { PlatformTransport, type PlatformTransportOptions } from './platformTransport';
import { OrganizationsApi } from './platformClients/organizations';
import { OrganizationOwnerInvitesApi } from './platformClients/organizationOwnerInvites';
import { SupportNotesApi } from './platformClients/supportNotes';
import { PlansApi } from './platformClients/plans';
import { SubscriptionsApi } from './platformClients/subscriptions';
import { InvoicesApi } from './platformClients/invoices';
import { UpdatesApi } from './platformClients/updates';

export { PlatformApiError } from './platformTransport';

export type PlatformApiClientOptions = PlatformTransportOptions;

/**
 * Aggregates the platform-admin API as domain-scoped sub-clients over one
 * shared transport. Auth/session lifecycle stays on the facade; data access
 * goes through `client.organizations`, `client.invoices`, etc.
 */
export class PlatformApiClient {
  private readonly transport: PlatformTransport;

  public readonly organizations: OrganizationsApi;
  public readonly organizationOwnerInvites: OrganizationOwnerInvitesApi;
  public readonly supportNotes: SupportNotesApi;
  public readonly plans: PlansApi;
  public readonly subscriptions: SubscriptionsApi;
  public readonly invoices: InvoicesApi;
  public readonly updates: UpdatesApi;

  public constructor(options: PlatformApiClientOptions) {
    this.transport = new PlatformTransport(options);
    this.organizations = new OrganizationsApi(this.transport);
    this.organizationOwnerInvites = new OrganizationOwnerInvitesApi(this.transport);
    this.supportNotes = new SupportNotesApi(this.transport);
    this.plans = new PlansApi(this.transport);
    this.subscriptions = new SubscriptionsApi(this.transport);
    this.invoices = new InvoicesApi(this.transport);
    this.updates = new UpdatesApi(this.transport);
  }

  public getSession(): PlatformAdminSession | null {
    return this.transport.getSession();
  }

  public signIn(userName: string, password: string): Promise<PlatformAdminSession> {
    return this.transport.signIn(userName, password);
  }

  public signOut(): Promise<void> {
    return this.transport.signOut();
  }
}

export function isExpiredOrMissing(session: PlatformAdminSession | null, now: Date = new Date()): boolean {
  if (session === null) {
    return true;
  }
  return isAccessTokenExpired(session, now);
}
