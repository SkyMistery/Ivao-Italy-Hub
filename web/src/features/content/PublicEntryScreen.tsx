import { Button, H1, Lead } from '@ivao/atmosphere-react';
import { Download } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { ContentRenderer, readBody } from '../../blocks';
import { mediaFileUrl } from '../../shared/api/mediaUrl';
import { useLocalized } from '../../shared/i18n/useLocalized';

import type { PublicContentDto } from './queries';

/**
 * One news item or one document, as a visitor reads it. What arrives is the published version and
 * never the draft behind it — the server reads `PublishedVersionId` and the visibility filter does
 * the rest — so an editor saving in the back office changes nothing here until somebody publishes.
 *
 * The body is the same `ContentRenderer` a page uses, because a news item **is** a page with three
 * more columns (design M1 §3.1). What this screen adds is the header those three columns make: a
 * cover, a date, and — for a document that has a file — the download that makes it a card rather
 * than something to read.
 */
export function PublicEntryScreen({ content }: { content: PublicContentDto }) {
  const { t, i18n } = useTranslation();
  const read = useLocalized();

  const summary = read(content.summary);
  const published = new Intl.DateTimeFormat(i18n.language, {
    dateStyle: 'long',
    timeZone: 'UTC',
  }).format(new Date(content.publishedAt));

  return (
    <article className="mx-auto flex w-full max-w-3xl flex-col gap-6 px-4 py-10">
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

      <ContentRenderer body={readBody(content.body)} />
    </article>
  );
}
