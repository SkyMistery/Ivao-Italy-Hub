import { Outlet, createFileRoute, redirect } from '@tanstack/react-router';

import { holdsPermissionAnywhere } from '../../shared/api/bootstrap';

/** The permission the links of the back office are behind. */
const LINKS_VIEW = 'Links.View';

/**
 * The layout of everything under `/staff/links`: the guard, and whichever child matched — the list
 * at `/`, the form at `/{id}`.
 *
 * It exists as a layout, rather than being the list itself, because of a real failure: the list
 * *was* this route, the form was its child, and nothing rendered an `Outlet`. Clicking "new link"
 * changed the address and left the list on the screen, so the whole form half of the back office
 * was unreachable in a browser while every test stayed green.
 *
 * Since 13 September 2026 the department is not in the address (note
 * 2026-09-13-contenuti-centralizzati): one list of every link this person may read — their
 * departments' and the public ones of the others — with the department as a filter.
 */
export const Route = createFileRoute('/_staff/staff/links')({
  beforeLoad: ({ context }) => {
    if (!holdsPermissionAnywhere(context.bootstrap, LINKS_VIEW)) {
      throw redirect({ to: '/forbidden' });
    }
  },
  component: Outlet,
});
