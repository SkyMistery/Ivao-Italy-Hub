import type { QueryClient } from '@tanstack/react-query';
import { Outlet, createRootRouteWithContext } from '@tanstack/react-router';

import { createBuildWatch } from '../app/newBuild';
import { bootstrapQuery } from '../features/me/queries';
import { useDivisionLanguage } from '../shared/i18n/useDivisionLanguage';
import { DivisionTitle } from '../shared/seo/PageMetadata';
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

/** Which build served this page, and whether a later bootstrap says the server has moved on. */
const buildWatch = createBuildWatch();

export const Route = createRootRouteWithContext<RouterContext>()({
  beforeLoad: async ({ context, location, preload }) => {
    // A bootstrap older than its `staleTime` is still handed over at once, and asked again behind it:
    // without that, a page that a screen keeps observing would hold the first answer for as long as
    // it stays open, and a delivery would never be noticed. The next navigation reads the fresh one.
    const bootstrap = await context.queryClient.ensureQueryData({
      ...bootstrapQuery,
      revalidateIfStale: true,
    });

    // A delivery happened under this page: the bootstrap is the new server's, the bundle is not.
    // The page loads again at the address it was going to rather than draw a menu it has no screens
    // for (note 2026-09-30-la-pagina-dopo-una-consegna). Here, on a navigation, because a navigation
    // is when the page is changing anyway: nobody is in the middle of a form they are leaving, and a
    // form that guards its changes (`useBlocker`) has been asked before this runs.
    if (!preload && buildWatch.shouldReload(bootstrap)) {
      window.location.assign(location.href);
      // The page is going away: nothing of the old bundle draws in the meantime.
      await new Promise<never>(() => {});
    }

    return { bootstrap };
  },
  component: Root,
  notFoundComponent: () => (
    <div className="bg-body text-foreground min-h-screen px-4 py-16">
      <div className="mx-auto max-w-2xl">
        <NotFound />
      </div>
    </div>
  ),
});

/**
 * The one place that knows the division before any route draws: its languages hold from here down,
 * and so does its name, the title of a tab whose page says none of its own (#224).
 */
function Root() {
  const { bootstrap } = Route.useRouteContext();
  useDivisionLanguage(bootstrap.division);

  return (
    <DivisionTitle division={bootstrap.division}>
      <Outlet />
    </DivisionTitle>
  );
}
