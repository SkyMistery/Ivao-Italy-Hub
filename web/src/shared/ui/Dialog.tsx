import {
  Button,
  DialogClose,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogRoot,
  DialogTitle,
  DialogTrigger,
} from '@ivao/atmosphere-react';
import { X } from 'lucide-react';
import type { ComponentProps, ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

/**
 * A dialog that shows something and is closed by its «X»: Atmosphere's `Dialog`, with the one word it
 * would not let us change (#224, note 2026-10-11-il-pulsante-di-chiusura-delle-finestre).
 *
 * ⚠️ Atmosphere's draws its «X» with `DialogTopRightClose`, whose screen-reader label is the English
 * "Close" written into the library, under every interface, and which takes no other word. So this is
 * the same molecule put back together from the same parts — the root, the trigger, the content, the
 * header, the title, the description, the footer, and Radix's own `DialogClose` that Atmosphere exports
 * — with the same props, the same classes and the same order, and with a close button of its own that
 * says `common.close` in the language on screen. What is ours is the word, and nothing else, exactly as
 * `DataList` does for the pages of a list.
 *
 * It is not `ConfirmDialog`, which asks a question and has no «X»: that one is an alert dialog, closed
 * by its two buttons.
 */
export function Dialog({
  trigger,
  title,
  description,
  children,
  footer,
  closeButton = true,
  ...root
}: Omit<ComponentProps<typeof DialogRoot>, 'children'> & {
  /** What opens it: a string is drawn as an outline button, anything else is the trigger itself. */
  trigger?: ReactNode;
  title: string;
  description?: string;
  children?: ReactNode;
  footer?: ReactNode;
  /** The «X» at the top right, on unless a dialog closes some other way only. */
  closeButton?: boolean;
}) {
  const { t } = useTranslation();

  return (
    <DialogRoot {...root}>
      {trigger === undefined ? null : (
        <DialogTrigger asChild>
          {typeof trigger === 'string' ? <Button variant="outline">{trigger}</Button> : trigger}
        </DialogTrigger>
      )}
      <DialogContent className="sm:max-w-md">
        <DialogHeader>
          <DialogTitle>{title}</DialogTitle>
          {description === undefined ? null : <DialogDescription>{description}</DialogDescription>}
        </DialogHeader>
        {children}
        {closeButton ? (
          // The classes of Atmosphere's `DialogTopRightClose`, so that the «X» sits and looks where it always did.
          <DialogClose
            aria-label={t('common.close')}
            className="ring-offset-background focus:ring-ring radix-state-open:bg-accent radix-state-open:text-muted-foreground absolute top-4 right-4 rounded-sm opacity-70 transition-opacity hover:opacity-100 focus:ring-2 focus:ring-offset-2 focus:outline-hidden disabled:pointer-events-none"
          >
            <X aria-hidden className="size-4" />
          </DialogClose>
        ) : null}
        {footer === undefined ? null : <DialogFooter>{footer}</DialogFooter>}
      </DialogContent>
    </DialogRoot>
  );
}
