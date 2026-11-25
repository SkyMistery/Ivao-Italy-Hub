import type { LocalizedString } from '../shared/api/bootstrap';

/**
 * The map of the site (design `13-design-struttura-del-sito.md` §5.1): what `SiteMapProvider` answers, made into the
 * nested list the block draws. The server answers flat, in the order of the address, with what whoever is reading may
 * open; the nesting and the order of sisters are made here, because the order is by title in the language on screen and
 * only the browser knows which one that is.
 */

/** One entry of the answer: a page online, with its title, or a screen of the application, with the key of its name. */
export interface SiteMapEntry {
  path?: string;
  title?: LocalizedString | null;
  titleKey?: string | null;
}

/** What `SiteMapProvider` answers with. Read defensively: it is JSON off the wire. */
export interface SiteMapData {
  items?: SiteMapEntry[];
}

/** One line of the map, with the lines of the pages under it. */
export interface SiteMapNode {
  /** Where the line leads. */
  href: string;
  /** What it is called, in the language on screen. */
  name: string;
  children: SiteMapNode[];
}

/**
 * The slug of the front page. A row like any other in the tree, read at `/home` as well as at `/` (design 13 §1, ⚠️ on the
 * front page), and the screen `/` is that same page seen from another door. Its own file, because `HomePage` draws the
 * blocks and a block that imported the screen would import itself.
 */
export const HOME_SLUG = 'home';

function segments(path: string): string[] {
  return path.split('/').filter((segment) => segment !== '');
}

/**
 * The lines of the map. `name` names an entry in the language on screen, `locale` is that language for the order.
 *
 * - **The front page is one line**, at `/`: when the reader may open the row `home`, it stands for the screen `/` under its
 *   own title, and the screen is left out; otherwise the screen is the line. It comes first, before the rest of the top.
 * - **A page sits under the nearest page above it that the reader may open**: a page whose mother they may not read is
 *   still on the map, one level up, rather than lost with her.
 * - **Sisters are in the order of their names**, in the language on screen (design 13 §5.1).
 */
export function siteMapTree(
  items: readonly SiteMapEntry[],
  name: (entry: SiteMapEntry) => string,
  locale: string,
): SiteMapNode[] {
  const entries = items.filter(
    (entry): entry is SiteMapEntry & { path: string } => typeof entry.path === 'string',
  );
  const home = `/${HOME_SLUG}`;
  const hasHomePage = entries.some((entry) => entry.titleKey == null && entry.path.toLowerCase() === home);

  const byPath = new Map<string, SiteMapNode>();
  const roots: SiteMapNode[] = [];

  const ordered = entries
    .filter((entry) => !(hasHomePage && entry.titleKey != null && entry.path === '/'))
    .sort((left, right) => segments(left.path).length - segments(right.path).length);

  for (const entry of ordered) {
    const key = entry.path.toLowerCase();
    if (byPath.has(key)) {
      continue;
    }

    const node: SiteMapNode = {
      href: entry.titleKey == null && key === home ? '/' : entry.path,
      name: name(entry) || entry.path,
      children: [],
    };
    byPath.set(key, node);

    const parts = segments(key);
    let mother: SiteMapNode | undefined;
    for (let length = parts.length - 1; length > 0 && mother === undefined; length--) {
      mother = byPath.get(`/${parts.slice(0, length).join('/')}`);
    }

    (mother?.children ?? roots).push(node);
  }

  const collator = new Intl.Collator(locale);
  const sort = (nodes: SiteMapNode[]): SiteMapNode[] => {
    nodes.sort((left, right) => collator.compare(left.name, right.name));
    nodes.forEach((node) => sort(node.children));
    return nodes;
  };

  sort(roots);

  const front = roots.findIndex((node) => node.href === '/');
  if (front > 0) {
    roots.unshift(...roots.splice(front, 1));
  }

  return roots;
}
