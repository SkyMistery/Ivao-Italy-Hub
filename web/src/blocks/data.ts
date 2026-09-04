import { queryOptions } from '@tanstack/react-query';

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
