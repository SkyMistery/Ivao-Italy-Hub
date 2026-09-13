/**
 * An address written as one string, turned into what the router's `Link` takes: a path, and the
 * query as `search`. `to` is a path — a query written into it is not parsed, it becomes part of the
 * path and matches no route — and the back office has addresses with a query of their own since
 * 13 September 2026 (`/staff/content?kind=News&department=TD`, note
 * 2026-09-13-contenuti-centralizzati). Everything that is handed an address as data and draws a
 * `Link` from it goes through here: the sidebar, the palette, the breadcrumb.
 */
export function linkTarget(href: string): { to: string; search?: Record<string, string> } {
  const [path, query] = href.split('?', 2) as [string, string | undefined];

  return query === undefined
    ? { to: path }
    : { to: path, search: Object.fromEntries(new URLSearchParams(query)) };
}
