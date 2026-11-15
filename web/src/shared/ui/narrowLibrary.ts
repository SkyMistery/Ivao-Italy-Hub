import type { MediaLibraryQuery } from './MediaPicker';

/** What somebody has asked of the library in a picker (`meta.browse` of the query). */
export interface MediaBrowse {
  q: string;
  category: string | null;
  page: number;
}

/** A picker that has just opened: the first page of everything, which is the newest files. */
export const NOTHING_ASKED: MediaBrowse = { q: '', category: null, page: 1 };

/**
 * The same library, offering only the files of one category (#359): the logos of a `logoGrid`. The
 * query says how it narrows itself, on its `meta` like the way to the library page, because the
 * query is the one thing about the library that reaches a field through the form generator, and
 * only whoever built it knows the filter its endpoint takes (`mediaPickerQuery` does).
 *
 * A library that cannot narrow itself — the gallery's sample — is offered whole: a picker with more
 * files than it should is a picker, and one that throws is a broken form.
 */
export function narrowLibrary(query: MediaLibraryQuery, category: string): MediaLibraryQuery {
  const narrow = query.meta?.narrow;
  return typeof narrow === 'function' ? (narrow as (category: string) => MediaLibraryQuery)(category) : query;
}

/**
 * The same library, offering only pictures (#488): a field that draws a picture is offered no document, which would be
 * drawn as a broken image (note 2026-11-15-il-selettore-delle-foto). Carried on the query like `narrow`, and kept by it:
 * the logos of a `logoGrid` are pictures of one category. A library that cannot narrow itself is offered whole, for the
 * reason above; publication still refuses a document where a picture is drawn (`errors.content.mediaNotPicture`).
 */
export function picturesOnly(query: MediaLibraryQuery): MediaLibraryQuery {
  const pictures = query.meta?.pictures;
  return typeof pictures === 'function' ? (pictures as () => MediaLibraryQuery)() : query;
}
