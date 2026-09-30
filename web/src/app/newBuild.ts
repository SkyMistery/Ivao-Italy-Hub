import type { Bootstrap } from '../shared/api/bootstrap';

/**
 * A page that outlived a delivery (note 2026-09-30-la-pagina-dopo-una-consegna).
 *
 * A tab opened before an upload keeps running the bundle it was loaded with, while the bootstrap it
 * fetches again comes from the new server: the menu then names screens the old bundle has neither
 * the routes nor the words for (`nav.trainees`, "This page does not exist"), and only a reload
 * mends it. So the page remembers the stamp of the server that served it — the first bootstrap it
 * saw — and a later bootstrap with another stamp means the code on screen is no longer the code
 * that runs: the next navigation loads the page again instead of drawing it.
 *
 * ⚠️ Compared server with server, never with a stamp baked into the bundle. The server is the one
 * that knows what is running (note 2026-09-27-la-versione-del-sito), and a stamp of the client's own
 * would differ from the server's in development and in every suite that stubs `/api/me`, which is
 * a reload on every click. What this misses is a bundle that was already old when its page first
 * asked for the bootstrap — an `index.html` from a cache — and the fallback serves it with no
 * validator a browser could keep it by.
 */
type Stamped = Pick<Bootstrap, 'version' | 'commit'>;

/** The stamp a bootstrap carries, as one word: `0.5.0+6261ffd`, the number alone for a build with no commit. */
export function stampOf(bootstrap: Stamped): string {
  return bootstrap.commit ? `${bootstrap.version}+${bootstrap.commit}` : bootstrap.version;
}

/** The mark left in `sessionStorage` before a reload, one per reason: the reason is never reloaded for twice. */
export const RELOADED_FOR = 'hub:reloaded-for:';

/**
 * Whether the page may be loaded again for this reason, and if so remembers that it was.
 *
 * ⚠️ The guard against a loop. Two processes of two releases can answer side by side for a while
 * during a delivery, and a page that reloaded whenever the stamp moved would bounce between them;
 * with one reload per stamp a tab reloads at most once for each release it meets. With no storage to
 * write the mark in there is no reload at all: an old page is a nuisance, a page that never stops
 * loading is an outage.
 */
export function claimReload(storage: Storage | undefined, reason: string): boolean {
  if (storage === undefined) {
    return false;
  }

  try {
    const key = RELOADED_FOR + reason;
    if (storage.getItem(key) !== null) {
      return false;
    }

    storage.setItem(key, '1');
    return true;
  } catch {
    // A browser that refuses the storage (a private window, site data blocked) keeps its page.
    return false;
  }
}

/** The tab's `sessionStorage`, or nothing where reading it throws. */
export function tabStorage(): Storage | undefined {
  try {
    return typeof window === 'undefined' ? undefined : window.sessionStorage;
  } catch {
    return undefined;
  }
}

export interface BuildWatch {
  /** True when this bootstrap comes from another build than the page's, and the page has not yet reloaded for it. */
  shouldReload(bootstrap: Stamped): boolean;
}

export function createBuildWatch(storage: () => Storage | undefined = tabStorage): BuildWatch {
  let served: string | undefined;

  return {
    shouldReload(bootstrap) {
      const stamp = stampOf(bootstrap);
      served ??= stamp;

      return stamp !== served && claimReload(storage(), stamp);
    },
  };
}

/**
 * A chunk of the bundle that is no longer on the server, because a delivery replaced it: Vite says so
 * with `vite:preloadError` for every dynamic import it wraps, and the page loads again once for that
 * chunk. The error still goes on to whoever imported it — the router reloads too for a screen's
 * chunk (`lazyRouteComponent`), and two calls to `reload` are one reload.
 */
export function reloadWhenAChunkIsGone(
  target: Window = window,
  storage: () => Storage | undefined = tabStorage,
): void {
  target.addEventListener('vite:preloadError', (event) => {
    const payload = (event as Event & { payload?: unknown }).payload;
    const reason = payload instanceof Error ? payload.message : String(payload);

    if (claimReload(storage(), `chunk:${reason}`)) {
      target.location.reload();
    }
  });
}
