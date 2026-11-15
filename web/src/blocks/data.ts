import { queryOptions } from '@tanstack/react-query';
import type { z } from 'zod';

import type { LocalizedString } from '../shared/api/bootstrap';
import { api, unwrap } from '../shared/api/client';
import { readFields, type FieldNode } from '../shared/forms';

/**
 * Asking the server what a data block should show, right now. The same provider answers here and
 * at publication; the difference is only when the question is asked (design M0 §5.5).
 *
 * The properties travel base64url encoded, because they are an opaque JSON object and a query
 * string has no shape for one. Plain base64 would carry `+`, which a query string reads as a
 * space, so the two characters are swapped and the padding dropped — the server accepts either
 * alphabet.
 */

export const blockDataKey = ['blocks', 'data'] as const;

export function encodeProps(props: Record<string, unknown>): string {
  const json = JSON.stringify(props);
  const bytes = new TextEncoder().encode(json);
  const binary = Array.from(bytes, (byte) => String.fromCharCode(byte)).join('');

  return btoa(binary).replaceAll('+', '-').replaceAll('/', '_').replace(/=+$/, '');
}

/** The fields of each block's schema, read once: a page draws its data blocks on every render. */
const fieldsOfSchema = new WeakMap<z.ZodType, FieldNode[]>();

/**
 * The question a data block asks the server: its properties without its words (PR 3.13, note
 * `2026-11-14-domande-e-risposte-e-le-etichette`). A translated field (`localized`) is what the block writes around the
 * answer — the word under a figure — and never part of the question: no provider reads one. Left in, every letter typed
 * in it would be a new request, with the figures back to "loading" and the provider counting again, and two pages asking
 * the same thing with different words would not share the answer.
 *
 * Only `localized` fields go, inside the entries of a list too; everything else stays where it was, in the order it was.
 * A block with no words gets its own properties back, so its request — and its place in the cache — is the one it
 * always had. Publication's capture is the server's, from the whole properties, and does not change.
 */
export function questionOf(schema: z.ZodType, props: Record<string, unknown>): Record<string, unknown> {
  let fields = fieldsOfSchema.get(schema);

  if (fields === undefined) {
    fields = readFields(schema);
    fieldsOfSchema.set(schema, fields);
  }

  return withoutWords(fields, props);
}

function withoutWords(fields: FieldNode[], value: Record<string, unknown>): Record<string, unknown> {
  const byName = new Map(fields.map((field) => [field.path.slice(field.path.lastIndexOf('.') + 1), field]));
  let changed = false;
  const kept: Record<string, unknown> = {};

  for (const [name, held] of Object.entries(value)) {
    const field = byName.get(name);

    if (field?.kind === 'localized') {
      changed = true;
      continue;
    }

    if (field?.kind === 'list' && Array.isArray(held)) {
      const children = field.children;
      const entries = held.map((entry: unknown) =>
        entry !== null && typeof entry === 'object' && !Array.isArray(entry)
          ? withoutWords(children, entry as Record<string, unknown>)
          : entry,
      );
      changed ||= entries.some((entry, index) => entry !== held[index]);
      kept[name] = entries;
      continue;
    }

    if (field?.kind === 'object' && held !== null && typeof held === 'object' && !Array.isArray(held)) {
      const inner = withoutWords(field.children, held as Record<string, unknown>);
      changed ||= inner !== held;
      kept[name] = inner;
      continue;
    }

    kept[name] = held;
  }

  return changed ? kept : value;
}

export function blockDataQuery(type: string, props: Record<string, unknown>) {
  const encoded = encodeProps(props);

  return queryOptions({
    queryKey: [...blockDataKey, type, encoded] as const,
    queryFn: async (): Promise<unknown> =>
      unwrap(
        await api.GET('/api/blocks/data/{type}', {
          params: { path: { type }, query: { props: encoded } },
        }),
      ),
    // A live block is live, not fresh to the second: a page full of them must not turn into a
    // request per block on every navigation.
    staleTime: 60_000,
  });
}

/**
 * What both `ContentListProvider`s answer with — the news and the documents — each kind adding two
 * keys of its own to a row.
 *
 * It lives here rather than next to the components because it is the shape of an *answer*, and
 * because a helper exported from `blocks.tsx` would cost that file its fast refresh (HANDOFF §3).
 */
export interface ContentListData {
  items?: {
    id?: number;
    title?: LocalizedString;
    summary?: LocalizedString | null;
    url?: string;
    /** The collections the row is filed in (G20). */
    collections?: string[];
    /** What a list captured before G20 carries instead: its one category. */
    category?: string | null;
    publishedAt?: string | null;
    coverMediaId?: number | null;
    fileMediaId?: number | null;
    pinned?: boolean;
  }[];
  /**
   * The shelves this kind is filed under, with their translated names. It travels with the rows
   * because whoever may read the list may read the names of its shelves, and because a category
   * key on its own is not something to show a reader (design M1 §3.4).
   */
  categories?: { key?: string; label?: LocalizedString }[];
}

/**
 * The collection a row is shown under in a list: the one the block lists, when it names one; the
 * first the row is filed in otherwise; and the category a list captured before G20 carries.
 */
export function shelfOf(
  item: NonNullable<ContentListData['items']>[number],
  listed: string | undefined,
): string {
  const collections = item.collections ?? [];

  if (listed !== undefined && listed !== '' && collections.includes(listed)) {
    return listed;
  }

  return collections[0] ?? item.category ?? '';
}

/**
 * The name of a shelf, or the key itself when the vocabulary has nothing to say about it — which
 * is exactly what a category somebody deleted looks like from here. The row keeps the key it was
 * filed under, and the list shows it as it is (design M1 §3.4).
 */
export function categoryLabel(
  data: ContentListData | null | undefined,
  key: string | null | undefined,
  read: (value: LocalizedString | null | undefined) => string,
): string {
  if (key === null || key === undefined || key === '') {
    return '';
  }

  const written = read(data?.categories?.find((entry) => entry.key === key)?.label);
  return written === '' ? key : written;
}
