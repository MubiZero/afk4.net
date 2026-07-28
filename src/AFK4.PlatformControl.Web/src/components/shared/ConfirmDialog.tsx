import { useEffect, useState } from 'react';
import { Dialog, DialogContent, DialogTitle, DialogDescription, DialogFooter } from '@/components/ui/dialog';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';

export interface ConfirmDialogProps {
  open: boolean;
  title: string;
  description?: string;
  confirmLabel: string;
  cancelLabel: string;
  reasonLabel?: string;
  destructive?: boolean;
  pending: boolean;
  onConfirm: (reason: string) => void;
  onOpenChange: (open: boolean) => void;
}

export function ConfirmDialog(props: ConfirmDialogProps) {
  const [reason, setReason] = useState('');
  useEffect(() => { if (props.open) setReason(''); }, [props.open]);
  return (
    <Dialog open={props.open} onOpenChange={props.onOpenChange}>
      <DialogContent>
        <DialogTitle>{props.title}</DialogTitle>
        {props.description && <DialogDescription>{props.description}</DialogDescription>}
        {props.reasonLabel && (
          <label className="mt-3 block text-sm">
            <span className="mb-1 block text-muted-foreground">{props.reasonLabel}</span>
            <Input aria-label={props.reasonLabel} value={reason} onChange={e => setReason(e.target.value)} />
          </label>
        )}
        <DialogFooter>
          <Button variant="outline" disabled={props.pending} onClick={() => props.onOpenChange(false)}>
            {props.cancelLabel}
          </Button>
          <Button
            variant={props.destructive ? 'destructive' : 'default'}
            disabled={props.pending}
            onClick={() => props.onConfirm(reason)}
          >
            {props.confirmLabel}
          </Button>
        </DialogFooter>
      </DialogContent>
    </Dialog>
  );
}
