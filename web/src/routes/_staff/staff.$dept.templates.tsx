import { Outlet, createFileRoute, redirect } from '@tanstack/react-router';

import { holdsPermission, reachableDepartments } from '../../shared/api/bootstrap';
import { deptParam } from '../../shared/api/department';
import { MANAGE_TEMPLATES } from '../../features/content/templateRules';

/**
 * The layout of everything under `/staff/<dept>/templates`: it owns the department in the address
 * and the guard on it, and draws whichever child matched — the list at `/`, the editor at `/{id}`.
 *
 * ⚠️ It carries a **second** guard the other resources do not have, and that is the point of the
 * screen. Every staff member may *read* a template — otherwise "new from a template" would not
 * exist for eight departments out of nine (design M1 §9.4) — but changing one changes every page
 * made from it afterwards, so the screen that changes them is behind `Content.ManageTemplates` on
 * this department. The server says the same thing through `CrudOptions.ExtraWritePolicy`; this is
 * only the door, so that the answer is not a 403 after the click.
 */
export const Route = createFileRoute('/_staff/staff/$dept/templates')({
  params: {
    parse: ({ dept }) => ({ dept: deptParam.parse(dept) }),
    stringify: ({ dept }) => ({ dept: deptParam.format(dept) }),
  },
  beforeLoad: ({ context, params }) => {
    if (!reachableDepartments(context.bootstrap).includes(params.dept)) {
      throw redirect({ to: '/forbidden' });
    }

    if (!holdsPermission(context.bootstrap, MANAGE_TEMPLATES, params.dept)) {
      throw redirect({ to: '/forbidden' });
    }
  },
  component: Outlet,
});
