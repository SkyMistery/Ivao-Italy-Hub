import { screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { afterEach, expect, test, vi } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';
import { renderWithProviders } from '../../test/harness';

import { OpenDraftPreview } from './OpenDraftPreview';

/**
 * «Open the preview» (design of phase 3, §4.7): what is on screen is stored first, and the tab is the browser's to allow only
 * during the press — so it is opened on the press, and sent to the draft once the draft is stored.
 */

const notice = vi.hoisted(() => vi.fn());

vi.mock('../../shared/ui', async (original) => ({
  ...(await original<Record<string, unknown>>()),
  useNotice: () => notice,
}));

vi.mock('@tanstack/react-router', () => ({
  useRouter: () => ({
    buildLocation: ({ params, search }: { params: { id: string }; search: { lang: string } }) => ({
      href: `/staff/content/${params.id}/preview?lang=${search.lang}`,
    }),
  }),
}));

const words = englishCommon.content.editor.openPreview;

/** A tab as `window.open` hands it back: where it was sent, and whether it was closed. */
function aTab() {
  return { opener: {} as unknown, location: { href: 'about:blank' }, close: vi.fn() };
}

afterEach(() => {
  vi.restoreAllMocks();
  notice.mockReset();
});

test('the tab opens on the press, and goes to the draft only once the draft is stored', async () => {
  const user = userEvent.setup();
  const tab = aTab();
  const open = vi.spyOn(window, 'open').mockReturnValue(tab as unknown as Window);

  let store: (stored: boolean) => void = () => {};
  const flush = vi.fn(() => new Promise<boolean>((resolve) => (store = resolve)));

  renderWithProviders(<OpenDraftPreview id={7} locale="it" flush={flush} disabled={false} />);

  await user.click(screen.getByRole('button', { name: words.label }));

  // Opened during the gesture, before anything was awaited, and cut off from the editor.
  expect(open).toHaveBeenCalledWith('about:blank', '_blank');
  expect(flush).toHaveBeenCalledTimes(1);
  expect(tab.opener).toBeNull();
  expect(tab.location.href).toBe('about:blank');

  store(true);

  await vi.waitFor(() => expect(tab.location.href).toBe('/staff/content/7/preview?lang=it'));
  expect(tab.close).not.toHaveBeenCalled();
});

test('a draft that could not be stored closes the tab and says so', async () => {
  const user = userEvent.setup();
  const tab = aTab();
  vi.spyOn(window, 'open').mockReturnValue(tab as unknown as Window);

  renderWithProviders(
    <OpenDraftPreview id={7} locale="en" flush={() => Promise.resolve(false)} disabled={false} />,
  );

  await user.click(screen.getByRole('button', { name: words.label }));

  await vi.waitFor(() => expect(tab.close).toHaveBeenCalledTimes(1));
  expect(tab.location.href).toBe('about:blank');
  expect(notice).toHaveBeenCalledWith({ tone: 'error', title: words.notSaved });
});

test('a tab the browser blocked is said, and nothing is stored for it', async () => {
  const user = userEvent.setup();
  vi.spyOn(window, 'open').mockReturnValue(null);
  const flush = vi.fn(() => Promise.resolve(true));

  renderWithProviders(<OpenDraftPreview id={7} locale="en" flush={flush} disabled={false} />);

  await user.click(screen.getByRole('button', { name: words.label }));

  expect(flush).not.toHaveBeenCalled();
  expect(notice).toHaveBeenCalledWith({ tone: 'error', title: words.blocked });
});
