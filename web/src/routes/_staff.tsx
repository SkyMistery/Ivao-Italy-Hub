import { createFileRoute } from '@tanstack/react-router';

import { StaffLayout } from '../app/layouts/StaffLayout';

import { requireStaff } from './-staffGuard';

/** Recipe 1 (design M0 §7.3), copied and not reinvented: the guard is `requireStaff`, and this file says what stands behind it. */
export const Route = createFileRoute('/_staff')({
  beforeLoad: ({ context, location }) => {
    requireStaff(context.bootstrap, location.href);
  },
  component: StaffRoot,
});

function StaffRoot() {
  const { bootstrap } = Route.useRouteContext();

  return <StaffLayout bootstrap={bootstrap} />;
}
