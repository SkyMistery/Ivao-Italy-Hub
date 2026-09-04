import { createFileRoute } from '@tanstack/react-router';

import { ContentRenderer, readBody } from '../../blocks';
import { publicContentQuery } from '../../features/content/queries';
import { useLocalized } from '../../shared/i18n/useLocalized';
import { NotFound } from '../../shared/ui';

/**
 * Recipe 3 (design M0 §7.3): a public page, loaded before it is drawn.
 *
 * What arrives is the published version and never the draft behind it — the server reads
 * `PublishedVersionId` and the visibility filter does the rest — so an editor saving in the back
 * office changes nothing here until somebody publishes.
 *
 * The static routes of `_public` win over this one, so `/forbidden` stays `/forbidden`: a page
 * cannot take an address the application already owns.
 */
export const Route = createFileRoute('/_public/$slug')({
  loader: ({ context, params }) =>
    context.queryClient.ensureQueryData(publicContentQuery('Page', params.slug)),
  notFoundComponent: NotFound,
  errorComponent: NotFound,
  component: PublicContentPage,
});

function PublicContentPage() {
  const read = useLocalized();
  const content = Route.useLoaderData();

  return (
    <article className="flex flex-col">
      {/* The title of the row is what a browser tab and a search result use; what the page itself
          shows is whatever heading block the editor put at the top of it. */}
      <h1 className="sr-only">{read(content.title)}</h1>
      <ContentRenderer body={readBody(content.body)} />
    </article>
  );
}
