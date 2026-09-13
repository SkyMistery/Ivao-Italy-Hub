import { Link } from '@tanstack/react-router';
import type { ComponentProps } from 'react';

import { linkTarget } from '../../shared/ui/linkTarget';

/**
 * An anchor that navigates without reloading the page.
 *
 * Atmosphere's navigation menu and sidebar take a component and hand it a plain `href`, because
 * they know nothing about our router; TanStack's `Link` is typed against the generated route tree,
 * which is what makes a typo in a path a build error. The two meet here, once, so that the widening
 * happens in one adapter instead of at every call site — and so that anything built from data (the
 * menu of the bootstrap, the departments of a member) still gets a real client side navigation.
 */
export function RouterAnchor({ href, ...rest }: ComponentProps<'a'>) {
  if (href === undefined) {
    // Atmosphere allows an entry with no address; it is a label, not a link.
    return <a {...rest} />;
  }

  // The one cast of the adapter: an anchor's props are all optional strings, a `Link`'s are the
  // exact union the route tree generated. Widening happens here so that nothing else has to.
  //
  // ⚠️ `exact`, and it is an accessibility fix before it is anything else. TanStack marks a link
  // `aria-current="page"` when the address *begins* with it, so a department's dashboard — whose
  // address is the department's own root — was announced as the current page on every screen of
  // that department, next to the entry that really was; and the home of the site, `/`, on every
  // page of it. Measured in the DOM on 11 September 2026, while fixing the same fault as it showed
  // to the eye. "The current page" is one page.
  //
  // And `includeSearch: false` with it, or exact matching finds nothing at all: a list puts its
  // paging and its sorting in the address, and `/staff/ed/documents?page=1` is not, to an exact
  // comparison that counts the query, the page `/staff/ed/documents` links to.
  //
  // ⚠️ Unless the address carries a query of its own. Since 13 September 2026 the back office has
  // one screen per object and says which kind and which department in the query
  // (`/staff/content?kind=News&department=TD`), and `to` is a path: a query written into it is not
  // parsed, it becomes part of the path and matches no route. So it travels as `search`, and it is
  // compared — against what the address bar holds, not against the defaults a route fills in — so
  // that the pages of Training and the pages of every department are not both "the current page".
  const target = linkTarget(href);

  return (
    <Link
      {...({
        activeOptions: { exact: true, includeSearch: target.search !== undefined },
        ...rest,
        ...target,
      } as unknown as ComponentProps<typeof Link>)}
    />
  );
}
