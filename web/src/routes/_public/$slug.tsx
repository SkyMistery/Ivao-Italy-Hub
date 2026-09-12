import { createFileRoute } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import {
  ContentRenderer,
  EmbeddingContext,
  readBody,
  startsWithPageTitle,
  usePublishedEmbedding,
} from '../../blocks';
import { publicContentQuery } from '../../features/content/queries';
import { resolveLocalized } from '../../shared/i18n/localized';
import { useLocalized } from '../../shared/i18n/useLocalized';
import { PageMetadata } from '../../shared/seo/PageMetadata';
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
  const { i18n } = useTranslation();
  const read = useLocalized();
  const content = Route.useLoaderData();
  const { bootstrap } = Route.useRouteContext();

  const body = readBody(content.body);
  const embedding = usePublishedEmbedding(content);

  return (
    <article className="flex flex-col">
      <PageMetadata
        title={content.title}
        description={content.summary}
        seo={content.seo}
        divisionName={resolveLocalized(
          bootstrap.division.name,
          i18n.language,
          bootstrap.division.defaultLocale,
        )}
      />

      {/* The title of the row is what a browser tab and a search result use; what the page itself
          shows is whatever heading block the editor put at the top of it.

          ⚠️ And when that block is already a title, this one is not drawn: a page had **two** `h1`
          otherwise, one of them invisible (`decisions/2026-09-07-giro-visivo-m1.md`, finding 4).
          The row's title still reaches a tab and a search result — `PageMetadata` writes it — and a
          page that opens with a paragraph still gets a name here. */}
      {startsWithPageTitle(body) ? null : <h1 className="sr-only">{read(content.title)}</h1>}
      {/* Where the frame of an interactive block lives: this row, at the version being read. */}
      <EmbeddingContext.Provider value={embedding}>
        <ContentRenderer body={body} />
      </EmbeddingContext.Provider>
    </article>
  );
}
