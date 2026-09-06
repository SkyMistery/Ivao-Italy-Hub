import { createFileRoute } from '@tanstack/react-router';

import { PublicEntryScreen } from '../../features/content/PublicEntryScreen';
import { publicContentQuery } from '../../features/content/queries';
import { NotFound } from '../../shared/ui';

/**
 * Recipe 3 (design M0 §7.3): one document, loaded before it is drawn. The address is the one
 * `ContentEntry.Url` writes, so the search index and this route cannot point at two places.
 *
 * ⚠️ This segment is a slug and **only** a slug. Design M1 §3.3 asked for `/documents/{dept}` as
 * well, and one path segment cannot be two things: the first version of this route decided by
 * peeking at whether the segment named a department, which quietly reserved nine slugs and shadowed
 * any document unlucky enough to be called `ed`. The documents of one department are
 * `/documents?department=ED` instead — the same filter `/news` already has, in the same grammar
 * (design changelog 1.6).
 */
export const Route = createFileRoute('/_public/documents/$slug')({
  loader: ({ context, params }) =>
    context.queryClient.ensureQueryData(publicContentQuery('Document', params.slug)),
  notFoundComponent: NotFound,
  errorComponent: NotFound,
  component: PublicDocumentEntry,
});

function PublicDocumentEntry() {
  return <PublicEntryScreen content={Route.useLoaderData()} />;
}
