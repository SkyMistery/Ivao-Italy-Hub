import { fireEvent, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test, vi } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';
import { renderWithProviders } from '../../test/harness';

import { SearchPalette } from './SearchPalette';

/**
 * The shortcut, and what the box offers when it opens (design M1 §7).
 *
 * The screens come from `staffDestinations`, which the sidebar draws from too — so what is asserted
 * here is that the palette offers the same places, not that it has a list of its own.
 */

const api = vi.hoisted(() => ({ get: vi.fn() }));

vi.mock('../../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../../shared/api/client')),
  api: { GET: api.get },
}));

vi.mock('@tanstack/react-router', () => ({
  useNavigate: () => vi.fn(),
}));

const bootstrap = {
  user: {
    vid: 111111,
    firstName: 'Test',
    lastName: 'Coordinator',
    positions: ['XX-EC'],
    isStaff: true,
    isSuperadmin: false,
    hasAllDepartments: false,
    locale: 'en',
    departments: ['ED'],
    firs: [],
  },
  permissions: [{ name: 'Links.View', department: 'ED' }],
  division: {
    code: 'XX',
    name: { en: 'IVAO Example' },
    locales: ['en'],
    defaultLocale: 'en',
    timezone: 'UTC',
    logoUrl: null,
    faviconUrl: null,
    firStaffScope: 'all',
    siteDepartment: 'WD',
  },
  modules: [],
  navigation: { public: [], footer: [], staff: [] },
  registries: { blocks: [], widgets: [], permissions: [] },
  calendarKinds: [],
  version: '0.0.0-test',
} as never;

test('control and K opens the palette, and it offers the screens of the back office', async () => {
  api.get.mockResolvedValue({
    data: { results: { items: [], page: 1, pageSize: 20, total: 0 }, notice: null },
  });

  renderWithProviders(<SearchPalette bootstrap={bootstrap} />);

  // Closed until asked for: a palette that is always on screen is a panel.
  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

  fireEvent.keyDown(document, { key: 'k', ctrlKey: true });

  const palette = await screen.findByRole('dialog');
  expect(palette).toBeInTheDocument();

  // The screens this member may reach — the ones the sidebar draws, read from the same list.
  expect(await screen.findByText(`ED — ${englishCommon.links.title}`)).toBeInTheDocument();
});

test('the box opens the same palette, so the shortcut is not the only way in', async () => {
  // Asked for by Carmine after the demo. ⌘K is invisible: a back office whose search you have to
  // be told about is a search most of the staff never use. What matters is that the box is a way
  // **in** and not a second search — one palette, one list of results.
  api.get.mockResolvedValue({
    data: { results: { items: [], page: 1, pageSize: 20, total: 0 }, notice: null },
  });

  const user = userEvent.setup();
  renderWithProviders(<SearchPalette bootstrap={bootstrap} />);

  expect(screen.queryByRole('dialog')).not.toBeInTheDocument();

  await user.click(
    screen.getByRole('button', { name: new RegExp(englishCommon.search.palette.placeholder) }),
  );

  const palette = await screen.findByRole('dialog');
  expect(palette).toBeInTheDocument();

  // And it is the same one: the screens of the back office are offered, not an empty box.
  expect(await screen.findByText(`ED — ${englishCommon.links.title}`)).toBeInTheDocument();
});

test('the same shortcut closes it again', async () => {
  api.get.mockResolvedValue({
    data: { results: { items: [], page: 1, pageSize: 20, total: 0 }, notice: null },
  });

  renderWithProviders(<SearchPalette bootstrap={bootstrap} />);

  fireEvent.keyDown(document, { key: 'K', ctrlKey: true });
  expect(await screen.findByRole('dialog')).toBeInTheDocument();

  // Upper case on purpose: with caps lock on, the key is still the same key.
  fireEvent.keyDown(document, { key: 'K', ctrlKey: true });
  await waitFor(() => expect(screen.queryByRole('dialog')).not.toBeInTheDocument());
});
