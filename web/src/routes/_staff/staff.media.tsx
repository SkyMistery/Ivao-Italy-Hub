import { Outlet, createFileRoute, redirect } from '@tanstack/react-router';

import { holdsPermissionAnywhere } from '../../shared/api/bootstrap';

/** The permission the library is behind. */
const MEDIA_VIEW = 'Media.View';

/**
 * The layout of everything under `/staff/media`: the guard, and whichever child matched — the
 * library at `/`, the metadata of one file at `/{id}`.
 *
 * Recipe of three routes, not two (design M0 §7.3, corrected after the whole form half of the back
 * office turned out to be unreachable). Since 13 September 2026 the department is a filter of the
 * library and not a part of the address (note 2026-09-13-contenuti-centralizzati): every file this
 * person may use, theirs and the public ones of the other departments.
 */
export const Route = createFileRoute('/_staff/staff/media')({
  beforeLoad: ({ context }) => {
    if (!holdsPermissionAnywhere(context.bootstrap, MEDIA_VIEW)) {
      throw redirect({ to: '/forbidden' });
    }
  },
  component: Outlet,
});
