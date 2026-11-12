import { ApiError } from './problem';

/**
 * Whether a query that failed is asked again, for every query of the application (`main.tsx`, issue #332, item 5).
 *
 * An answer of the server about the request itself — a 4xx: not found, not yours, not signed in, a parameter refused — is
 * the same answer the second time, and a 401 ran the redraw as anonymous at every try: it is not asked again. A 500 or a
 * network that failed may be a moment's: once more, and not the library's three times with a back-off, which kept the
 * error off the screen for about seven seconds.
 */
export function retryQuery(failureCount: number, error: unknown): boolean {
  if (error instanceof ApiError && error.status < 500) {
    return false;
  }

  return failureCount < 1;
}
