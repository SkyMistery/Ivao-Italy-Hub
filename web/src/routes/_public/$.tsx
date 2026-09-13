import { createFileRoute, redirect } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import {
  ContentRenderer,
  EmbeddingContext,
  readBody,
  startsWithPageTitle,
  usePublishedEmbedding,
} from '../../blocks';
import { publicPageQuery } from '../../features/content/queries';
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
 * cannot take an address the application already owns — and the server refuses to give a page one
 * of those addresses in the first place (`ContentAddresses.ReservedSegments`).
 *
 * The address is the whole rest of the path since 13 September 2026, because a page sits under a
 * page up to three levels (note 2026-09-13-contenuti-centralizzati, 3.7): `/training/guide/start`.
 * An address a published page used to have is answered with where it is now, and the router goes
 * there, replacing the old address in the history so the back button does not bounce.
 */
export const Route = createFileRoute('/_public/$')({
  loader: async ({ context, params }) => {
    const answer = await context.queryClient.ensureQueryData(publicPageQuery(params._splat ?? ''));

    if (answer.movedTo !== null) {
      throw redirect({ to: '/$', params: { _splat: answer.movedTo.replace(/^\//, '') }, replace: true });
    }

    if (answer.page === null) {
      // Not reached: the server answers 404 rather than an empty answer. Said anyway, the way every
      // other loader of this site says "nothing here" — an error the route draws as `NotFound`.
      throw new Error(`No page at /${params._splat ?? ''}.`);
    }

    return answer.page;
  },
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
