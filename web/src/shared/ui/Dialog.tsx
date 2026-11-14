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
 *
 * Three sizes. The ordinary one, as wide as a form. `wide`, for a piece of work that needs the room the panel it opens
 * from has not got — the grid of a table (design of the editor's phase 3, §5.1): up to 64 rem, never wider than the
 * window less a margin. And `screen`, for one thing to look at — a picture at full screen (design of the editor's phase
 * 3, §5.3; the gallery's pictures in 3.11): as large as what it shows, up to the window less a margin, with its title
 * said to a screen reader and not drawn above the picture, which is what the dialog is about.
 */
export function Dialog({
  trigger,
  title,
  description,
  children,
  footer,
  closeButton = true,
  size = 'default',
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
  size?: 'default' | 'wide' | 'screen';
}) {
  const { t } = useTranslation();
  const screen = size === 'screen';

  return (
    <DialogRoot {...root}>
      {trigger === undefined ? null : (
        <DialogTrigger asChild>
          {typeof trigger === 'string' ? <Button variant="outline">{trigger}</Button> : trigger}
        </DialogTrigger>
      )}
      <DialogContent
        className={
          screen
            ? // As wide as what it shows (`w-max`), and not `w-fit`: a box fixed at the middle of the window has half the
              // window to fit in, and a picture was drawn at half its size. Room above for the «X», so that it never sits
              // on the corner of the picture. ⚠️ `p-[0.75rem]` and not `p-3`: Atmosphere's stylesheet has its own `.p-3`
              // in the same layer, read after ours, and it took the top back from `pt-12`.
              'w-max max-w-[calc(100vw-2rem)] gap-3 p-[0.75rem] pt-12 sm:max-w-[calc(100vw-2rem)]'
            : size === 'wide'
              ? 'max-w-[calc(100vw-2rem)] sm:max-w-[min(64rem,calc(100vw-2rem))]'
              : 'sm:max-w-md'
        }
      >
        <DialogHeader {...(screen ? { className: 'sr-only' } : {})}>
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
