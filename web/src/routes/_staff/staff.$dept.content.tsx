import { Outlet, createFileRoute, redirect } from '@tanstack/react-router';

import { reachableDepartments } from '../../shared/api/bootstrap';
import { deptParam } from '../../shared/api/department';

/**
 * The layout of everything under `/staff/<dept>/content`: it owns the department in the address and
 * the guard on it, and draws whichever child matched -- the list at `/`, the editor at `/{id}`.
 *
 * See the links layout next to it for why this is a layout and not the list itself: a detail route
 * that is a child of a component with no `Outlet` never renders, and the address bar says otherwise.
 */
export const Route = createFileRoute('/_staff/staff/$dept/content')({
  params: {
    parse: ({ dept }) => ({ dept: deptParam.parse(dept) }),
    stringify: ({ dept }) => ({ dept: deptParam.format(dept) }),
  },
  beforeLoad: ({ context, params }) => {
    if (!reachableDepartments(context.bootstrap).includes(params.dept)) {
      throw redirect({ to: '/forbidden' });
    }
  },
  component: Outlet,
});
