import {
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
import { useLayoutEffect, useRef, useState, type ReactNode } from 'react';
import { flushSync } from 'react-dom';
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
 *
 * Since M2 (T7a) it can also **ask before it asks**: removing a leg is a deletion or a retirement,
 * and only the server knows which, so the editor asks it when the dialog opens (`onOpenChange`) and
 * keeps the confirmation off until the answer — and the reason a retirement needs — is there
 * (`confirmDisabled`). Extended again rather than written twice.
 *
 * ⚠️ The answer is often a live form (`SchemaForm` with `onChange`), which the screen keeps and confirms with. The form
 * applies 150 ms after the last keystroke; a press inside that pause applies at once, on the click in capture
 * (`decisions/2026-11-08-l-ultima-battuta.md`). But a click whose listeners run in one go — a click dispatched by a
 * script, every click of a test — reached `onConfirm` before React had drawn what was applied, and the confirmation
 * sent the text before the last keystrokes, or went through with the box just emptied (#428). So the press draws what
 * is pending first and then reads the screen's **latest** `onConfirm` and `confirmDisabled`: an answer that is no
 * longer complete confirms nothing, and the dialog stays open with it.
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
  confirmDisabled = false,
  onOpenChange,
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
  /** Keeps the confirmation off while the answer is not complete yet. */
  confirmDisabled?: boolean;
  /** Told when the dialog opens and closes, for a question asked only once somebody means it. */
  onOpenChange?: (open: boolean) => void;
}) {
  const { t } = useTranslation();
  const [open, setOpen] = useState(false);
  // What the screen says now, read by the press after it has drawn what was pending (above).
  const latest = useRef({ onConfirm, confirmDisabled });

  useLayoutEffect(() => {
    latest.current = { onConfirm, confirmDisabled };
  });

  const changeOpen = (next: boolean) => {
    setOpen(next);
    onOpenChange?.(next);
  };

  const confirm = () => {
    // Draws what the click applied (a keystroke inside the live form's pause), so that `latest` is the screen with it.
    flushSync(() => undefined);
    if (latest.current.confirmDisabled) {
      return;
    }

    changeOpen(false);
    latest.current.onConfirm();
  };

  return (
    <AlertDialogRoot open={open} onOpenChange={changeOpen}>
      {/* No `asChild` on the two wrappers: Atmosphere's own `AlertDialog` hands them a `Button`
          exactly like this, and its Button is not a single element a slot could take over. The
          confirmation has no `AlertDialogAction` around it: that one closes on any click, and the
          dialog closes here only once the answer is confirmed (`confirm` above). */}
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
          <Button type="button" variant={confirmVariant} disabled={confirmDisabled} onClick={confirm}>
            {confirmText}
          </Button>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialogRoot>
  );
}
