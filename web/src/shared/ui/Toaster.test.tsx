import { readdirSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';

import { Button } from '@ivao/atmosphere-react';
import { QueryClient } from '@tanstack/react-query';
import { act, fireEvent, render, screen, within } from '@testing-library/react';
import { afterEach, expect, test, vi } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';
import italianCommon from '../../../../locales/it/common.json';
import { HubProviders } from '../../app/Providers';
import { createTestI18n } from '../../test/harness';

import { useNotice, type NoticeTone } from './notices';

/**
 * The core's toast (#266, note 2026-10-19-il-toast-del-nucleo): Atmosphere's, written again on Radix so that its three
 * words — the «X», the word that comes before every toast a screen reader announces, and the name of the corner they
 * appear in — are said in the language on screen.
 *
 * It mounts `HubProviders`, as `Notice.test.tsx` does: the toast is something the tree has, not a component a screen
 * mounts, and a test with a provider of its own would pass while the application had another.
 */

afterEach(() => {
  vi.useRealTimers();
});

async function mount(language: 'en' | 'it') {
  const i18n = createTestI18n();
  await i18n.changeLanguage(language);

  function Screen() {
    const notice = useNotice();
    const tones: readonly NoticeTone[] = ['error', 'warning', 'success', 'info'];

    return (
      <>
        {tones.map((tone) => (
          <Button key={tone} type="button" onClick={() => notice({ tone, title: `A ${tone} toast` })}>
            {tone}
          </Button>
        ))}
      </>
    );
  }

  render(
    <HubProviders i18n={i18n} queryClient={new QueryClient()}>
      <Screen />
    </HubProviders>,
  );
}

/** Raises one toast of the tone and returns its root, the `li` Radix draws. */
function raise(tone: NoticeTone): HTMLElement {
  fireEvent.click(screen.getByRole('button', { name: tone }));
  const root = screen.getByText(`A ${tone} toast`).closest('li');
  expect(root).not.toBeNull();
  return root!;
}

test('in Italian the «X», the announcement and the corner of the toasts are said in Italian', async () => {
  await mount('it');
  const toast = raise('success');

  expect(within(toast).getByRole('button', { name: 'Chiudi' })).toHaveAttribute(
    'aria-label',
    italianCommon.common.close,
  );
  expect(screen.getByRole('region', { name: 'Notifiche (F8)' })).toHaveAttribute(
    'aria-label',
    italianCommon.toast.region,
  );
  // Radix announces a toast in a live region of its own, a frame after it opens, its words after the provider's label.
  expect(await screen.findByText('Notifica A success toast')).toBeInTheDocument();

  // And nothing of the three is left in English.
  expect(within(toast).queryByRole('button', { name: /close/i })).not.toBeInTheDocument();
  expect(screen.queryByRole('region', { name: /notifications/i })).not.toBeInTheDocument();
  expect(screen.queryByText(/^Notification /)).not.toBeInTheDocument();
});

test('in English they are the words Atmosphere said, now taken from the language file', async () => {
  await mount('en');
  const toast = raise('success');

  expect(englishCommon.common.close).toBe('Close');
  expect(englishCommon.toast).toEqual({ label: 'Notification', region: 'Notifications (F8)' });

  expect(within(toast).getByRole('button', { name: 'Close' })).toBeInTheDocument();
  expect(screen.getByRole('region', { name: 'Notifications (F8)' })).toBeInTheDocument();
  expect(await screen.findByText('Notification A success toast')).toBeInTheDocument();
});

test('the «X» closes the toast', async () => {
  await mount('en');
  const toast = raise('info');

  fireEvent.click(within(toast).getByRole('button', { name: 'Close' }));

  expect(screen.queryByText('A info toast')).not.toBeInTheDocument();
});

test('a toast stays five seconds and goes, as Atmosphere’s did', async () => {
  await mount('en');
  vi.useFakeTimers();

  raise('success');

  act(() => {
    vi.advanceTimersByTime(4999);
  });
  expect(screen.getByText('A success toast')).toBeInTheDocument();

  act(() => {
    vi.advanceTimersByTime(1);
  });
  expect(screen.queryByText('A success toast')).not.toBeInTheDocument();
});

test('a toast keeps Atmosphere’s classes, the swipe to the right and the tones', async () => {
  await mount('en');

  const error = raise('error');
  const warning = raise('warning');
  const success = raise('success');
  const info = raise('info');

  // The swipe: to the right, and the classes that move the toast with the finger.
  for (const toast of [error, warning, success, info]) {
    expect(toast).toHaveAttribute('data-swipe-direction', 'right');
    expect(toast).toHaveClass(
      'group',
      'radix-swipe-end:translate-x-radix-toast-swipe-end-x',
      'radix-swipe-move:translate-x-radix-toast-swipe-move-x',
      'rounded-md',
      'border',
      'shadow-lg',
    );
  }

  // A refusal is Atmosphere's destructive variant, a filled red; the other three its default with the tone's border.
  expect(error).toHaveClass('border-destructive', 'bg-destructive', 'text-destructive-foreground');
  expect(error).not.toHaveClass('bg-background');

  for (const [toast, colour] of [
    [warning, 'yellow'],
    [success, 'green'],
    [info, 'blue'],
  ] as const) {
    expect(toast).toHaveClass('bg-background', 'text-foreground', `border-semantic-${colour}-600`);
    expect(toast).not.toHaveClass('bg-destructive');
  }

  // The corner they appear in is Atmosphere's: at the top on a phone, at the bottom right from `sm` up.
  expect(screen.getByRole('region', { name: 'Notifications (F8)' }).querySelector('ol')).toHaveClass(
    'fixed',
    'z-[100]',
    'sm:bottom-0',
    'sm:right-0',
    'md:max-w-[420px]',
  );
});

/** Atmosphere's toast: its provider, which draws the «X» named "Close" in the library, and the two ways into it. */
const ATMOSPHERE_TOAST = ['Toast', 'ToastProvider', 'useToast'];

test('no screen uses a toast of Atmosphere’s', () => {
  const root = resolve(process.cwd(), 'src');
  const importsFromAtmosphere = /import\s*\{([^}]*)\}\s*from\s*'@ivao\/atmosphere-react'/g;

  const offenders = readdirSync(root, { withFileTypes: true, recursive: true })
    .filter((entry) => entry.isFile() && /\.tsx?$/.test(entry.name))
    .map((entry) => resolve(entry.parentPath, entry.name))
    .filter((file) =>
      [...readFileSync(file, 'utf8').matchAll(importsFromAtmosphere)].some((match) =>
        (match[1] ?? '')
          .split(',')
          // `type Toast` and `useToast as Something` are still Atmosphere's.
          .map((name) =>
            name
              .trim()
              .replace(/^type\s+/, '')
              .replace(/\s+as\s+\w+$/, ''),
          )
          .some((name) => ATMOSPHERE_TOAST.includes(name)),
      ),
    )
    .map((file) =>
      file
        .slice(root.length + 1)
        .split('\\')
        .join('/'),
    );

  expect(offenders).toEqual([]);

  // Nor a toast of its own straight from Radix: the core's is the one, and a second would say "Close" again.
  const fromRadix = readdirSync(root, { withFileTypes: true, recursive: true })
    .filter((entry) => entry.isFile() && /\.tsx?$/.test(entry.name) && !/\.test\.tsx?$/.test(entry.name))
    .map((entry) => resolve(entry.parentPath, entry.name))
    .filter((file) => readFileSync(file, 'utf8').includes("from '@radix-ui/react-toast'"))
    .map((file) =>
      file
        .slice(root.length + 1)
        .split('\\')
        .join('/'),
    );

  expect(fromRadix).toEqual(['shared/ui/Toaster.tsx']);
});
