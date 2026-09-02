import { Outlet, createRootRoute } from '@tanstack/react-router';

import { AppShell } from '../app/AppShell';

/**
 * Root route. The three layouts of the design (`_public`, `_member`, `_staff`) and the router
 * context `{ queryClient, bootstrap }` arrive in F6; F2 only needs a frame with a way in and out.
 */
export const Route = createRootRoute({
  component: () => (
    <AppShell>
      <Outlet />
    </AppShell>
  ),
});
