import { Outlet, createFileRoute, redirect } from '@tanstack/react-router';

import { holdsPermissionAnywhere } from '../../shared/api/bootstrap';

/** The permission every screen of the content of the back office is behind. */
const CONTENT_VIEW = 'Content.View';

/**
 * The layout of everything under `/staff/content`: the guard, and whichever child matched — the
 * list at `/`, the editor at `/{id}`.
 *
 * Since 13 September 2026 the department is not in the address (note
 * 2026-09-13-contenuti-centralizzati): it is a filter of the list and a fact of the row. So the
 * guard asks the one thing that is left to ask before the server does — whether this person reads
 * content anywhere — and the server narrows every list and refuses every row that is not theirs.
 *
 * A layout and not the list itself, for the reason the links layout gives: a detail route that is a
 * child of a component with no `Outlet` never renders, and the address bar says otherwise.
 */
export const Route = createFileRoute('/_staff/staff/content')({
  beforeLoad: ({ context }) => {
    if (!holdsPermissionAnywhere(context.bootstrap, CONTENT_VIEW)) {
      throw redirect({ to: '/forbidden' });
    }
  },
  component: Outlet,
});
