import type { TFunction } from 'i18next';

import type { SearchHit } from './queries';

/** What `sourceModule` says for a row of the editorial core (`ProjectionSource.Core` on the server). */
const CORE_SOURCE = 'core';

/**
 * The word for what kind of row a hit is.
 *
 * Whoever projected the row owns the word: the core keeps its own under `search.kinds`, and a
 * module keeps `search.kinds.<kind>` in its own language file, asked for through its namespace —
 * the way the staff sidebar asks a module for `nav.section`. The core never lists a module's kinds
 * (note `2026-10-06-le-etichette-dei-tipi-nella-ricerca`); `modules/manifest.test.ts` fails when a
 * module projects a kind and brings no word for it.
 *
 * `defaultValue` is the kind itself, so a row from a module this build does not know shows the key
 * rather than nothing — the same choice the category of a news item makes.
 */
export function kindLabel(t: TFunction, hit: Pick<SearchHit, 'sourceModule' | 'kind'>): string {
  const key =
    hit.sourceModule === CORE_SOURCE
      ? `search.kinds.${hit.kind}`
      : `${hit.sourceModule}:search.kinds.${hit.kind}`;

  return t(key, { defaultValue: hit.kind });
}
