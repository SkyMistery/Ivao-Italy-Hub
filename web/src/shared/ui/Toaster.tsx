import * as ToastPrimitive from '@radix-ui/react-toast';
import { X } from 'lucide-react';
import { createContext, useCallback, useContext, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

/**
 * The corner of the screen a confirmation is said in: the provider, the queue and the viewport of the toasts, and the
 * toast itself (#266, note 2026-10-19-il-toast-del-nucleo).
 *
 * ⚠️ Atmosphere's `ToastProvider` draws every toast with its own molecule, whose «X» is labelled with the English
 * "Close" written into the library and takes no other word; its provider and its viewport pass Radix no label either,
 * so a screen reader heard «Notification» before every toast and «Notifications (F8)» for the region, under every
 * interface. Atmosphere exports neither the atoms nor a way to pass the words, and Radix is bundled inside it, so a
 * part of ours cannot sit inside its provider. So this is the same thing written again on Radix's own toast, the
 * version Atmosphere 3.1 bundles, with the classes of Atmosphere's atoms copied as they are — the root and its two
 * variants, the title, the description, the «X» and the viewport — and the five seconds and the swipe to the right of
 * its molecule and provider. What is ours is the three words, said in the language on screen.
 *
 * A screen never mounts or calls this: it says a notice through `useNotice`, which reads the tones. `HubProviders`
 * mounts it once, and the guard in `Toaster.test.tsx` keeps Atmosphere's toast out of every screen.
 */

/** What one toast says, and how it is drawn: the variant of Atmosphere's root and the classes put on top of it. */
export interface ToastNotice {
  title: string;
  description?: string;
  /** Milliseconds. Atmosphere's molecule says five seconds, which is right for a confirmation. */
  duration?: number;
  variant?: 'default' | 'destructive';
  className?: string;
}

/** Atmosphere's `toastVariants`, copied: the classes every toast carries, and the two variants. */
const TOAST_ROOT =
  'group radix-swipe-cancel:translate-x-0 radix-swipe-end:translate-x-radix-toast-swipe-end-x ' +
  'radix-swipe-move:translate-x-radix-toast-swipe-move-x radix-swipe-move:transition-none radix-state-open:animate-in ' +
  'radix-state-closed:animate-out radix-swipe-end:animate-out radix-state-closed:fade-out-80 ' +
  'radix-state-closed:slide-out-to-right-full radix-state-open:slide-in-from-top-full ' +
  'sm:radix-state-open:slide-in-from-bottom-full pointer-events-auto relative flex w-full items-center ' +
  'justify-between gap-2 overflow-hidden rounded-md border p-4 pr-6 shadow-lg transition-all';

const TOAST_VARIANTS: Readonly<Record<'default' | 'destructive', string>> = {
  default: 'bg-background text-foreground border',
  destructive: 'group border-destructive bg-destructive text-destructive-foreground',
};

/** Atmosphere's `ToastViewport`, copied: at the top on a phone, at the bottom right from `sm` up. */
const VIEWPORT =
  'fixed top-0 z-[100] flex max-h-screen w-full list-none flex-col-reverse gap-3 p-4 sm:top-auto sm:right-0 ' +
  'sm:bottom-0 sm:flex-col md:max-w-[420px]';

/** Atmosphere's `ToastClose`, copied: shown on hover and on focus, in red on red for the destructive variant. */
const CLOSE =
  'text-foreground/50 hover:text-foreground group-[.destructive]:text-semantic-red-300 ' +
  'group-[.destructive]:hover:text-semantic-red-50 absolute top-1 right-1 rounded-md p-1 opacity-0 transition-opacity ' +
  'group-hover:opacity-100 focus:opacity-100 focus:ring-1 focus:outline-hidden ' +
  'group-[.destructive]:focus:ring-red-400 group-[.destructive]:focus:ring-offset-red-600';

/** Atmosphere's molecule says a toast for five seconds unless told otherwise. */
const TOAST_DURATION = 5000;

const ToastQueue = createContext<((notice: ToastNotice) => void) | null>(null);

/**
 * The provider, the queue and the viewport, mounted once by `HubProviders`.
 *
 * The queue only grows, as Atmosphere's did: a toast that has closed is unmounted by Radix after its animation, and
 * what is left of it in the queue is a few strings until the page is left.
 */
export function Toaster({ children }: { children: ReactNode }) {
  const { t } = useTranslation();
  const [toasts, setToasts] = useState<readonly (ToastNotice & { id: number })[]>([]);

  const push = useCallback((notice: ToastNotice) => {
    setToasts((queue) => [...queue, { ...notice, id: (queue.at(-1)?.id ?? 0) + 1 }]);
  }, []);

  return (
    <ToastQueue.Provider value={push}>
      <ToastPrimitive.Provider label={t('toast.label')} swipeDirection="right">
        {children}
        {toasts.map(
          ({ id, title, description, duration = TOAST_DURATION, variant = 'default', className }) => (
            <ToastPrimitive.Root
              key={id}
              duration={duration}
              className={[TOAST_ROOT, TOAST_VARIANTS[variant], className].filter(Boolean).join(' ')}
            >
              <div className="grid gap-1">
                <ToastPrimitive.Title className="text-sm font-semibold [&+div]:text-xs">
                  {title}
                </ToastPrimitive.Title>
                {description === undefined ? null : (
                  <ToastPrimitive.Description className="text-sm opacity-90">
                    {description}
                  </ToastPrimitive.Description>
                )}
              </div>
              <ToastPrimitive.Close aria-label={t('common.close')} className={CLOSE}>
                <span aria-hidden>
                  <X className="size-4" />
                </span>
              </ToastPrimitive.Close>
            </ToastPrimitive.Root>
          ),
        )}
        <ToastPrimitive.Viewport label={t('toast.region')} className={VIEWPORT} />
      </ToastPrimitive.Provider>
    </ToastQueue.Provider>
  );
}

/** The queue of the `Toaster` above, for `useNotice` only. */
export function useToastQueue(): (notice: ToastNotice) => void {
  const push = useContext(ToastQueue);

  if (push === null) {
    throw new Error('A notice was said with no Toaster above it: HubProviders mounts one.');
  }

  return push;
}
