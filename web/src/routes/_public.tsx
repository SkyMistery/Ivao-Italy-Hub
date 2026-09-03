import { Outlet, createFileRoute } from '@tanstack/react-router';

import { Shell } from '../app/layouts/Chrome';

/**
 * Recipe 1 without a guard: the public site is open, so the layout only supplies the frame. It is
 * still a layout route rather than nothing, so that `/` and a published page share one header, one
 * footer and one code split boundary (design M0 §7.2).
 */
export const Route = createFileRoute('/_public')({
  component: PublicLayout,
});

function PublicLayout() {
  const { bootstrap } = Route.useRouteContext();

  return (
    <Shell bootstrap={bootstrap}>
      <Outlet />
    </Shell>
  );
}
