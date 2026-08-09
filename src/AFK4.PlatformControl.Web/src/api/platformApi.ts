import { isAccessTokenExpired, type PlatformAdminSession } from '../auth/tokenStore';
import { PlatformTransport, type PlatformTransportOptions, type SignInOutcome } from './platformTransport';
import { OrganizationsApi } from './platformClients/organizations';
import { OrganizationOwnerInvitesApi } from './platformClients/organizationOwnerInvites';
import { SupportNotesApi } from './platformClients/supportNotes';
import { SupportAccessApi } from './platformClients/supportAccess';
import { PlansApi } from './platformClients/plans';
import { SubscriptionsApi } from './platformClients/subscriptions';
import { InvoicesApi } from './platformClients/invoices';
import { DebtApi } from './platformClients/debt';
import { AnalyticsApi } from './platformClients/analytics';
import { BranchDynamicsApi } from './platformClients/branchDynamics';
import { FeaturesApi } from './platformClients/features';
import { UpdatesApi } from './platformClients/updates';
import { AuditApi } from './platformClients/audit';
import { SearchApi } from './platformClients/search';
import { PulseApi } from './platformClients/pulse';
import { AdminsApi } from './platformClients/admins';
import { TwoFactorApi } from './platformClients/twoFactor';
import { RolesApi } from './platformClients/roles';
import { AnnouncementsApi } from './platformClients/announcements';
import { HealthApi } from './platformClients/health';

export { PlatformApiError, PlatformStaleClientError } from './platformTransport';
export type { SignInOutcome } from './platformTransport';

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
  public readonly supportAccess: SupportAccessApi;
  public readonly plans: PlansApi;
  public readonly subscriptions: SubscriptionsApi;
  public readonly invoices: InvoicesApi;
  public readonly debt: DebtApi;
  public readonly analytics: AnalyticsApi;
  public readonly branchDynamics: BranchDynamicsApi;
  public readonly features: FeaturesApi;
  public readonly updates: UpdatesApi;
  public readonly audit: AuditApi;
  public readonly search: SearchApi;
  public readonly pulse: PulseApi;
  public readonly admins: AdminsApi;
  public readonly twoFactor: TwoFactorApi;
  public readonly roles: RolesApi;

  public readonly announcements: AnnouncementsApi;
  public readonly health: HealthApi;

  public constructor(options: PlatformApiClientOptions) {
    this.transport = new PlatformTransport(options);
    this.organizations = new OrganizationsApi(this.transport);
    this.organizationOwnerInvites = new OrganizationOwnerInvitesApi(this.transport);
    this.supportNotes = new SupportNotesApi(this.transport);
    this.supportAccess = new SupportAccessApi(this.transport);
    this.plans = new PlansApi(this.transport);
    this.subscriptions = new SubscriptionsApi(this.transport);
    this.invoices = new InvoicesApi(this.transport);
    this.debt = new DebtApi(this.transport);
    this.analytics = new AnalyticsApi(this.transport);
    this.branchDynamics = new BranchDynamicsApi(this.transport);
    this.features = new FeaturesApi(this.transport);
    this.updates = new UpdatesApi(this.transport);
    this.audit = new AuditApi(this.transport);
    this.search = new SearchApi(this.transport);
    this.pulse = new PulseApi(this.transport);
    this.admins = new AdminsApi(this.transport);
    this.twoFactor = new TwoFactorApi(this.transport);
    this.roles = new RolesApi(this.transport);
    this.announcements = new AnnouncementsApi(this.transport);
    this.health = new HealthApi(this.transport);
  }

  public getSession(): PlatformAdminSession | null {
    return this.transport.getSession();
  }

  // Step 1 only — see PlatformTransport.signIn. Never returns a working session by itself.
  public signIn(userName: string, password: string): Promise<SignInOutcome> {
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
