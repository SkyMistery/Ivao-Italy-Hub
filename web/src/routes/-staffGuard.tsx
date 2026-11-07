import { redirect } from '@tanstack/react-router';

import type { Bootstrap } from '../shared/api/bootstrap';
import { loginHref } from '../shared/api/client';

/**
 * The guard of the staff (recipe 1, design M0 §7.3), said once for the two layouts it stands in front of: the back office
 * and the draft looked at in the frame of the site. Signed out goes to the login by `href`, because `/auth/login` is a
 * Kestrel endpoint (and with `reloadDocument`, as in `_member.tsx`, or the router keeps a path in the application); signed
 * in but not staff goes to `/forbidden`, which is a page of this application and therefore a `to`.
 *
 * Two answers and not one: "you are not signed in" and "this is not for you" are different things, and sending the second
 * to a login screen is how a member ends up in a loop.
 *
 * The file starts with `-` because it lives under `routes/`: the router's plugin ignores what starts with it.
 */
export function requireStaff(bootstrap: Pick<Bootstrap, 'user'>, here: string): void {
  if (!bootstrap.user) {
    throw redirect({ href: loginHref(here), reloadDocument: true });
  }

  if (!bootstrap.user.isStaff && !bootstrap.user.isSuperadmin) {
    throw redirect({ to: '/forbidden' });
  }
}
