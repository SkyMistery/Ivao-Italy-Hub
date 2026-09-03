import createClient, { type Middleware } from 'openapi-fetch';

import { ApiError, type HubProblem } from './problem';

import type { paths } from './schema';

/**
 * The only place that talks to the API. Every call carries `X-Requested-With: hub`, which the
 * server demands on anything that changes state: a cross site form can post with our cookie
 * attached, but it cannot set a header.
 *
 * ESLint forbids `fetch` anywhere outside this folder, so there is no second way in.
 *
 * The typed surface is `schema.d.ts`, generated from the OpenAPI document the build emits. It is
 * committed, and the CI regenerates it and fails on a diff: a path or a field that moved on the
 * server cannot quietly go stale here (design M0 section 7.4).
 */

/** What the server checks for on every state changing call. */
export const REQUESTED_WITH = 'hub';

/** Called when the server says the session is gone, so the cached bootstrap can be dropped. */
type UnauthorizedHandler = () => void;

let onUnauthorized: UnauthorizedHandler = () => {};

export function setUnauthorizedHandler(handler: UnauthorizedHandler): void {
  onUnauthorized = handler;
}

const unauthorizedMiddleware: Middleware = {
  onResponse({ response }) {
    if (response.status === 401) {
      onUnauthorized();
    }
    return undefined;
  },
};

export const api = createClient<paths>({
  baseUrl: '/',
  headers: { 'X-Requested-With': REQUESTED_WITH },
});

api.use(unauthorizedMiddleware);

/** Where the browser goes to start a login. A Kestrel endpoint, not a route of this application. */
export function loginHref(returnUrl: string): string {
  return `/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`;
}

/**
 * The answer of a call, or the refusal as an exception. Every `queries.ts` and `mutations.ts` ends
 * with this line, so that no screen ever reads a status code and every refusal reaches
 * `useProblemDetails` in the same shape.
 */
export function unwrap<T>(result: { data?: T | undefined; error?: unknown; response: Response }): T {
  if (result.error !== undefined || result.data === undefined) {
    throw new ApiError(result.response.status, result.error as HubProblem | undefined);
  }

  return result.data;
}

/** The same, for a call that answers 204 and has nothing to return. */
export function unwrapEmpty(result: { error?: unknown; response: Response }): void {
  if (result.error !== undefined || !result.response.ok) {
    throw new ApiError(result.response.status, result.error as HubProblem | undefined);
  }
}
