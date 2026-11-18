/**
 * The hosts a `video` or an `embed` block is allowed to point at, and how an address of one turns
 * into something an `<iframe>` may load.
 *
 * One list, read by both blocks (design M1 §1.2). An editor types a page address — the one in the
 * browser bar of the video they are looking at — and never an embed address: those are a detail of
 * each site, and asking a coordinator for one is asking them to know something the code knows.
 *
 * Why an allow list at all: the source of an `<iframe>` is an address this hub serves under its own
 * origin's eyes. Anything typed into it would let a page of the division carry a frame of any site
 * on the internet, and a page nobody notices is exactly where somebody would put one.
 *
 * ⚠️ The list is **data**, `embedHosts.json` next to this file, and the server reads the same file:
 * an address the block would not frame stops publication (issue #524, Carmine, 18 November 2026:
 * "one JSON", note 2026-11-18-l-indirizzo-che-l-hub-non-incorpora). This file reads it to
 * build the frame; `Core/Content/EmbedAllowlist.cs` reads it to answer "would it be framed?", by the
 * same rules written below, and `embedHosts.cases.json` is the table both sides are tested against.
 *
 * Growing it is adding an entry to the JSON — a host and how to read its address. It is not a
 * decision, because what an entry may do is already decided: build an address, out of identifiers
 * it recognised, on a host it named. Nothing here ever echoes the address it was given.
 *
 * How an entry reads an address, the same on both sides:
 * - only `https`, on one of the entry's `hosts`, matched exactly or as a subdomain;
 * - its `addresses` are tried in order, and the first that **applies** decides: one whose `on` (when
 *   it has one) names the host, and whose literal `path` segments are the address's at their place;
 * - the one that applies reads each `{name}` — a path segment at its place, or a query parameter —
 *   and every one must be an `identifier`, or nothing is framed: an address is never guessed;
 * - the frame is `frame` with each `{name}` replaced; `{parent}` is the host serving this page.
 */

import list from './embedHosts.json';

/** One way of reading an address of a site, as `embedHosts.json` writes it. */
interface AddressShape {
  /** The host names this shape is for, when it is not every host of the site. */
  readonly on?: readonly string[];
  /** The segments of the path, each a literal or a `{name}` to read. */
  readonly path?: readonly string[];
  /** The query parameters to read, each a `{name}`. */
  readonly query?: Readonly<Record<string, string>>;
  /** The address to frame, with the `{name}`s read. */
  readonly frame: string;
}

/** One site the hub knows how to embed. */
export interface EmbedHost {
  /** What it is called, for the i18n key that lists what an editor may paste. */
  readonly key: string;
  /** The host names it answers on, matched exactly or as a subdomain. */
  readonly hosts: readonly string[];
  /** The address to frame, built from what was recognised, or null when it was not. */
  readonly embed: (url: URL) => string | null;
}

const IDENTIFIER = new RegExp(list.identifier);

/** `{id}` is `id`; anything else is a literal. */
function placeholder(part: string): string | null {
  return /^\{(\w+)\}$/.exec(part)?.[1] ?? null;
}

/** Whether a host is that name or one of its subdomains, and never `evil-youtube.com`. */
function matches(hostname: string, host: string): boolean {
  return hostname === host || hostname.endsWith(`.${host}`);
}

/** The path split into its parts, empty ones dropped: `/video/123/` is `['video', '123']`. */
function segments(url: URL): string[] {
  return url.pathname.split('/').filter((part) => part.length > 0);
}

/** Whether a shape is the one that decides for this address. */
function applies(shape: AddressShape, url: URL, parts: readonly string[]): boolean {
  return (
    (shape.on === undefined || shape.on.some((name) => matches(url.hostname, name))) &&
    (shape.path ?? []).every((part, index) => placeholder(part) !== null || parts[index] === part)
  );
}

/** What a shape reads out of an address, or null when one of its names is not an identifier. */
function read(shape: AddressShape, url: URL, parts: readonly string[]): Map<string, string> | null {
  const values = new Map<string, string>();
  const named: [string, string | null | undefined][] = [
    ...(shape.path ?? []).map((part, index) => [part, parts[index]] as [string, string | undefined]),
    ...Object.entries(shape.query ?? {}).map(
      ([key, part]) => [part, url.searchParams.get(key)] as [string, string | null],
    ),
  ];

  for (const [part, value] of named) {
    const name = placeholder(part);
    if (name === null) {
      continue;
    }
    if (value === null || value === undefined || !IDENTIFIER.test(value)) {
      return null;
    }
    values.set(name, value);
  }

  return values;
}

function site(key: string, hosts: readonly string[], shapes: readonly AddressShape[]): EmbedHost {
  return {
    key,
    hosts,
    embed: (url) => {
      const parts = segments(url);
      const shape = shapes.find((candidate) => applies(candidate, url, parts));
      const values = shape === undefined ? null : read(shape, url, parts);

      if (shape === undefined || values === null) {
        return null;
      }

      // The Twitch player refuses to load unless it is told which page is framing it, and the page is
      // whichever host this build is served from: a hard coded one would work on one deployment.
      values.set('parent', typeof window === 'undefined' ? 'localhost' : window.location.hostname);

      return shape.frame.replace(/\{(\w+)\}/g, (_, name: string) => values.get(name) ?? '');
    },
  };
}

export const EMBED_HOSTS: readonly EmbedHost[] = list.sites.map((entry) =>
  site(entry.key, entry.hosts, entry.addresses as readonly AddressShape[]),
);

/** The entry that answers for an address, or null when nothing here does. */
export function hostOf(raw: string): EmbedHost | null {
  let url: URL;
  try {
    url = new URL(raw);
  } catch {
    return null;
  }

  // Only https: an http frame inside an https page is blocked by the browser anyway, and what a
  // reader would see is a hole with no explanation.
  if (url.protocol !== 'https:') {
    return null;
  }

  return EMBED_HOSTS.find((host) => host.hosts.some((name) => matches(url.hostname, name))) ?? null;
}

/**
 * What an `<iframe>` should load for an address an editor typed, or null when this hub will not
 * frame it. Null is not an error to hide: the block says so, because an editor who pasted the
 * wrong thing needs to be told rather than to see nothing — and since #524 publication says so
 * too, so that a visitor never reads it.
 */
export function embedSource(raw: string): string | null {
  const host = hostOf(raw);
  return host === null ? null : host.embed(new URL(raw));
}
