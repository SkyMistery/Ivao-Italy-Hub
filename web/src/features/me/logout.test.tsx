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
import type { AnyRouter } from '@tanstack/react-router';
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

const { bootstrapChanged, bootstrapKey, bootstrapQuery, sessionChanged, useLogout } =
  await import('./queries');

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
      dateFormat: 'dd/mm/yyyy',
      logoUrl: null,
      faviconUrl: null,
      firStaffScope: 'all',
      siteDepartment: 'WD',
      contentApproval: ['Page'],
      tagline: {},
      builtBy: [],
    },
    modules: [],
    navigation: { public: [], footer: [], staff: [] },
    registries: { blocks: [], permissions: [] },
    calendarKinds: [],
    version: '0.0.0-test',
    commit: null,
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
  tokenAudiences: [],
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

/** A router as far as these two functions use one. */
function stubRouter(): AnyRouter & { invalidate: ReturnType<typeof vi.fn> } {
  return { invalidate: vi.fn(() => Promise.resolve()) } as unknown as AnyRouter & {
    invalidate: ReturnType<typeof vi.fn>;
  };
}

test('a changed session forgets everything the previous person read (issue #332, item 9)', async () => {
  const queryClient = new QueryClient();
  queryClient.setQueryData(bootstrapKey, bootstrap(member));
  queryClient.setQueryData(['me', 'tokens'], ['a token']);
  queryClient.setQueryData(['blocks', 'my-events'], ['an event']);
  const router = stubRouter();

  await sessionChanged(queryClient, router);

  expect(queryClient.getQueryCache().getAll()).toHaveLength(0);
  expect(router.invalidate).toHaveBeenCalledOnce();
});

test('a refused sign-out leaves the person signed in and their data where it was (issue #332, item 9)', async () => {
  api.post.mockImplementation(() => ({
    data: undefined,
    error: { title: 'refused' },
    response: new Response(null, { status: 500 }),
  }));
  const queryClient = new QueryClient({ defaultOptions: { mutations: { retry: false } } });
  queryClient.setQueryData(['me', 'tokens'], ['a token']);

  const rootRoute = createRootRouteWithContext<{ queryClient: QueryClient }>()({});
  const indexRoute = createRoute({
    getParentRoute: () => rootRoute,
    path: '/',
    component: function Screen() {
      const logout = useLogout();
      return (
        <>
          <p>{logout.isError ? 'refused' : 'waiting'}</p>
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

  await userEvent.click(await screen.findByRole('button', { name: 'Sign out' }));
  expect(await screen.findByText('refused')).toBeInTheDocument();
  expect(queryClient.getQueryData(['me', 'tokens'])).toEqual(['a token']);

  queryClient.clear();
});

test('a write that changes the bootstrap removes it and runs the root again (issue #332, item 3)', async () => {
  const queryClient = new QueryClient();
  queryClient.setQueryData(bootstrapKey, bootstrap(member));
  queryClient.setQueryData(['me', 'tokens'], ['a token']);
  const router = stubRouter();

  await bootstrapChanged(queryClient, router);

  expect(queryClient.getQueryData(bootstrapKey)).toBeUndefined();
  expect(queryClient.getQueryData(['me', 'tokens'])).toEqual(['a token']);
  expect(router.invalidate).toHaveBeenCalledOnce();
});
