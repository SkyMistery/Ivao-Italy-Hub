import { createFileRoute, redirect } from '@tanstack/react-router';

import { StaffLayout } from '../app/layouts/StaffLayout';

/**
 * Recipe 1 (design M0 §7.3), copied and not reinvented: signed out goes to the login by `href`,
 * because `/auth/login` is a Kestrel endpoint; signed in but not staff goes to `/forbidden`, which
 * is a page of this application and therefore a `to`.
 *
 * Two answers and not one: "you are not signed in" and "this is not for you" are different things,
 * and sending the second to a login screen is how a member ends up in a loop.
 */
export const Route = createFileRoute('/_staff')({
  beforeLoad: ({ context, location }) => {
    const me = context.bootstrap;

    if (!me.user) {
      throw redirect({ href: `/auth/login?returnUrl=${encodeURIComponent(location.href)}` });
    }

    if (!me.user.isStaff && !me.user.isSuperadmin) {
      throw redirect({ to: '/forbidden' });
    }
  },
  component: StaffRoot,
});

function StaffRoot() {
  const { bootstrap } = Route.useRouteContext();

  return <StaffLayout bootstrap={bootstrap} />;
}
