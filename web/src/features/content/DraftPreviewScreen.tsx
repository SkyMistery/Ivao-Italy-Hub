import { Button } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { RefreshCw } from 'lucide-react';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';

import {
  ContentRenderer,
  EmbeddingContext,
  frameAddress,
  readBody,
  startsWithPageTitle,
  useLinksDisabled,
  type Embedding,
} from '../../blocks';
import { PreviewLocaleContext } from '../../shared/i18n/previewLocale';
import { resolveLocalized } from '../../shared/i18n/localized';
import { PageMetadata } from '../../shared/seo/PageMetadata';
import { collectionsOfKindQuery } from '../categories/queries';

import { PublicEntryScreen } from './PublicEntryScreen';
import type { ContentDetailDto, PublicContentDto } from './queries';

/**
 * A row of the back office in the shape a visitor's screen reads, for a draft that has never been published.
 *
 * What only a publication knows stands in as near as it can: the date is the last save's, and the footer of a document
 * stays off, because the edition it would name does not exist yet. A draft has no captures and no fingerprints of its files.
 * The names of its collections are the vocabulary's, which whoever looks at a draft may read; a publication carries them.
 */
function asPublicEntry(
  row: ContentDetailDto,
  collectionNames: PublicContentDto['collectionNames'],
): PublicContentDto {
  return {
    id: row.id,
    kind: row.kind,
    slug: row.slug,
    path: row.path,
    ownerDepartment: row.ownerDepartment,
    title: row.title,
    summary: row.summary,
    seo: row.seo,
    body: row.body,
    schemaVersion: row.schemaVersion,
    collections: row.collections,
    coverMediaId: row.coverMediaId,
    fileMediaId: row.fileMediaId,
    version: 0,
    // Not the row's `publishedAt` when it is already online: every publication writes its own instant
    // (`ContentPublishService`), so the date this draft will carry is the next one's, which the last save is nearest to.
    publishedAt: row.updatedAt,
    effectiveOn: row.effectiveOn,
    reviewOn: row.reviewOn,
    retiredAt: row.retiredAt,
    supersededBySlug: null,
    supersededByTitle: null,
    showFooter: false,
    publishedByName: null,
    media: {},
    collectionNames,
  };
}

/**
 * The saved draft as a visitor would read it, at the true width of the window (design of phase 3, §4.7). The same renderer
 * and the same screens a visitor gets, with nothing of the composing around them: no picking, and the links stopped
 * (`useLinksDisabled`).
 *
 * ⚠️ Not an emulator (`docs/UI-GUIDELINES.md`): the three widths stay in the editor, and here the window decides.
 *
 * The language chosen in the editor draws what the row says, as it does in the editor's own preview
 * (`PreviewLocaleContext`); the frame of the site, and what the code says, stay in the site's language.
 */
export function DraftPreviewScreen({
  row,
  locale,
  refreshing,
  refreshFailed,
  onRefresh,
}: {
  row: ContentDetailDto;
  locale: string;
  refreshing: boolean;
  /** The last read again was refused or lost: what is on screen is the one before it. */
  refreshFailed: boolean;
  onRefresh: () => void;
}) {
  const { t } = useTranslation();

  useLinksDisabled();

  // The frame of the draft, served only to somebody who may edit it. The row's version in the address, so that a frame shows
  // what was saved since the last read: an unchanged draft leaves the address, and whatever is open in the frame, alone.
  const embedding = useMemo<Embedding>(
    () => ({
      frameUrl: (blockId) => frameAddress(row.id, 'draft', blockId, locale, row.rowVersion),
    }),
    [row.id, row.rowVersion, locale],
  );

  const body = useMemo(() => readBody(row.body), [row.body]);

  // The names a publication would carry (issue #332, item 12): the row's own department's first, as the server picks them.
  const vocabulary = useQuery({ ...collectionsOfKindQuery(row.kind), enabled: row.collections.length > 0 });
  const collectionNames = useMemo(() => {
    const names = new Map<string, PublicContentDto['collectionNames'][number]>();
    for (const collection of vocabulary.data?.items ?? []) {
      if (!names.has(collection.key) || collection.ownerDepartment === row.ownerDepartment) {
        names.set(collection.key, { key: collection.key, label: collection.label });
      }
    }
    return [...names.values()];
  }, [vocabulary.data, row.ownerDepartment]);
  const title = resolveLocalized(row.title, locale, locale);
  const isEntry = row.kind === 'News' || row.kind === 'Document';

  return (
    <PreviewLocaleContext.Provider value={locale}>
      <EmbeddingContext.Provider value={embedding}>
        <PageMetadata title={t('content.draftPreview.tabTitle', { title })} />

        {/* Sticky, so that «refresh» is at hand wherever the page is being read; never on paper. */}
        <div className="bg-muted border-border sticky top-0 z-50 mb-6 flex flex-wrap items-center justify-between gap-x-4 gap-y-2 rounded-md border px-4 py-2 text-sm print:hidden">
          <p className="flex flex-col">
            <span className="font-medium">{t('content.draftPreview.notice')}</span>
            {refreshFailed ? (
              <span role="alert" className="text-destructive">
                {t('content.draftPreview.refreshFailed')}
              </span>
            ) : null}
          </p>
          <Button type="button" variant="secondary" size="sm" disabled={refreshing} onClick={onRefresh}>
            <RefreshCw aria-hidden className="size-4" />
            {t('content.draftPreview.refresh')}
          </Button>
        </div>

        {isEntry ? (
          <PublicEntryScreen content={asPublicEntry(row, collectionNames)} embedding={embedding} staff />
        ) : (
          <article className="flex flex-col">
            {startsWithPageTitle(body) ? null : <h1 className="sr-only">{title}</h1>}
            <ContentRenderer body={body} staff dashboard={row.kind === 'Dashboard'} />
          </article>
        )}
      </EmbeddingContext.Provider>
    </PreviewLocaleContext.Provider>
  );
}
