import { createFileRoute, redirect } from '@tanstack/react-router';

import { UiKitPage } from '../../features/admin/UiKitPage';
import { holdsPermissionAnywhere } from '../../shared/api/bootstrap';

/**
 * The component gallery. Behind `Admin.Access` like everything under `/staff/admin/*`: it shows
 * every screen the hub can draw, which is not something to leave open (design M0 §7.1 and §7.2).
 */
const ADMIN_ACCESS = 'Admin.Access';

export const Route = createFileRoute('/_staff/staff/admin/ui-kit')({
  beforeLoad: ({ context }) => {
    if (!holdsPermissionAnywhere(context.bootstrap, ADMIN_ACCESS)) {
      throw redirect({ to: '/forbidden' });
    }
  },
  component: UiKit,
});

function UiKit() {
  const { bootstrap } = Route.useRouteContext();
  return <UiKitPage bootstrap={bootstrap} />;
}
