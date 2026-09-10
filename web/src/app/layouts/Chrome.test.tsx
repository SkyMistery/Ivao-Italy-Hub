import { QueryClient } from '@tanstack/react-query';
import { RouterProvider, createMemoryHistory, createRootRoute, createRouter } from '@tanstack/react-router';
import { cleanup, render, screen } from '@testing-library/react';
import i18next from 'i18next';
import { initReactI18next } from 'react-i18next';
import { beforeAll, expect, test } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';
import type { Bootstrap } from '../../shared/api/bootstrap';
import { HubProviders } from '../Providers';

import { Shell } from './Chrome';

/**
 * The frame every screen sits in, mounted under the providers `main.tsx` actually mounts.
 *
 * This exists because of a real failure. `DarkModeToggle` wraps itself in a Radix tooltip, a
 * tooltip without a `TooltipProvider` above it throws rather than degrading, and `main.tsx` did not
 * mount one -- so every screen behind a layout died in the root error boundary while all 74 unit
 * tests stayed green. They stayed green because each of them mounted one component under a harness
 * of its own, and the fault was not in a component: it was in the tree.
 *
 * Hence the shape of this test. It deliberately does **not** use `renderWithProviders`, whose job
 * is to give a single component the least it needs; and it does not list the providers itself
 * either, because a list of its own would pass while the application was missing one. It mounts
 * `HubProviders`, the same component `main.tsx` mounts, so removing a provider from the application
 * fails here. The day an Atmosphere component starts demanding a context nobody mounted, this is
 * what fails first.
 */

const i18n = i18next.createInstance();

beforeAll(async () => {
  await i18n.use(initReactI18next).init({
    lng: 'en',
    fallbackLng: 'en',
    ns: ['common'],
    defaultNS: 'common',
    resources: { en: { common: englishCommon } },
    interpolation: { escapeValue: false },
  });
});

/** An anonymous bootstrap: the hardest case for the header, because it draws the sign in button. */
const bootstrap: Bootstrap = {
  user: null,
  permissions: [],
  division: {
    code: 'XX',
    name: { en: 'IVAO Example' },
    locales: ['en'],
    defaultLocale: 'en',
    timezone: 'UTC',
    logoUrl: null,
    firStaffScope: 'all',
    siteDepartment: 'WD',
  },
  modules: [],
  // Both kinds of entry, because the header has to draw both: a module's, which carries a
  // translation key, and an editorial row, which carries the words themselves (design M1 §8.1).
  navigation: {
    public: [
      { key: 'nav.home', path: '/', label: null, icon: null, children: [] },
      { key: null, path: '/about', label: { en: 'About us' }, icon: null, children: [] },
    ],
    footer: [{ key: null, path: '/legal', label: { en: 'Legal' }, icon: null, children: [] }],
    staff: [],
  },
  registries: { blocks: [], widgets: [], permissions: [] },
  calendarKinds: [],
  version: '0.0.0-test',
};

function renderShell(me: Bootstrap = bootstrap) {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  // A real router, in memory: the header links are TanStack `Link`s and a `Link` outside a router
  // throws. Building one here rather than stubbing it is the whole point -- the tree under test
  // should differ from `main.tsx` in the routes it carries, and in nothing else.
  const rootRoute = createRootRoute({
    component: () => (
      <Shell bootstrap={me}>
        <h1>A screen</h1>
      </Shell>
    ),
  });

  const router = createRouter({
    routeTree: rootRoute,
    history: createMemoryHistory({ initialEntries: ['/'] }),
    context: { queryClient },
  });

  return render(
    <HubProviders i18n={i18n} queryClient={queryClient}>
      <RouterProvider router={router} />
    </HubProviders>,
  );
}

test('the shell of every layout renders under the providers the application mounts', async () => {
  renderShell();

  // The division name, the content, and the footer: the three bands of the frame, so a component
  // that throws anywhere in it takes this test down with it. Awaited because the router resolves
  // its first match after the initial paint.
  expect(await screen.findByRole('heading', { name: 'A screen' })).toBeInTheDocument();
  expect(screen.getAllByText('IVAO Example').length).toBeGreaterThan(0);
  // The release, which is on the line at the bottom together with the copyright since 10 September
  // 2026. Still asserted, and for the reason it always was: the version comes from the bootstrap, so
  // reading it back proves the frame was handed the answer and not merely drawn.
  expect(
    screen.getByText(
      englishCommon.footer.rights
        .replace('{{year}}', String(new Date().getFullYear()))
        .replace('{{division}}', 'IVAO Example')
        .replace('{{version}}', '0.0.0-test'),
    ),
  ).toBeInTheDocument();
});

test('the theme toggle carries our own words, not the ones Atmosphere ships', async () => {
  renderShell();
  await screen.findByRole('heading', { name: 'A screen' });

  // Atmosphere's default is the English "Change theme", and it reaches the user through `title`
  // rather than through `aria-label`: passing only the second leaves an untranslated tooltip that
  // no screenshot review catches, because it appears on hover.
  const toggle = screen.getByRole('button', { name: englishCommon.theme.toggle });
  expect(toggle).toHaveAttribute('title', englishCommon.theme.toggle);
});

/**
 * The way back into the back office, asked for by Carmine on 10 September 2026.
 *
 * ⚠️ The condition is the **route guard's own**, `isStaff || isSuperadmin`, and these three cases
 * exist so it stays that way: a link drawn for somebody the guard would send to `/forbidden` is
 * worse than no link, because it teaches people that the bar lies.
 */
const signedIn = (extra: Partial<NonNullable<Bootstrap['user']>>): Bootstrap => ({
  ...bootstrap,
  user: {
    vid: 704798,
    firstName: 'Test',
    lastName: 'Member',
    positions: [],
    isStaff: false,
    isSuperadmin: false,
    hasAllDepartments: false,
    locale: 'en',
    departments: [],
    firs: [],
    ...extra,
  },
});

test('a member of staff is offered the back office; a visitor and a plain member are not', async () => {
  renderShell();
  await screen.findByRole('heading', { name: 'A screen' });
  expect(screen.queryByRole('link', { name: englishCommon.nav.staff })).not.toBeInTheDocument();

  cleanup();
  renderShell(signedIn({}));
  await screen.findByRole('heading', { name: 'A screen' });
  expect(screen.queryByRole('link', { name: englishCommon.nav.staff })).not.toBeInTheDocument();

  cleanup();
  renderShell(signedIn({ isStaff: true }));
  await screen.findByRole('heading', { name: 'A screen' });
  expect(screen.getByRole('link', { name: englishCommon.nav.staff })).toHaveAttribute('href', '/staff');
});

/**
 * The footer, in columns since 10 September 2026.
 *
 * The three assertions are the three shapes it has to draw, and each is a state the menu table can
 * be in: a heading that leads nowhere, a column whose links all carry a mark, and a lone entry with
 * no column — which is what every footer written before this looked like, and must keep working.
 */
const withFooter: Bootstrap = {
  ...bootstrap,
  navigation: {
    ...bootstrap.navigation,
    footer: [
      {
        key: null,
        path: '',
        label: { en: 'Quick links' },
        icon: null,
        children: [
          { key: null, path: '/news', label: { en: 'News' }, icon: null, children: [] },
          { key: null, path: '/contact', label: { en: 'Contact us' }, icon: null, children: [] },
        ],
      },
      {
        key: null,
        path: '',
        label: { en: 'Follow us' },
        icon: null,
        children: [
          {
            key: null,
            path: 'https://discord.example.org',
            label: { en: 'Discord' },
            icon: 'discord',
            children: [],
          },
        ],
      },
      { key: null, path: '/legal', label: { en: 'Legal' }, icon: null, children: [] },
    ],
  },
};

/**
 * The mark of the division, in the two places Carmine asked for it — and, more importantly, absent
 * from both when a division has none.
 *
 * ⚠️ That second half is the one that matters for a fork: the hub draws whatever `logoUrl` points
 * at and knows nothing about which division it is running for, so a division that leaves the key
 * out must get a bar and a footer that look finished rather than two broken pictures.
 */
test('a division with no mark of its own is drawn without one', async () => {
  renderShell();
  await screen.findByRole('heading', { name: 'A screen' });

  expect(document.querySelectorAll('img')).toHaveLength(0);
});

test('the mark is drawn twice when the division has one, and says nothing to a screen reader', async () => {
  renderShell({
    ...bootstrap,
    division: { ...bootstrap.division, logoUrl: '/branding/division.svg' },
  });

  await screen.findByRole('heading', { name: 'A screen' });

  const marks = [...document.querySelectorAll('img')];

  // Twice: on the bar and in the foot of the page.
  expect(marks).toHaveLength(2);
  expect(marks.every((mark) => mark.getAttribute('src') === '/branding/division.svg')).toBe(true);

  // ⚠️ And decorative in both: the name of the division is written beside it in both places, so an
  // alternative text would say the same thing twice to whoever cannot see the picture. Asserted,
  // because "helpfully" filling this in later is exactly the kind of improvement that makes a page
  // worse for the people it is meant to help.
  expect(marks.every((mark) => mark.getAttribute('alt') === '')).toBe(true);
  expect(screen.queryByRole('img')).not.toBeInTheDocument();
});

test('a footer column is a heading with its links under it, and the heading need not lead anywhere', async () => {
  renderShell(withFooter);
  await screen.findByRole('heading', { name: 'A screen' });

  // The heading is words and not a link, because the entry behind it has no address — which is the
  // whole of what the new column on the menu table buys.
  expect(screen.getByText('Quick links')).toBeInTheDocument();
  expect(screen.queryByRole('link', { name: 'Quick links' })).not.toBeInTheDocument();

  expect(screen.getByRole('link', { name: 'News' })).toHaveAttribute('href', '/news');
  expect(screen.getByRole('link', { name: 'Contact us' })).toHaveAttribute('href', '/contact');
});

test('a column whose links all carry a mark is the row of accounts, drawn as marks', async () => {
  renderShell(withFooter);
  await screen.findByRole('heading', { name: 'A screen' });

  // ⚠️ The words survive as the accessible name even though the eye sees a mark: an icon with no
  // name is a link that a screen reader announces as its own address.
  const discord = screen.getByRole('link', { name: 'Discord' });
  expect(discord).toHaveAttribute('href', 'https://discord.example.org');
  expect(discord).toHaveTextContent('');

  // And it left the columns: it is beside the division's own words, not a fourth column of text.
  expect(screen.queryByText('Follow us')).not.toBeInTheDocument();
});

test('an entry with no column still has its place', async () => {
  renderShell(withFooter);
  await screen.findByRole('heading', { name: 'A screen' });

  // The shape every footer had before this one. A division that upgrades must not lose the links it
  // already wrote just because they were never put in a column.
  expect(screen.getByRole('link', { name: 'Legal' })).toHaveAttribute('href', '/legal');
});

test('a superadmin who holds no staff position is offered it too', async () => {
  // The other half of the guard's condition, and the one easy to drop: the superadmin of a fresh
  // installation has no staff position at all until IVAO says otherwise.
  renderShell(signedIn({ isSuperadmin: true }));
  await screen.findByRole('heading', { name: 'A screen' });

  expect(screen.getByRole('link', { name: englishCommon.nav.staff })).toHaveAttribute('href', '/staff');
});
