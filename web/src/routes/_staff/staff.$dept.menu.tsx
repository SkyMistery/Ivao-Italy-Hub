import { Outlet, createFileRoute, redirect } from '@tanstack/react-router';

import { menuDepartment, reachableDepartments } from '../../shared/api/bootstrap';
import { deptParam } from '../../shared/api/department';

/**
 * The layout of everything under `/staff/<dept>/menu`: it owns the department in the address and
 * the guard on it, and draws whichever child matched — the list at `/`, the form at `/{id}`.
 *
 * Recipe of three routes, not two (design M0 §7.3), like every other resource of the back office.
 * What is different here is the guard: the menu of the site belongs to one department, so this
 * screen exists at one address — `/staff/wd/menu` for a division whose web team is `WD` — and the
 * others are sent away rather than shown a list the server would answer empty.
 *
 * ⚠️ Which department that is comes from `/api/me` and never from a file name: a route file called
 * `staff.wd.menu.tsx` would be a department code written into a client that is not allowed to know
 * one (CLAUDE.md §2 and §3). The server owns the answer, in `SiteOwnership`.
 */
export const Route = createFileRoute('/_staff/staff/$dept/menu')({
  params: {
    parse: ({ dept }) => ({ dept: deptParam.parse(dept) }),
    stringify: ({ dept }) => ({ dept: deptParam.format(dept) }),
  },
  beforeLoad: ({ context, params }) => {
    const owner = menuDepartment(context.bootstrap);

    if (params.dept !== owner || !reachableDepartments(context.bootstrap).includes(params.dept)) {
      throw redirect({ to: '/forbidden' });
    }
  },
  component: Outlet,
});
