import { QueryClient } from '@tanstack/react-query';
import { RouterProvider, createMemoryHistory, createRootRoute, createRouter } from '@tanstack/react-router';
import { render, screen } from '@testing-library/react';
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
    firStaffScope: 'all',
  },
  modules: [],
  navigation: { public: [{ key: 'nav.home', path: '/' }], staff: [] },
  registries: { blocks: [], widgets: [], permissions: [] },
  version: '0.0.0-test',
};

function renderShell() {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  // A real router, in memory: the header links are TanStack `Link`s and a `Link` outside a router
  // throws. Building one here rather than stubbing it is the whole point -- the tree under test
  // should differ from `main.tsx` in the routes it carries, and in nothing else.
  const rootRoute = createRootRoute({
    component: () => (
      <Shell bootstrap={bootstrap}>
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
  expect(
    screen.getByText(englishCommon.footer.version.replace('{{version}}', '0.0.0-test')),
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
