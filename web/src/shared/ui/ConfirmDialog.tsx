import {
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogRoot,
  AlertDialogTitle,
  AlertDialogTrigger,
  Button,
} from '@ivao/atmosphere-react';
import { useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

/**
 * "Are you sure?" for the one kind of action that deserves the question: the one that cannot be
 * undone. Deleting a link is the first, deleting a grant is the next.
 *
 * It is a component and not a habit so that the wording, the order of the buttons and the colour
 * of the confirmation are the same everywhere — a dialog whose destructive button moves around is
 * a dialog people learn to dismiss without reading.
 *
 * Since G14 it can also carry a **question**: publishing asks what changed on the way, and that is a
 * field between the title and the buttons (`children`).
 * The same dialog, extended rather than a second one written beside it (plan §16.E, rule (b)); and
 * a confirmation that is not destructive says so with `confirmVariant`, because a blue "publish"
 * and a red "delete" must never look alike.
 */
export function ConfirmDialog({
  triggerText,
  triggerVariant = 'ghost',
  title,
  description,
  children,
  confirmText,
  confirmVariant = 'destructive',
  onConfirm,
  disabled = false,
}: {
  triggerText: string;
  triggerVariant?: 'ghost' | 'secondary';
  title: string;
  description?: string;
  /** Fields the answer is given with, drawn between the description and the buttons. */
  children?: ReactNode;
  confirmText: string;
  confirmVariant?: 'destructive' | 'primary';
  onConfirm: () => void;
  disabled?: boolean;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);

  return (
    <AlertDialogRoot open={open} onOpenChange={setOpen}>
      {/* No `asChild` on the three wrappers: Atmosphere's own `AlertDialog` hands them a `Button`
          exactly like this, and its Button is not a single element a slot could take over. */}
      <AlertDialogTrigger>
        <Button type="button" variant={triggerVariant} disabled={disabled}>
          {triggerText}
        </Button>
      </AlertDialogTrigger>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>{title}</AlertDialogTitle>
          {description === undefined ? null : <AlertDialogDescription>{description}</AlertDialogDescription>}
        </AlertDialogHeader>
        {children === undefined ? null : <div className="flex flex-col gap-3">{children}</div>}
        <AlertDialogFooter>
          <AlertDialogCancel>
            <Button type="button" variant="outline">
              {t('common.cancel')}
            </Button>
          </AlertDialogCancel>
          <AlertDialogAction>
            <Button type="button" variant={confirmVariant} onClick={() => onConfirm()}>
              {confirmText}
            </Button>
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialogRoot>
  );
}
