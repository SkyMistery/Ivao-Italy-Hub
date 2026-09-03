import { createFileRoute, redirect } from '@tanstack/react-router';

import { reachableDepartments } from '../../shared/api/bootstrap';

/**
 * `/staff` is a door, not a page. It opens on the first department the member may work in, because
 * for a coordinator that is the only one and for a director it is as good a place to start as any.
 * A staff member with no department at all is told so rather than shown an empty shell.
 */
export const Route = createFileRoute('/_staff/staff/')({
  beforeLoad: ({ context }) => {
    const first = reachableDepartments(context.bootstrap)[0];

    if (first === undefined) {
      throw redirect({ to: '/forbidden' });
    }

    // The department travels as the enum the API uses; the route's own `stringify` is what
    // turns it into the lowercase segment of the URL (`shared/api/department.ts`).
    throw redirect({ to: '/staff/$dept/links', params: { dept: first } });
  },
});
