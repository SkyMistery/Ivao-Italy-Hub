import { Outlet, createFileRoute, redirect } from '@tanstack/react-router';

import { reachableDepartments } from '../../shared/api/bootstrap';
import { deptParam } from '../../shared/api/department';

/**
 * The layout of everything under `/staff/<dept>/links`: it owns the department in the address and
 * the guard on it, and draws whichever child matched — the list at `/`, the form at `/{id}`.
 *
 * It exists as a layout, rather than being the list itself, because of a real failure: the list
 * *was* this route, the form was its child, and nothing rendered an `Outlet`. Clicking "new link"
 * changed the address and left the list on the screen, so the whole form half of the back office
 * was unreachable in a browser while every test stayed green.
 *
 * Keeping the department here rather than on both children is the other half of the point: the
 * parse and the guard are written once and the two screens inherit them, which is also why the
 * search parameters of the list live on the list and no longer follow the form around.
 */
export const Route = createFileRoute('/_staff/staff/$dept/links')({
  params: {
    parse: ({ dept }) => ({ dept: deptParam.parse(dept) }),
    stringify: ({ dept }) => ({ dept: deptParam.format(dept) }),
  },
  // A department in the address bar is not a department the member may work in. Without this the
  // server would simply narrow the list to nothing and a coordinator who mistyped would be looking
  // at an empty table wondering where the links went, instead of being told.
  beforeLoad: ({ context, params }) => {
    if (!reachableDepartments(context.bootstrap).includes(params.dept)) {
      throw redirect({ to: '/forbidden' });
    }
  },
  component: Outlet,
});
