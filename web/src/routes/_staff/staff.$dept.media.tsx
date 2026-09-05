import { Outlet, createFileRoute, redirect } from '@tanstack/react-router';

import { reachableDepartments } from '../../shared/api/bootstrap';
import { deptParam } from '../../shared/api/department';

/**
 * The layout of everything under `/staff/<dept>/media`: it owns the department in the address and
 * the guard on it, and draws whichever child matched — the library at `/`, the metadata of one file
 * at `/{id}`.
 *
 * Recipe of three routes, not two (design M0 §7.3, corrected after the whole form half of the back
 * office turned out to be unreachable): the parse and the guard are written once here, and the
 * search parameters of the list stay on the list instead of following the form around.
 */
export const Route = createFileRoute('/_staff/staff/$dept/media')({
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
