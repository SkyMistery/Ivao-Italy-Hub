import createClient, { type Middleware } from 'openapi-fetch';

import type { Bootstrap } from './bootstrap';

/**
 * The only place that talks to the API. Every call carries `X-Requested-With: hub`, which the
 * server demands on anything that changes state: a cross site form can post with our cookie
 * attached, but it cannot set a header.
 *
 * ESLint forbids `fetch` anywhere outside this folder, so there is no second way in.
 *
 * The typed surface arrives in F5, generated from the OpenAPI document into `schema.d.ts`. Until
 * then the paths used by F2 are declared here by hand.
 */
export interface ApiPaths {
  '/api/me': {
    get: {
      responses: {
        200: { content: { 'application/json': Bootstrap } };
      };
    };
  };
  '/auth/logout': {
    post: {
      responses: {
        204: { content: never };
      };
    };
  };
}

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

export const api = createClient<ApiPaths>({
  baseUrl: '/',
  headers: { 'X-Requested-With': REQUESTED_WITH },
});

api.use(unauthorizedMiddleware);

/** Where the browser goes to start a login. A Kestrel endpoint, not a route of this application. */
export function loginHref(returnUrl: string): string {
  return `/auth/login?returnUrl=${encodeURIComponent(returnUrl)}`;
}
