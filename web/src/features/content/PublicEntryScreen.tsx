import { Button, H1, Lead } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { Download } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import {
  ContentRenderer,
  EmbeddingContext,
  PrintContext,
  readBody,
  usePublishedEmbedding,
} from '../../blocks';
import { mediaFileUrl } from '../../shared/api/mediaUrl';
import { resolveLocalized } from '../../shared/i18n/localized';
import { useLocalized } from '../../shared/i18n/useLocalized';
import { PageMetadata } from '../../shared/seo/PageMetadata';
import { bootstrapQuery } from '../me/queries';

import { DocumentFooter, DocumentNotice, DocumentStrip } from './DocumentFrame';
import type { PublicContentDto } from './queries';
import { usePrintMode } from './usePrintMode';

/**
 * One news item or one document, as a visitor reads it. What arrives is the published version and
 * never the draft behind it — the server reads `PublishedVersionId` and the visibility filter does
 * the rest — so an editor saving in the back office changes nothing here until somebody publishes.
 *
 * The body is the same `ContentRenderer` a page uses, because a news item **is** a page with three
 * more columns (design M1 §3.1). What this screen adds is the header those three columns make: a
 * cover, a date, and — for a document that has a file — the download that makes it a card rather
 * than something to read.
 *
 * An **operational** document (G14) adds three pieces around the same body: the strip of what it
 * is about under the title, the notice when it is not the one to follow, and the footer that says
 * which edition this is. All three are this screen's, none the renderer's (`DocumentFrame`). And
 * while it is being printed the blocks are told so, which is what unfolds the tabs on paper.
 */
export function PublicEntryScreen({ content }: { content: PublicContentDto }) {
  const { t, i18n } = useTranslation();
  const read = useLocalized();
  const { data: bootstrap } = useQuery(bootstrapQuery);
  const printing = usePrintMode();

  // The frame of an interactive block, addressed by the version being read: a published version
  // never changes, so what comes back is cacheable for a year (`EmbedEndpoints`).
  const embedding = usePublishedEmbedding(content);

  const isDocument = content.kind === 'Document';
  const summary = read(content.summary);
  const published = new Intl.DateTimeFormat(i18n.language, {
    dateStyle: 'long',
    timeZone: 'UTC',
  }).format(new Date(content.publishedAt));

  return (
    <article className="mx-auto flex w-full max-w-3xl flex-col gap-6 px-4 py-10">
      {/* A news item is the thing people actually paste into a chat, so its cover is what stands in
          when the editor named no picture of its own. */}
      <PageMetadata
        title={content.title}
        description={content.summary}
        seo={content.seo}
        imageMediaId={content.coverMediaId}
        divisionName={resolveLocalized(
          bootstrap?.division.name,
          i18n.language,
          bootstrap?.division.defaultLocale ?? i18n.language,
        )}
      />

      <header className="flex flex-col gap-3">
        <div className="text-muted-foreground flex flex-wrap items-center gap-3 text-sm">
          <time dateTime={content.publishedAt} className="tabular-nums">
            {published}
          </time>
          <span>{t(`departments.${content.ownerDepartment}`)}</span>
          {content.category === null ? null : <span>{content.category}</span>}
        </div>

        <H1>{read(content.title)}</H1>
        {summary === '' ? null : <Lead>{summary}</Lead>}
      </header>

      {isDocument ? (
        <>
          <DocumentNotice content={content} />
          <DocumentStrip content={content} />
        </>
      ) : null}

      {/* The cover, and no alternative text of its own: the headline right above says what the
          picture illustrates, and repeating it is what a screen reader hears twice (design M1 §1.2). */}
      {content.coverMediaId === null ? null : (
        <img
          src={mediaFileUrl(content.coverMediaId)}
          alt=""
          className="bg-muted w-full rounded-lg object-cover"
        />
      )}

      {/* A document with a file is a card with a download; one without is read in the browser like
          any other page (design M1 §3.3). The file is served by Kestrel with the query filter in
          front of it, so a document nobody may read is a link that answers 404. */}
      {content.fileMediaId === null ? null : (
        <div className="border-border flex flex-wrap items-center justify-between gap-4 rounded-lg border p-4">
          <span className="text-muted-foreground text-sm">{t('documents.public.fileHint')}</span>
          <Button asChild>
            <a href={mediaFileUrl(content.fileMediaId)} download>
              <Download aria-hidden className="mr-2 size-4" />
              {t('documents.public.download')}
            </a>
          </Button>
        </div>
      )}

      {/* The two things the page around a body grants it: whether it is being printed, and where the
          frame of an interactive block lives. The second is the version being read, which only this
          screen knows — a block knows neither the row it is on nor which version of it
          (`blocks/embedding.ts`). */}
      <PrintContext.Provider value={printing}>
        <EmbeddingContext.Provider value={embedding}>
          <ContentRenderer body={readBody(content.body)} />
        </EmbeddingContext.Provider>
      </PrintContext.Provider>

      {/* The footer is the document's own, switched off on the row when it is not wanted; a news
          item never has one, its date is at the top. */}
      {content.kind === 'Document' && content.showFooter ? <DocumentFooter content={content} /> : null}
    </article>
  );
}
