import { Outlet, createFileRoute, redirect } from '@tanstack/react-router';

import { Shell } from '../app/layouts/Chrome';

/**
 * Recipe 1 (design M0 §7.3): the guard reads the bootstrap the root already loaded and never
 * fetches anything of its own.
 *
 * The redirect is by `href` and not by `to`, because `/auth/login` is a Kestrel endpoint and not a
 * route of this application: it has to be a full navigation, or the router would look for a page
 * that does not exist.
 */
export const Route = createFileRoute('/_member')({
  beforeLoad: ({ context, location }) => {
    if (!context.bootstrap.user) {
      throw redirect({ href: `/auth/login?returnUrl=${encodeURIComponent(location.href)}` });
    }
  },
  component: MemberLayout,
});

function MemberLayout() {
  const { bootstrap } = Route.useRouteContext();

  return (
    <Shell bootstrap={bootstrap}>
      <Outlet />
    </Shell>
  );
}
