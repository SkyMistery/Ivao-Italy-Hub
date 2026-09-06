import { queryOptions } from '@tanstack/react-query';

import type { LocalizedString } from '../shared/api/bootstrap';
import { api, unwrap } from '../shared/api/client';

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
