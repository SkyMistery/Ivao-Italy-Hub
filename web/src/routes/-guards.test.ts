import { isRedirect } from '@tanstack/react-router';
import { expect, test } from 'vitest';

import { Route as memberRoute } from './_member';
import { Route as staffRoute } from './_staff';

/**
 * Where a guard sends a visitor who is not signed in (#329).
 *
 * ⚠️ To the server's sign-in, **leaving the application**. The router reloads the document by itself only
 * for an absolute address, and `loginHref` returns a path: without `reloadDocument` the redirect was a
 * navigation inside the application, to `/auth/login`, which has no route of its own, and `_public/$`
 * answered it with "This page does not exist". Nothing saw it: no test opened a guarded page signed out.
 * The browser half is `e2e/guards.spec.ts`.
 *
 * The file starts with `-` because it lives under `routes/`, like `-routes.test.ts`.
 */

type BeforeLoad = (args: unknown) => unknown;

/** The redirect a guard throws for a visitor who is not signed in, at `href`. */
function signedOut(
  guard: BeforeLoad | undefined,
  href: string,
): Response & { options: Record<string, unknown> } {
  try {
    guard?.({ context: { bootstrap: { user: null } }, location: { href } });
  } catch (thrown) {
    if (isRedirect(thrown)) {
      return thrown as Response & { options: Record<string, unknown> };
    }
    throw thrown;
  }
  throw new Error('The guard let a visitor who is not signed in through.');
}

test.each([
  ['the staff guard', staffRoute.options.beforeLoad as BeforeLoad | undefined, '/staff/links?page=2#top'],
  ['the member guard', memberRoute.options.beforeLoad as BeforeLoad | undefined, '/me/tokens?page=2#top'],
])(
  '%s sends a visitor who is not signed in to the server, with the page to come back to',
  (_, guard, href) => {
    const { options } = signedOut(guard, href);

    expect(options.href).toBe(`/auth/login?returnUrl=${encodeURIComponent(href)}`);
    expect(options.reloadDocument).toBe(true);
  },
);
