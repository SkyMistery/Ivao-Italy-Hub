import { Outlet, createFileRoute, redirect } from '@tanstack/react-router';

import { reachableDepartments } from '../../shared/api/bootstrap';
import { deptParam } from '../../shared/api/department';

/**
 * The layout of everything under `/staff/<dept>/contacts`: it owns the department in the address
 * and the guard on it, and draws whichever child matched — the queue at `/`, one message at
 * `/{id}`. The same recipe as every other resource of the back office (design M0 §7.3).
 */
export const Route = createFileRoute('/_staff/staff/$dept/contacts')({
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
