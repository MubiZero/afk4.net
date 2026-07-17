import type { OperatorAuthSession } from '../../authClient';
import type {
  DeviceInventoryItemDto,
  PackageOptionDto,
  PosProductDto,
  StaffUserDto,
  TariffOptionDto,
  ZoneDto
} from '../../operatorApiClients';
import type { Feedback, LoadStatus, OperatorBackendContext } from '../../operatorTypes';
import type { ManagementScreenProps } from '../ManagementScreen';

// Shared LoadStatus -> ManagementScreen.state mapping, so every settings-domain destination
// (halls/tariffs/staff/goods) wires loading/error the same way instead of re-deriving it.
export function managementScreenState(loadStatus: LoadStatus | undefined): NonNullable<ManagementScreenProps['state']> {
  if (loadStatus === 'loading') return 'loading';
  if (loadStatus === 'failed') return 'error';
  return 'ready';
}

// Shared prop contract every «Управление» destination wrapper implements. ManagementWorkspace
// mounts exactly one of these at a time and only reads `onDirtyChange` back — the destination
// owns its own load/save state.
//
// The settings-domain fields below are lifted into ManagementWorkspace once (Task 2.1) and
// threaded through to the halls/tariffs/staff/goods wrappers built in Task 2.2-2.6 — they stay
// optional so club/loyalty/news (which load/save their own data independently) can keep
// destructuring only what they use.
export interface DestinationProps {
  backend: OperatorBackendContext | null;
  session: OperatorAuthSession | null;
  currencyCode: string;
  onDirtyChange?: (dirty: boolean) => void;

  zones?: ZoneDto[];
  staffUsers?: StaffUserDto[];
  catalog?: PosProductDto[];
  tariffs?: TariffOptionDto[];
  packageOptions?: PackageOptionDto[];
  deviceInventory?: DeviceInventoryItemDto[];
  loadStatus?: LoadStatus;
  errorDetail?: string; // concrete failure text when loadStatus === 'failed' (settingsFeedback.detail)

  onStaffUsersChange?: (staffUsers: StaffUserDto[]) => void;
  onCatalogChange?: (catalog: PosProductDto[]) => void;
  onDeviceInventoryChange?: (deviceInventory: DeviceInventoryItemDto[]) => void;
  onReload?: (nextBackend?: OperatorBackendContext | null) => Promise<void>;
  onFeedback?: (feedback: Feedback) => void;
  onRetry?: () => void;
}
