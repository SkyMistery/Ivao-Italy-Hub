import { useToast } from '@ivao/atmosphere-react';
import { CircleAlert, CircleCheck, Info, TriangleAlert } from 'lucide-react';
import type { ComponentType } from 'react';

/**
 * The four tones a notice is said in, and the confirmation that says one in the corner of the
 * screen and then goes.
 *
 * Two of the four are Atmosphere's own alert variants, used as they are. The other two are written
 * here in the same shape, because the theme has the colour scales — `semantic-yellow` and
 * `semantic-blue`, beside the green and the red the variants use — and the component simply has no
 * variant for them. This is the "wrap it in `shared/ui`, do not fork it" case the plan describes.
 *
 * It sits beside `Notice.tsx` rather than inside it so that the panel and the toast read the same
 * table: a confirmation that was green in one place and grey in the other would be two components
 * wearing one name.
 */

export type NoticeTone = 'error' | 'warning' | 'success' | 'info';

interface Tone {
  /** Atmosphere's own variant when it has one, and the neutral one when the colour is ours. */
  readonly variant: 'default' | 'success' | 'destructive';
  /** What is added on top of the variant. Empty for the two Atmosphere already draws. */
  readonly className: string;
  readonly Icon: ComponentType<{ className?: string }>;
  /** A refusal interrupts; the other three are read when the reader gets to them. */
  readonly role: 'alert' | 'status';
}

export const NOTICE_TONES: Readonly<Record<NoticeTone, Tone>> = {
  error: { variant: 'destructive', className: '', Icon: CircleAlert, role: 'alert' },
  success: { variant: 'success', className: '', Icon: CircleCheck, role: 'status' },
  warning: {
    variant: 'default',
    className:
      'border-semantic-yellow-600 bg-semantic-yellow-50 text-semantic-yellow-800 ' +
      '[&>svg]:text-semantic-yellow-600 dark:border-semantic-yellow-500 dark:bg-semantic-yellow-900 ' +
      'dark:text-semantic-yellow-100 dark:[&>h5]:text-semantic-yellow-100',
    Icon: TriangleAlert,
    role: 'status',
  },
  info: {
    variant: 'default',
    className:
      'border-semantic-blue-600 bg-semantic-blue-50 text-semantic-blue-800 ' +
      '[&>svg]:text-semantic-blue-600 dark:border-semantic-blue-500 dark:bg-semantic-blue-900 ' +
      'dark:text-semantic-blue-100 dark:[&>h5]:text-semantic-blue-100',
    Icon: Info,
    role: 'status',
  },
};

/**
 * The same four tones, said in the corner of the screen and then gone.
 *
 * A confirmation is not something to close: "saved" has been read by the time the eye gets back to
 * the page, and a box that stays would push the editor down every time somebody pressed save.
 * Carmine chose this shape over a panel at the top of the editor.
 *
 * The toast itself is Atmosphere's — provider, queue, viewport, the swipe to dismiss and the five
 * seconds — so what is added here is only the tone, which its two variants do not carry. A screen
 * therefore never writes a colour: it says which of the four this is, exactly as it would to
 * `Notice`, and the two look alike because they read the same table.
 *
 * ⚠️ `ToastProvider` is mounted in `HubProviders` and nowhere else. A test that mounts a screen
 * calling this without it gets Atmosphere's own error saying so, which is the right failure.
 */
export function useNotice(): (notice: {
  tone: NoticeTone;
  title: string;
  description?: string;
  /** Milliseconds. Atmosphere's default is five seconds, which is right for a confirmation. */
  duration?: number;
}) => void {
  const toast = useToast();

  return ({ tone, title, description, duration }) =>
    toast({
      title,
      ...(description === undefined ? {} : { description }),
      ...(duration === undefined ? {} : { duration }),
      toastProps: {
        // The refusal borrows the toast's own destructive variant, which is a filled red rather
        // than the tinted panel `Notice` draws: a toast is small and a tint on it does not read.
        ...(tone === 'error' ? { variant: 'destructive' as const } : {}),
        ...(tone === 'error' ? {} : { className: TOAST_TONES[tone] }),
      },
    });
}

/** What a toast borrows from the tone. Only a border, because the toast draws its own ground. */
const TOAST_TONES: Readonly<Record<Exclude<NoticeTone, 'error'>, string>> = {
  success: 'border-semantic-green-600 dark:border-semantic-green-500',
  warning: 'border-semantic-yellow-600 dark:border-semantic-yellow-500',
  info: 'border-semantic-blue-600 dark:border-semantic-blue-500',
};
