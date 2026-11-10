import { Button, Subtle } from '@ivao/atmosphere-react';
import { keepPreviousData, useQueries, useQuery, type UseQueryOptions } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { FileText, Upload } from 'lucide-react';
import { useId, useRef, useState, type DragEvent, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { describeProblem } from '../forms/useProblemDetails';
import { resolveLocalized } from '../i18n/localized';
import { ListFilter } from '../list/ListFilter';
import { SearchBox } from '../list/SearchBox';

import { EmptyState } from './layout-pieces';
import { NOTHING_ASKED, type MediaBrowse } from './narrowLibrary';

/**
 * Choosing a file out of the library of a department. It is a custom component of the closed list
 * (design M1 §12) because two very different screens mount it: the metadata screen of the library,
 * and — from G2 onwards — every block property annotated `.meta({ media: true })`, which is eight
 * of the twenty two blocks.
 *
 * It looks for a file (a search, a category, a next page: the same read of the library, design of
 * the editor's phase 3, §4.4) and it may take a new one where the screen says so, through the same
 * call the library screen makes: the upload sits next to the search because under two dozen pictures
 * the staff never saw it.
 *
 * ⚠️ A media is never a number somebody types (design M1 §1.5): a free numeric field produces pages
 * pointing at files that were deleted years ago. That is the whole reason this exists.
 */

/** What the picker needs of a row. It is a subset of `MediaListDto`, on purpose: the component is
 * in `shared/` and must not depend on the generated contract of one endpoint. */
export interface PickableMedia {
  id: number;
  fileName: string;
  contentType: string;
  alt: Record<string, string>;
  url: string;
}

/** A page of rows, in the shape every list of the hub answers with. */
export interface MediaPage<TRow extends PickableMedia = PickableMedia> {
  items: TRow[];
  total: number;
}

/**
 * The library a picker chooses from, as query options. Named because the form generator carries it
 * from the screen down to whichever field asked for a media: the generator cannot build it itself,
 * since which department's library to show is a fact of the screen and not of the schema.
 *
 * What it can do besides answering travels on its `meta`, for the same reason: `libraryHref` and
 * `fileHref` (where the files live), `narrow` (one category for good: `narrowLibrary`), `browse` (the
 * same library, asked for a `MediaBrowse`) and `categories` (the ones a person may choose among). A
 * library that says none of it — the tests' — is a grid, and a grid is still a picker.
 */
export type MediaLibraryQuery = UseQueryOptions<MediaPage, Error, MediaPage, readonly unknown[]>;

export function MediaPicker<TRow extends PickableMedia, TKey extends readonly unknown[]>({
  query,
  value,
  onChange,
  onUpload,
  locale,
  defaultLocale,
  disabled = false,
}: {
  /** The page of the library to choose from, as query options, exactly like `DataList`. */
  query: UseQueryOptions<MediaPage<TRow>, Error, MediaPage<TRow>, TKey>;
  /** The chosen file, or null for none. */
  value: number | null;
  onChange: (id: number | null) => void;
  /**
   * Uploads a file into this library and answers its identifier, which is then chosen. Given by a
   * screen that may upload here (Carmine, 11 September 2026: "yes, if it lands in the library of
   * the department the document belongs to"); the upload is the same call the library screen makes,
   * reached from one more place.
   */
  onUpload?: ((file: File) => Promise<number>) | undefined;
  locale: string;
  defaultLocale: string;
  disabled?: boolean;
}) {
  const { t, i18n } = useTranslation();
  const filterId = useId();

  const browse = query.meta?.browse;
  const browsable = typeof browse === 'function';
  const categories = (query.meta?.categories ?? []) as readonly string[];
  const fileHref = query.meta?.fileHref as ((id: number) => string) | undefined;

  const [asked, setAsked] = useState(NOTHING_ASKED);
  // Which search box is on screen. A new one is the only way of emptying what somebody typed: the
  // box holds its own draft, and that is what keeps the focus in it while the grid answers.
  const [searchBox, setSearchBox] = useState(0);

  const pageOf = (page: number) =>
    browsable ? (browse as (asked: MediaBrowse) => typeof query)({ ...asked, page }) : query;

  // "Show more" adds to the grid, it does not turn it over. The pages already read stay queries and
  // not a copy, so they follow the library like the last one does.
  const earlier = useQueries({
    queries: Array.from({ length: asked.page - 1 }, (_, index) => pageOf(index + 1)),
  });

  const { data, isPending, isPlaceholderData, isLoadingError, isFetching, refetch } = useQuery({
    ...pageOf(asked.page),
    // The grid stays while the next answer comes: a grid that empties at every word is a flicker
    // under the hand of somebody typing.
    placeholderData: keepPreviousData,
  });

  // A file uploaded between two pages moves the others down by one, and the last of a page comes
  // back as the first of the next.
  const items = [
    ...new Map(
      [...earlier.flatMap((page) => page.data?.items ?? []), ...(data?.items ?? [])].map((media) => [
        media.id,
        media,
      ]),
    ).values(),
  ];

  // Also when the last page brought nothing: `total` may have grown since the first page was read,
  // and a button that answers nothing would stay there for good.
  const more = browsable && data !== undefined && data.items.length > 0 && items.length < data.total;
  const settled = !isPending && !isPlaceholderData;
  // The page asked for has never answered. A later read that fails leaves the rows it had, which are
  // still true; this one has nothing to show, and "no file matches" would be a false statement.
  const readFailed = isLoadingError;

  // The grid scrolls by itself, and what it answers next starts from its first row: a file just
  // uploaded is that row, and it would be chosen above the top of the box.
  const grid = useRef<HTMLUListElement>(null);

  const ask = (patch: Partial<MediaBrowse>) => {
    setAsked((now) => ({ ...now, ...patch, page: 1 }));
    if (grid.current !== null) {
      grid.current.scrollTop = 0;
    }
  };

  // The file input is hidden and a button opens it: what the browser draws for one is not a thing
  // that reads in every language or theme.
  const fileInput = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState(false);
  const [refused, setRefused] = useState<unknown>(null);
  const [onlyFirst, setOnlyFirst] = useState(false);
  // The file uploaded here. It alone says its alternative text is to be written: whoever uploaded it
  // can write it, which a tile of somebody else's department could not promise.
  const [uploaded, setUploaded] = useState<number | null>(null);

  const refusal = describeProblem(refused, t, i18n.language);

  const upload = async (file: File | undefined, others = false) => {
    if (file === undefined || onUpload === undefined || uploading) {
      return;
    }

    setUploading(true);
    setRefused(null);
    setOnlyFirst(others);
    try {
      const id = await onUpload(file);
      setUploaded(id);
      // Back to the newest files, where the new one is: it would be chosen and out of sight under a
      // search or a category it does not match.
      ask(NOTHING_ASKED);
      setSearchBox((box) => box + 1);
      onChange(id);
    } catch (error) {
      setRefused(error);
    } finally {
      setUploading(false);
      if (fileInput.current !== null) {
        fileInput.current.value = '';
      }
    }
  };

  const uploader =
    onUpload === undefined ? null : (
      <div className="flex flex-col gap-1">
        <input
          ref={fileInput}
          type="file"
          hidden
          aria-label={t('media.picker.upload')}
          onChange={(event) => void upload(event.target.files?.[0])}
        />
        <div>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            disabled={disabled || uploading}
            isLoading={uploading}
            onClick={() => fileInput.current?.click()}
          >
            <Upload aria-hidden className="mr-2 size-4" />
            {t(uploading ? 'media.picker.uploading' : 'media.picker.upload')}
          </Button>
        </div>
        {refusal === null ? null : (
          <p role="alert" className="text-destructive text-sm">
            {refusal}
          </p>
        )}
        {onlyFirst ? <Subtle className="text-xs">{t('media.picker.onlyFirst')}</Subtle> : null}
      </div>
    );

  // Where the files come from, when the query says so (`mediaPickerQuery` does): the way to the page
  // that holds them should be one press away and not a thing to know (Carmine, 11 September 2026).
  const libraryHref = typeof query.meta?.libraryHref === 'string' ? query.meta.libraryHref : null;
  const library =
    libraryHref === null ? null : (
      <Link to={libraryHref} className="text-primary text-sm underline-offset-2 hover:underline">
        {t('media.picker.open')}
      </Link>
    );

  const droppable = onUpload !== undefined && !disabled;
  // A picker chooses one file, so one is uploaded, and whoever let go of several is told.
  const takeDropped = (files: ArrayLike<File>) => void upload(files[0], files.length > 1);

  // Only a library with nothing in it says so: under a search, the box that took the grid away has
  // to stay, or there is no way back.
  if (settled && !readFailed && items.length === 0 && asked.q === '' && asked.category === null) {
    return (
      <FileDrop enabled={droppable} onFiles={takeDropped}>
        <EmptyState
          title={t('media.picker.empty')}
          description={t('media.picker.emptyHint')}
          action={
            <div className="flex flex-col items-center gap-2">
              {uploader}
              {library}
            </div>
          }
        />
      </FileDrop>
    );
  }

  return (
    <div className="flex flex-col gap-3">
      {!browsable && uploader === null ? null : (
        <div className="flex flex-col gap-1">
          <div className="flex flex-wrap items-end gap-3">
            {browsable ? (
              <SearchBox
                key={searchBox}
                // A line of its own in a block's panel, next to the category where there is room.
                className="min-w-56 flex-1"
                initial={asked.q}
                placeholder={t('media.picker.search')}
                onCommit={(q) => ask({ q })}
              />
            ) : null}
            {categories.length === 0 ? null : (
              <ListFilter
                id={filterId}
                className="min-w-36"
                label={t('media.fields.category')}
                none={t('media.picker.anyCategory')}
                value={asked.category ?? undefined}
                onChange={(category) => ask({ category: category ?? null })}
                items={categories.map((category) => ({
                  value: category,
                  label: t(`media.options.category.${category}`),
                }))}
              />
            )}
            {uploader}
          </div>
          {droppable ? <Subtle className="text-xs">{t('media.picker.dropHint')}</Subtle> : null}
        </div>
      )}

      <FileDrop enabled={droppable} onFiles={takeDropped}>
        {settled && items.length === 0 ? (
          readFailed ? null : (
            <Subtle>{t('media.picker.noMatch')}</Subtle>
          )
        ) : (
          <ul
            ref={grid}
            // A grid rather than a list of names: a picture is recognised and a file name is read, and
            // a coordinator choosing a hero image is recognising. As many columns as the place it is
            // in has room for, not the window: a block's panel is narrow on the widest screen. And no
            // taller than this, or two dozen files push the block's other fields out of reach.
            className="grid max-h-[28rem] grid-cols-[repeat(auto-fill,minmax(7rem,1fr))] gap-3 overflow-y-auto p-1"
            aria-busy={!settled}
            aria-label={t('media.picker.label')}
          >
            {items.map((media) => {
              const chosen = media.id === value;
              // A file with no alternative text yet still needs one here, or a screen reader is read
              // an empty button. The name it was uploaded with is the honest stand in.
              const written = resolveLocalized(media.alt, locale, defaultLocale);
              const alt = written === '' ? media.fileName : written;

              return (
                <li key={media.id} className="flex flex-col gap-1">
                  <button
                    type="button"
                    disabled={disabled}
                    aria-pressed={chosen}
                    onClick={() => onChange(chosen ? null : media.id)}
                    className={`border-border bg-card flex w-full flex-col items-stretch gap-2 rounded-md border p-2 text-left ${
                      chosen ? 'ring-primary ring-2' : ''
                    }`}
                  >
                    <MediaThumbnail media={media} alt={alt} />
                    <span className="text-foreground truncate text-xs" title={media.fileName}>
                      {media.fileName}
                    </span>
                  </button>
                  {media.id === uploaded && written === '' ? (
                    <AltToWrite href={fileHref?.(media.id)} />
                  ) : null}
                </li>
              );
            })}
          </ul>
        )}
      </FileDrop>

      {readFailed ? (
        <div role="alert" className="flex flex-wrap items-center gap-3">
          <p className="text-destructive text-sm">{t('media.picker.readFailed')}</p>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            disabled={isFetching}
            isLoading={isFetching}
            onClick={() => void refetch()}
          >
            {t('media.picker.retry')}
          </Button>
        </div>
      ) : null}

      {more ? (
        <div>
          <Button
            type="button"
            variant="ghost"
            size="sm"
            disabled={!settled}
            isLoading={isPlaceholderData}
            onClick={() => setAsked((now) => ({ ...now, page: now.page + 1 }))}
          >
            {t('media.picker.more')}
          </Button>
        </div>
      ) : null}

      {value === null ? null : (
        <div className="flex items-center gap-3">
          <Subtle>{t('media.picker.chosen', { id: value })}</Subtle>
          <Button type="button" variant="ghost" size="sm" disabled={disabled} onClick={() => onChange(null)}>
            {t('media.picker.clear')}
          </Button>
        </div>
      )}

      {library}
    </div>
  );
}

/**
 * Where a file can be let go. Only a dragged **file** is taken: what the editor's own drag carries, a
 * block or a section, passes through, and the browser is told to keep its hands off a file dropped
 * here, or it would leave the page to show it, with the draft in it.
 */
function FileDrop({
  enabled,
  onFiles,
  children,
}: {
  enabled: boolean;
  onFiles: (files: ArrayLike<File>) => void;
  children: ReactNode;
}) {
  const [over, setOver] = useState(false);

  const carriesFile = (event: DragEvent) => enabled && event.dataTransfer.types.includes('Files');

  return (
    <div
      className={over ? 'outline-primary rounded-md outline-2 outline-offset-2 outline-dashed' : undefined}
      onDragOver={(event) => {
        if (carriesFile(event)) {
          event.preventDefault();
          event.dataTransfer.dropEffect = 'copy';
          setOver(true);
        }
      }}
      onDragLeave={(event) => {
        // Leaving a thumbnail for the one next to it is a leave too, and it is not leaving this.
        if (!event.currentTarget.contains(event.relatedTarget as Node | null)) {
          setOver(false);
        }
      }}
      onDrop={(event) => {
        if (carriesFile(event)) {
          event.preventDefault();
          setOver(false);
          onFiles(event.dataTransfer.files);
        }
      }}
    >
      {children}
    </div>
  );
}

/**
 * A file with no alternative text, and the way to its page in the library. In another tab: whoever
 * reads this is in the middle of a page they have not saved. A block's own alternative text stays
 * the block's (`docs/UI-GUIDELINES.md`, 6 September 2026); this is the one every block inherits.
 */
function AltToWrite({ href }: { href: string | undefined }) {
  const { t } = useTranslation();
  const words = t('media.picker.altToWrite');

  return href === undefined ? (
    <span className="text-muted-foreground text-xs">{words}</span>
  ) : (
    <Link
      to={href}
      target="_blank"
      rel="noopener"
      className="text-primary text-xs underline-offset-2 hover:underline"
    >
      {words}
    </Link>
  );
}

/**
 * An image shows itself; anything else shows what it is. A PDF drawn as a broken image would look
 * like a file that failed to upload.
 */
function MediaThumbnail({ media, alt }: { media: PickableMedia; alt: string }) {
  if (!media.contentType.startsWith('image/')) {
    return (
      <span className="bg-body text-muted-foreground flex h-24 items-center justify-center rounded">
        <FileText aria-hidden className="size-8" />
      </span>
    );
  }

  return (
    <img src={media.url} alt={alt} loading="lazy" className="bg-body h-24 w-full rounded object-contain" />
  );
}
