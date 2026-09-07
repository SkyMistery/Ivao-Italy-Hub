import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import {
  RouterProvider,
  createMemoryHistory,
  createRootRouteWithContext,
  createRoute,
  createRouter,
} from '@tanstack/react-router';
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { expect, test, vi } from 'vitest';

import type { Bootstrap } from '../../shared/api/bootstrap';

/**
 * Signing out redraws the page, and this is the test that says so.
 *
 * ⚠️ It did not. `useLogout` dropped the cached `/api/me` answer and stopped there — but no screen
 * reads that query: the root route loads the bootstrap once in `beforeLoad` and hands it to every
 * route as **context**, and the header, the sidebar and the guards read that copy. Invalidating a
 * query does not re-run a `beforeLoad`, so the shell kept drawing the name of somebody who had just
 * left until the page was reloaded by hand.
 *
 * Same family as the delete that said nothing and the loader that never refetched
 * (`decisions/2026-09-07-il-loader-non-e-la-riga.md`): the cache was right and what the screen
 * actually reads was not.
 *
 * So the router here is the application's shape and not a convenience — a root that loads the
 * bootstrap with `ensureQueryData`, a screen that reads `useRouteContext`. Take
 * `router.invalidate()` out of `sessionChanged` and this test fails.
 */

const api = vi.hoisted(() => ({ get: vi.fn(), post: vi.fn() }));

vi.mock('../../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../../shared/api/client')),
  api: { GET: api.get, POST: api.post },
}));

const { bootstrapQuery, useLogout } = await import('./queries');

function bootstrap(user: Bootstrap['user']): Bootstrap {
  return {
    user,
    permissions: [],
    division: {
      code: 'XX',
      name: { en: 'IVAO Example' },
      locales: ['en'],
      defaultLocale: 'en',
      timezone: 'UTC',
      firStaffScope: 'all',
      siteDepartment: 'WD',
    },
    modules: [],
    navigation: { public: [], footer: [], staff: [] },
    registries: { blocks: [], widgets: [], permissions: [] },
    version: '0.0.0-test',
  };
}

const member: Bootstrap['user'] = {
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
};

test('signing out redraws the shell as anonymous, without a reload', async () => {
  // The session ends on the server between the two answers, which is exactly what the button does.
  let signedIn = true;

  api.get.mockImplementation(() => ({
    data: bootstrap(signedIn ? member : null),
    response: new Response(null, { status: 200 }),
  }));
  api.post.mockImplementation(() => {
    signedIn = false;
    return { data: undefined, response: new Response(null, { status: 204 }) };
  });

  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });

  // The application's shape: the bootstrap is loaded once at the root and handed down as context.
  const rootRoute = createRootRouteWithContext<{ queryClient: QueryClient }>()({
    beforeLoad: async ({ context }) => ({
      bootstrap: await context.queryClient.ensureQueryData(bootstrapQuery),
    }),
  });

  const indexRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/',
    component: function Screen() {
      // Read off the root and not off this route: a child asking for its own context inside its
      // own component is circular, and TypeScript answers `never` — which is assignable to
      // anything and would check nothing at all.
      const { bootstrap: me } = rootRoute.useRouteContext();
      const logout = useLogout();

      return (
        <>
          <p>{me.user ? `signed in as ${me.user.vid}` : 'anonymous'}</p>
          <button type="button" onClick={() => logout.mutate()}>
            Sign out
          </button>
        </>
      );
    },
  });

  const router = createRouter({
    routeTree: rootRoute.addChildren([indexRoute]),
    history: createMemoryHistory({ initialEntries: ['/'] }),
    context: { queryClient },
  });

  render(
    <QueryClientProvider client={queryClient}>
      <RouterProvider router={router} />
    </QueryClientProvider>,
  );

  expect(await screen.findByText('signed in as 704798')).toBeInTheDocument();

  await userEvent.click(screen.getByRole('button', { name: 'Sign out' }));

  await waitFor(() => expect(screen.getByText('anonymous')).toBeInTheDocument());

  // And it really did ask the server, rather than the screen redrawing on its own.
  expect(api.post).toHaveBeenCalledWith('/auth/logout');

  queryClient.clear();
});
