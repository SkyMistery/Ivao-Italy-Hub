import type { MediaLibraryQuery } from './MediaPicker';

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
