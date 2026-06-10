import { ApiTransport, type ApiTransportOptions } from './apiTransport';
import { AuditApi } from './clients/audit';
import { BillingApi } from './clients/billing';
import { BranchApi } from './clients/branches';
import { CatalogApi } from './clients/catalog';
import { OwnerCodeApi } from './clients/ownerCode';
import { PackagesApi } from './clients/packages';
import { PlayersApi } from './clients/players';
import { ProfileApi } from './clients/profile';
import { ReportsApi } from './clients/reports';
import { StaffApi } from './clients/staff';
import { TariffsApi } from './clients/tariffs';
import { VenueApi } from './clients/venue';
import type { StaffSession } from '../auth/staffTokenStore';

export type ClubApiClientOptions = ApiTransportOptions;

/**
 * Aggregates the staff-facing API as domain-scoped sub-clients over one shared
 * transport. Consumers depend on the sub-client they need (`client.venue`,
 * `client.tariffs`, ...) rather than the whole surface.
 */
export class ClubApiClient {
  private readonly transport: ApiTransport;

  public readonly ownerCode: OwnerCodeApi;
  public readonly profile: ProfileApi;
  public readonly branches: BranchApi;
  public readonly billing: BillingApi;
  public readonly venue: VenueApi;
  public readonly reports: ReportsApi;
  public readonly audit: AuditApi;
  public readonly staff: StaffApi;
  public readonly tariffs: TariffsApi;
  public readonly catalog: CatalogApi;
  public readonly packages: PackagesApi;
  public readonly players: PlayersApi;

  public constructor(options: ClubApiClientOptions) {
    this.transport = new ApiTransport(options);
    this.ownerCode = new OwnerCodeApi(this.transport);
    this.profile = new ProfileApi(this.transport);
    this.branches = new BranchApi(this.transport);
    this.billing = new BillingApi(this.transport);
    this.venue = new VenueApi(this.transport);
    this.reports = new ReportsApi(this.transport);
    this.audit = new AuditApi(this.transport);
    this.staff = new StaffApi(this.transport);
    this.tariffs = new TariffsApi(this.transport);
    this.catalog = new CatalogApi(this.transport);
    this.packages = new PackagesApi(this.transport);
    this.players = new PlayersApi(this.transport);
  }

  public getSession(): StaffSession | null {
    return this.transport.getSession();
  }
}
