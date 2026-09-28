import type { QueryClient } from '@tanstack/react-query';
import { Outlet, createRootRouteWithContext } from '@tanstack/react-router';

import { bootstrapQuery } from '../features/me/queries';
import { useDivisionLanguage } from '../shared/i18n/useDivisionLanguage';
import { NotFound } from '../shared/ui';

/**
 * The root of the route tree, and the only place the bootstrap is loaded.
 *
 * `GET /api/me` answers everything the client needs in order to draw itself — the division, the
 * menus, the languages, the effective permissions — so it is fetched once here with
 * `ensureQueryData` and handed to every route as context. A guard reads `context.bootstrap` and
 * never fetches (design M0 §7.3).
 */
export interface RouterContext {
  queryClient: QueryClient;
}

export const Route = createRootRouteWithContext<RouterContext>()({
  beforeLoad: async ({ context }) => ({
    bootstrap: await context.queryClient.ensureQueryData(bootstrapQuery),
  }),
  component: Root,
  notFoundComponent: () => (
    <div className="bg-body text-foreground min-h-screen px-4 py-16">
      <div className="mx-auto max-w-2xl">
        <NotFound />
      </div>
    </div>
  ),
});

/** The one place that knows the division before any route draws: its languages hold from here down. */
function Root() {
  const { bootstrap } = Route.useRouteContext();
  useDivisionLanguage(bootstrap.division);

  return <Outlet />;
}
