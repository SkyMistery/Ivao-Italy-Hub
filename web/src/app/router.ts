import type { QueryClient } from '@tanstack/react-query';
import { createRoute, createRouter, type RouteComponent } from '@tanstack/react-router';

import { routeTree } from '../routeTree.gen';
import { Route as PublicLayoutRoute } from '../routes/_public';

import { registry } from './registry';

/**
 * The router, with the routes the modules declare mounted into the generated tree.
 *
 * The core's own screens are file based and generated into `routeTree.gen.ts`, which is committed:
 * a build never depends on a generation step having run. A module's screens cannot be, because the
 * generator scans one directory and a module's code lives in `web/src/modules/<key>/` — so they
 * arrive the other way design M0 §6.5 allows, registered from the manifest.
 *
 * They are mounted under `_public`, so a module page has the same header, footer and language
 * switcher as every other public page. A module route is therefore not in `FileRouteTypes` and
 * `<Link to="/atc">` would not typecheck: that is what `RouterAnchor` is for, and it is already the
 * one place in the application that widens a string into a `to`. It is also the honest position —
 * the core cannot have compile time knowledge of a path it is not allowed to know about.
 */
export function createHubRouter(queryClient: QueryClient) {
  const moduleRoutes = registry.routes.map((definition) =>
    createRoute({
      getParentRoute: () => PublicLayoutRoute,
      path: definition.path,
      // The manifest types a screen as a plain `ComponentType`, which is what a module author
      // writes; the router wants its own alias of the same thing.
      component: definition.component as RouteComponent,
    }),
  );

  if (moduleRoutes.length > 0) {
    // `addChildren` replaces the children of the route it is called on and returns that same
    // object, which is the one the generated tree already holds: appending to what is there is how
    // a module route joins the tree without the tree being rebuilt around it.
    const existing = (PublicLayoutRoute.children ?? []) as unknown[];
    PublicLayoutRoute.addChildren([...existing, ...moduleRoutes] as never);
  }

  return createRouter({ routeTree, context: { queryClient } });
}
