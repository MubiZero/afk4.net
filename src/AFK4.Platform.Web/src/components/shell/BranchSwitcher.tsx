import { ChevronsUpDown } from 'lucide-react';
import { DropdownMenu, DropdownMenuContent, DropdownMenuItem, DropdownMenuTrigger } from '@/components/ui/dropdown-menu';

export interface BranchOption { branchId: string; name: string; }
export interface BranchSwitcherProps {
  orgName: string;
  branches: BranchOption[];
  activeBranchId: string;
  onSelect: (branchId: string) => void;
}

export function BranchSwitcher({ orgName, branches, activeBranchId, onSelect }: BranchSwitcherProps) {
  const active = branches.find(b => b.branchId === activeBranchId) ?? branches[0];
  return (
    <DropdownMenu>
      <DropdownMenuTrigger className="m-3 flex items-center gap-3 rounded-lg border border-border bg-card px-3 py-2 text-left">
        <span className="flex size-7 items-center justify-center rounded-md bg-primary text-xs font-bold text-primary-foreground">
          {orgName.slice(0, 1)}
        </span>
        <span className="min-w-0">
          <span className="block truncate text-sm font-bold">{orgName}</span>
          <span className="block truncate text-[11px] text-muted">{active?.name ?? '—'}</span>
        </span>
        <ChevronsUpDown className="ml-auto size-4 text-muted" />
      </DropdownMenuTrigger>
      <DropdownMenuContent align="start" className="w-56">
        {branches.map(b => (
          <DropdownMenuItem key={b.branchId} onSelect={() => onSelect(b.branchId)}>
            {b.name}
          </DropdownMenuItem>
        ))}
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
