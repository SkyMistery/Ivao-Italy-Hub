import { createFileRoute } from '@tanstack/react-router';

import { PublicEntryScreen } from '../../features/content/PublicEntryScreen';
import { publicContentQuery } from '../../features/content/queries';
import { NotFound } from '../../shared/ui';

/**
 * Recipe 3 (design M0 §7.3): one news item, loaded before it is drawn. The address is the one
 * `ContentEntry.Url` writes, so the search index and this route cannot point at two places.
 */
export const Route = createFileRoute('/_public/news/$slug')({
  loader: ({ context, params }) =>
    context.queryClient.ensureQueryData(publicContentQuery('News', params.slug)),
  notFoundComponent: NotFound,
  errorComponent: NotFound,
  component: PublicNewsEntry,
});

function PublicNewsEntry() {
  return <PublicEntryScreen content={Route.useLoaderData()} />;
}
