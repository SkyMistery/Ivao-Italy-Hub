import { Outlet, createRootRoute } from '@tanstack/react-router';

/**
 * Root route. The three layouts of the design (`_public`, `_member`, `_staff`) and the router
 * context `{ queryClient, bootstrap }` arrive in F6.
 */
export const Route = createRootRoute({
  component: () => <Outlet />,
});
