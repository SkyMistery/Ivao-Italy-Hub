import { Outlet, createFileRoute, redirect } from '@tanstack/react-router';

import { holdsPermissionAnywhere } from '../../shared/api/bootstrap';

/** The catalogue is read with this, on any department. */
const AWARDS_VIEW = 'Awards.View';
/** The queue and the register are behind this, which is global. */
const AWARDS_ASSIGN = 'Awards.Assign';

/**
 * The layout of everything under `/staff/awards` (M2, T4b): the catalogue, the queue and the register.
 * Two permissions reach here — whoever writes the award of a department and whoever assigns — and each
 * screen below checks the one it is behind.
 */
export const Route = createFileRoute('/_staff/staff/awards')({
  beforeLoad: ({ context }) => {
    if (
      !holdsPermissionAnywhere(context.bootstrap, AWARDS_VIEW) &&
      !holdsPermissionAnywhere(context.bootstrap, AWARDS_ASSIGN)
    ) {
      throw redirect({ to: '/forbidden' });
    }
  },
  component: Outlet,
});
