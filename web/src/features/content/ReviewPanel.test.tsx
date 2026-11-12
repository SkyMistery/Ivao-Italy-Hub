import { screen, waitFor, within } from '@testing-library/react';
import userEvent, { type UserEvent } from '@testing-library/user-event';
import { beforeEach, describe, expect, test, vi } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';
import { Toaster } from '../../shared/ui/Toaster';
import { renderWithProviders } from '../../test/harness';

import type { ContentDetailDto } from './queries';

/**
 * The two selects of the approval dialog put back to nothing draw nothing. Radix, without a `value`, goes uncontrolled and
 * draws the last choice it held: the dialog named one parent and the approval moved the page, or its menu entry, to the top
 * (note 2026-11-06-il-pannello-rimesso-a-niente).
 */

const api = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn() }));

vi.mock('../../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../../shared/api/client')),
  api: { GET: api.get, POST: api.post },
}));

const { ReviewPanel } = await import('./ReviewPanel');

const words = englishCommon.content.review;
const NO_PARENT = englishCommon.content.options.parentId.none;
const THE_TOP = words.menu.top;

/** What the tree of pages offers as a parent, and what the menu offers to hang the entry under. */
const ABOUT = '/about — About';
const PILOTS = '/pilots — Pilots';
const SERVICES = 'Services';

/** The version of the row the summary describes, and an older one the tab opened on. */
const SUMMARY_VERSION = '2026-11-10T10:00:00.123456Z';
const OPENED_VERSION = '2026-11-10T09:00:00Z';

const ok = <T,>(data: T) => ({ data, response: new Response(null, { status: 200 }) });

function answers(path: string) {
  switch (path) {
    case '/api/content/{id}/review':
      return ok({
        firstPublication: true,
        titleChanged: false,
        path: '/about/airshow',
        sections: [],
        menu: null,
        readyAt: null,
        readyByName: null,
        rowVersion: SUMMARY_VERSION,
      });
    case '/api/content/pages':
      return ok([
        { id: 1, depth: 1, path: 'about', title: { en: 'About' } },
        { id: 2, depth: 1, path: 'pilots', title: { en: 'Pilots' } },
      ]);
    case '/api/menu':
      return ok({ items: [{ id: 7, parentId: null, path: 'services', label: { en: 'Services' } }] });
    default:
      throw new Error(`Nothing answers ${path}`);
  }
}

function aPageWaiting(parentId: number | null): ContentDetailDto {
  return {
    id: 20,
    slug: 'airshow',
    path: 'about/airshow',
    parentId,
    status: 'Ready',
    reviewNote: null,
    rowVersion: OPENED_VERSION,
  } as ContentDetailDto;
}

async function openTheApproval(user: UserEvent, parentId: number | null = null) {
  renderWithProviders(
    <Toaster>
      <ReviewPanel
        content={aPageWaiting(parentId)}
        locales={['en']}
        mayApprove
        flush={() => Promise.resolve(true)}
        onChanged={() => undefined}
      />
    </Toaster>,
  );

  // The dialog's options come from these two, so they are in before it opens.
  await waitFor(() => {
    expect(api.get).toHaveBeenCalledWith('/api/menu', expect.anything());
  });
  await user.click(screen.getByRole('button', { name: words.approve }));

  return screen.findByRole('alertdialog');
}

const pageParent = () => screen.getByRole('combobox', { name: englishCommon.content.fields.parentId });
const menuParent = () => screen.getByRole('combobox', { name: words.menu.parent });

async function choose(user: UserEvent, select: HTMLElement, name: string) {
  await user.click(select);
  await user.click(await screen.findByRole('option', { name }));
}

/** Approves, and gives back what went to the server. */
async function approve(user: UserEvent, dialog: HTMLElement) {
  await user.click(within(dialog).getByRole('button', { name: words.approveDialog.confirm }));
  await waitFor(() => {
    expect(api.post).toHaveBeenCalled();
  });
  const [, request] = api.post.mock.calls[0] as [string, { body: unknown }];
  return request.body;
}

beforeEach(() => {
  api.get.mockReset();
  api.post.mockReset();
  api.get.mockImplementation((path: string) => Promise.resolve(answers(path)));
  api.post.mockResolvedValue(ok(aPageWaiting(null)));
});

describe('the page parent of the approval', () => {
  test('a page at the top shows what the top is called, as a placeholder', async () => {
    const user = userEvent.setup();
    await openTheApproval(user);

    expect(pageParent()).toHaveTextContent(NO_PARENT);
    expect(pageParent()).toHaveAttribute('data-placeholder');
  });

  test('a page under a parent draws it, and goes with it', async () => {
    const user = userEvent.setup();
    const dialog = await openTheApproval(user, 1);

    await waitFor(() => {
      expect(pageParent()).toHaveTextContent(ABOUT);
    });
    expect(await approve(user, dialog)).toMatchObject({ parentId: 1 });
  });

  test('a page taken from its parent draws the top, and goes to the top', async () => {
    const user = userEvent.setup();
    const dialog = await openTheApproval(user, 1);

    await choose(user, pageParent(), NO_PARENT);

    expect(pageParent()).toHaveTextContent(NO_PARENT);
    expect(pageParent()).not.toHaveTextContent(ABOUT);
    expect(pageParent()).toHaveAttribute('data-placeholder');
    expect(await approve(user, dialog)).toMatchObject({ parentId: null });
  });

  test('a choice taken back draws the top', async () => {
    const user = userEvent.setup();
    await openTheApproval(user);

    await choose(user, pageParent(), PILOTS);
    expect(pageParent()).toHaveTextContent(PILOTS);

    await choose(user, pageParent(), NO_PARENT);

    expect(pageParent()).toHaveTextContent(NO_PARENT);
    expect(pageParent()).toHaveAttribute('data-placeholder');
  });

  test('a page that had a parent, taken away and given back and taken away again, draws the top', async () => {
    const user = userEvent.setup();
    await openTheApproval(user, 1);

    await choose(user, pageParent(), NO_PARENT);
    await choose(user, pageParent(), ABOUT);
    await choose(user, pageParent(), NO_PARENT);

    expect(pageParent()).toHaveTextContent(NO_PARENT);
    expect(pageParent()).not.toHaveTextContent(ABOUT);
  });

  test('after going back to the top the same parent can be chosen again, and the page goes under it', async () => {
    const user = userEvent.setup();
    const dialog = await openTheApproval(user);

    await choose(user, pageParent(), PILOTS);
    await choose(user, pageParent(), NO_PARENT);
    await choose(user, pageParent(), PILOTS);

    expect(pageParent()).toHaveTextContent(PILOTS);
    expect(await approve(user, dialog)).toMatchObject({ parentId: 2 });
  });
});

describe('the menu parent of the approval', () => {
  test('an entry nobody placed shows what the top is called, as a placeholder', async () => {
    const user = userEvent.setup();
    await openTheApproval(user);

    expect(menuParent()).toHaveTextContent(THE_TOP);
    expect(menuParent()).toHaveAttribute('data-placeholder');
  });

  test('an entry taken back to the top draws the top, and goes to the top', async () => {
    const user = userEvent.setup();
    const dialog = await openTheApproval(user);
    await user.type(within(dialog).getByRole('textbox', { name: /^Label/ }), 'Airshow');

    await choose(user, menuParent(), SERVICES);
    expect(menuParent()).toHaveTextContent(SERVICES);

    await choose(user, menuParent(), THE_TOP);

    expect(menuParent()).toHaveTextContent(THE_TOP);
    expect(menuParent()).not.toHaveTextContent(SERVICES);
    expect(menuParent()).toHaveAttribute('data-placeholder');
    expect(await approve(user, dialog)).toMatchObject({ menu: { parentId: null } });
  });

  test('after going back to the top the same parent can be chosen again, and the entry goes under it', async () => {
    const user = userEvent.setup();
    const dialog = await openTheApproval(user);
    await user.type(within(dialog).getByRole('textbox', { name: /^Label/ }), 'Airshow');

    await choose(user, menuParent(), SERVICES);
    await choose(user, menuParent(), THE_TOP);
    await choose(user, menuParent(), SERVICES);

    expect(menuParent()).toHaveTextContent(SERVICES);
    expect(await approve(user, dialog)).toMatchObject({ menu: { parentId: 7 } });
  });
});

describe('the version the approval answers (issue #332, item 2)', () => {
  test('approving answers the version the summary on screen describes', async () => {
    const user = userEvent.setup();
    const dialog = await openTheApproval(user);

    expect(await approve(user, dialog)).toMatchObject({ action: 'Approve', rowVersion: SUMMARY_VERSION });
  });
});
