import { useEffect, useState } from 'react';
import { Dialog } from '@/components/ui/dialog';
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
    <Dialog
      open={props.open}
      title={props.title}
      description={props.description}
      tone={props.destructive === true ? 'danger' : undefined}
      onClose={() => props.onOpenChange(false)}
      footer={
        <>
          <Button variant="outline" disabled={props.pending} onClick={() => props.onOpenChange(false)}>
            {props.cancelLabel}
          </Button>
          <Button
            variant={props.destructive === true ? 'destructive' : 'default'}
            disabled={props.pending || (props.reasonLabel !== undefined && reason.trim().length === 0)}
            onClick={() => props.onConfirm(reason)}
          >
            {props.confirmLabel}
          </Button>
        </>
      }
    >
      {props.reasonLabel !== undefined ? (
        <div className="ui-field">
          <label htmlFor="confirm-reason">{props.reasonLabel}</label>
          <Input id="confirm-reason" aria-label={props.reasonLabel} value={reason} onChange={event => setReason(event.target.value)} />
        </div>
      ) : null}
    </Dialog>
  );
}
