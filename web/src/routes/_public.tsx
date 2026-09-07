import { Outlet, createFileRoute } from '@tanstack/react-router';

import { Shell } from '../app/layouts/Chrome';
import { LiveStatusStrip } from '../shared/ui';

/**
 * Recipe 1 without a guard: the public site is open, so the layout only supplies the frame. It is
 * still a layout route rather than nothing, so that `/` and a published page share one header, one
 * footer and one code split boundary (design M0 §7.2).
 *
 * The live strip goes in the frame's `banner` slot: edge to edge under the header, which is what
 * makes a strip a strip rather than a line inside the reading column. It is passed from here and
 * not put in `Shell` itself because `Shell` is the frame of all three layouts, and a reader of the
 * back office is not looking at who is on frequency.
 */
export const Route = createFileRoute('/_public')({
  component: PublicLayout,
});

function PublicLayout() {
  const { bootstrap } = Route.useRouteContext();

  return (
    <Shell bootstrap={bootstrap} banner={<LiveStatusStrip />}>
      <Outlet />
    </Shell>
  );
}
