import { Button, Subtle } from '@ivao/atmosphere-react';
import { useQuery, type UseQueryOptions } from '@tanstack/react-query';
import { Link } from '@tanstack/react-router';
import { FileText, Upload } from 'lucide-react';
import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { resolveLocalized } from '../i18n/localized';

import { EmptyState } from './layout-pieces';

/**
 * Choosing a file out of the library of a department. It is a custom component of the closed list
 * (design M1 §12) because two very different screens mount it: the metadata screen of the library,
 * and — from G2 onwards — every block property annotated `.meta({ media: true })`, which is eight
 * of the twenty two blocks.
 *
 * It picks and does nothing else. Uploading is the back office screen's job, and a picker that also
 * uploaded would be a second place where a file can enter the library.
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
   * the department the document belongs to"); the picker still only picks — the upload is the same
   * call the library screen makes, reached from one more place.
   */
  onUpload?: ((file: File) => Promise<number>) | undefined;
  locale: string;
  defaultLocale: string;
  disabled?: boolean;
}) {
  const { t } = useTranslation();
  const { data, isPending } = useQuery(query);

  const items = data?.items ?? [];

  // The file input is hidden and a button opens it: what the browser draws for one is not a thing
  // that reads in every language or theme.
  const fileInput = useRef<HTMLInputElement>(null);
  const [uploading, setUploading] = useState(false);
  const [refused, setRefused] = useState<string | null>(null);

  const upload = async (file: File | undefined) => {
    if (file === undefined || onUpload === undefined) {
      return;
    }

    setUploading(true);
    setRefused(null);
    try {
      onChange(await onUpload(file));
    } catch (error) {
      setRefused(error instanceof Error ? error.message : String(error));
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
        {refused === null ? null : (
          <p role="alert" className="text-destructive text-sm">
            {t('media.picker.uploadRefused')}
          </p>
        )}
      </div>
    );

  // Where the files come from, when the query says so (`mediaPickerQuery` does): the picker only
  // picks, and the way to the page that uploads should be one press away and not a thing to know
  // (Carmine, 11 September 2026).
  const libraryHref = typeof query.meta?.libraryHref === 'string' ? query.meta.libraryHref : null;
  const library =
    libraryHref === null ? null : (
      <Link to={libraryHref} className="text-primary text-sm underline-offset-2 hover:underline">
        {t('media.picker.open')}
      </Link>
    );

  if (!isPending && items.length === 0) {
    return (
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
    );
  }

  return (
    <div className="flex flex-col gap-3">
      <ul
        // A grid rather than a list of names: a picture is recognised and a file name is read, and
        // a coordinator choosing a hero image is recognising.
        className="grid grid-cols-2 gap-3 sm:grid-cols-3 md:grid-cols-4"
        aria-busy={isPending}
        aria-label={t('media.picker.label')}
      >
        {items.map((media) => {
          const chosen = media.id === value;
          // A file with no alternative text yet still needs one here, or a screen reader is read
          // an empty button. The name it was uploaded with is the honest stand in.
          const written = resolveLocalized(media.alt, locale, defaultLocale);
          const alt = written === '' ? media.fileName : written;

          return (
            <li key={media.id}>
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
            </li>
          );
        })}
      </ul>

      {value === null ? null : (
        <div className="flex items-center gap-3">
          <Subtle>{t('media.picker.chosen', { id: value })}</Subtle>
          <Button type="button" variant="ghost" size="sm" disabled={disabled} onClick={() => onChange(null)}>
            {t('media.picker.clear')}
          </Button>
        </div>
      )}

      <div className="flex flex-wrap items-center gap-3">
        {uploader}
        {library}
      </div>
    </div>
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
