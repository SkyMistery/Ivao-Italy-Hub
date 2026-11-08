import { useQuery } from '@tanstack/react-query';
import { createFileRoute, redirect, type ErrorComponentProps } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { draftPreviewSearchSchema } from '../../features/content/contentSearch';
import { DraftPreviewScreen } from '../../features/content/DraftPreviewScreen';
import { contentQuery } from '../../features/content/queries';
import { holdsPermissionAnywhere } from '../../shared/api/bootstrap';
import { ApiError } from '../../shared/api/problem';
import { Forbidden, NotFound } from '../../shared/ui';
import { requireStaff } from '../-staffGuard';

/** The permission every screen of the content of the back office is behind (`staff.content.tsx`). */
const CONTENT_VIEW = 'Content.View';

/**
 * The saved draft of a row, in the frame of the public site (design of phase 3, §4.7).
 *
 * Under `_public` because that layout **is** the frame: the bar, the live strip, the footer, and a refused read drawn inside
 * them. The guard is the back office's, and the read is the one the editor uses, so whoever is not served the row in the editor
 * is not served it here. The address stays under `/staff/content`, which no page may take.
 *
 * What the screen reads is the query the loader filled, as in the editor (`-routes.test.ts`): it reads the row again when the
 * window comes back into focus, which is TanStack Query's default for an answer with no `staleTime`, and when asked to.
 */
export const Route = createFileRoute('/_public/staff/content/$id/preview')({
  validateSearch: draftPreviewSearchSchema,
  beforeLoad: ({ context, location }) => {
    requireStaff(context.bootstrap, location.href);

    if (!holdsPermissionAnywhere(context.bootstrap, CONTENT_VIEW)) {
      throw redirect({ to: '/forbidden' });
    }
  },
  loader: ({ context, params }) => context.queryClient.ensureQueryData(contentQuery(Number(params.id))),
  notFoundComponent: NotFound,
  errorComponent: Refused,
  component: DraftPreview,
});

/** Two answers, as the guard gives them: this row is not for you, or there is no such row. */
function Refused({ error }: ErrorComponentProps) {
  return error instanceof ApiError && error.status === 403 ? <Forbidden /> : <NotFound />;
}

function DraftPreview() {
  const { i18n } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const { id } = Route.useParams();
  const { lang } = Route.useSearch();

  // `retry: false`: a read refused while the tab stays open is said at once, over the last draft that was read.
  const draft = useQuery({ ...contentQuery(Number(id)), retry: false });

  if (draft.data === undefined) {
    // The loader has already put it in the cache: the compiler asking, not a state a reader reaches.
    return null;
  }

  return (
    <DraftPreviewScreen
      row={draft.data}
      // A language the division does not have, written by hand into the address, is the site's language.
      locale={lang !== undefined && bootstrap.division.locales.includes(lang) ? lang : i18n.language}
      refreshing={draft.isFetching}
      refreshFailed={draft.isRefetchError}
      onRefresh={() => void draft.refetch()}
    />
  );
}
