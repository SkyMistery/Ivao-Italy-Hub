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
 * Growing it is adding an entry here — a host and how to read its address. It is not a decision,
 * because what an entry may do is already decided: build an address, out of parts it recognised,
 * on a host it named. Nothing here ever echoes the address it was given.
 */

/** One site the hub knows how to embed. */
export interface EmbedHost {
  /** What it is called, for the i18n key that lists what an editor may paste. */
  readonly key: string;
  /** The host names it answers on, matched exactly or as a subdomain. */
  readonly hosts: readonly string[];
  /** The address to frame, built from what was recognised, or null when it was not. */
  readonly embed: (url: URL) => string | null;
}

/** The identifier of a resource, when it looks like one. Never anything with a slash or a dot. */
function identifier(value: string | null | undefined): string | null {
  return value !== null && value !== undefined && /^[\w-]{1,64}$/.test(value) ? value : null;
}

/** The path split into its parts, empty ones dropped: `/video/123/` is `['video', '123']`. */
function segments(url: URL): string[] {
  return url.pathname.split('/').filter((part) => part.length > 0);
}

export const EMBED_HOSTS: readonly EmbedHost[] = [
  {
    key: 'youtube',
    hosts: ['youtube.com', 'youtube-nocookie.com', 'youtu.be'],
    embed: (url) => {
      // Three shapes reach us: the watch address, the short one, and an embed address somebody
      // copied from elsewhere. All three carry the same identifier, and it is the identifier —
      // not the address — that is used to build what the frame loads.
      const parts = segments(url);
      const id = identifier(
        url.hostname.endsWith('youtu.be')
          ? parts[0]
          : parts[0] === 'embed' || parts[0] === 'shorts'
            ? parts[1]
            : url.searchParams.get('v'),
      );

      return id === null ? null : `https://www.youtube-nocookie.com/embed/${id}`;
    },
  },
  {
    key: 'vimeo',
    hosts: ['vimeo.com', 'player.vimeo.com'],
    embed: (url) => {
      const parts = segments(url);
      const id = identifier(parts[0] === 'video' ? parts[1] : parts[0]);

      return id === null ? null : `https://player.vimeo.com/video/${id}`;
    },
  },
  {
    key: 'twitch',
    hosts: ['twitch.tv', 'www.twitch.tv', 'player.twitch.tv'],
    embed: (url) => {
      const parts = segments(url);
      const channel = identifier(parts[0] === 'videos' ? null : parts[0]);
      const video = identifier(parts[0] === 'videos' ? parts[1] : null);

      // The player refuses to load unless it is told which page is framing it, and the page is
      // whichever host this build is served from: a hard coded one would work on one deployment.
      const parent = typeof window === 'undefined' ? 'localhost' : window.location.hostname;

      if (video !== null) {
        return `https://player.twitch.tv/?video=${video}&parent=${parent}`;
      }

      return channel === null ? null : `https://player.twitch.tv/?channel=${channel}&parent=${parent}`;
    },
  },
];

/** Whether a host is that name or one of its subdomains, and never `evil-youtube.com`. */
function matches(hostname: string, host: string): boolean {
  return hostname === host || hostname.endsWith(`.${host}`);
}

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
 * wrong thing needs to be told rather than to see nothing.
 */
export function embedSource(raw: string): string | null {
  const host = hostOf(raw);
  return host === null ? null : host.embed(new URL(raw));
}
