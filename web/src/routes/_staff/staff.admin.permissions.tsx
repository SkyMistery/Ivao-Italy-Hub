import { Outlet, createFileRoute, redirect } from '@tanstack/react-router';

import { holdsPermissionAnywhere } from '../../shared/api/bootstrap';

/**
 * The layout of everything under `/staff/admin/permissions`: it owns the guard and draws whichever
 * child matched -- the list at `/`, the form of one grant at `/{id}`.
 *
 * See the links layout for why this is a layout and not the list itself: a detail route that is a
 * child of a component with no `Outlet` never renders, however right the address bar looks.
 */
const PERMISSIONS_MANAGE = 'Permissions.Manage';

export const Route = createFileRoute('/_staff/staff/admin/permissions')({
  beforeLoad: ({ context }) => {
    if (!holdsPermissionAnywhere(context.bootstrap, PERMISSIONS_MANAGE)) {
      throw redirect({ to: '/forbidden' });
    }
  },
  component: Outlet,
});
