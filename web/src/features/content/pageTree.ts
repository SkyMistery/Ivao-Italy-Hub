import type { Department, LocalizedString, SiteScreen } from '../../shared/api/bootstrap';
import type { ChoiceOption } from '../../shared/forms';
import type { Visibility } from '../../shared/ui';

import type { ContentPageNodeDto, ContentStatus } from './queries';

/**
 * The tree of the site (design `13-design-struttura-del-sito.md` §3.2): the pages a person may read online or edit, as
 * `GET /api/content/pages` gives them, and the pages written in code, as the bootstrap carries them
 * (`registries.screens`, §3.1), in one order — the order of the address, a level at a time.
 *
 * One file for the three places that show it: the «Tree» view of `/staff/content`, and the choice of the page a page
 * sits under in the form of a page, in «new from a template» and in the panel of whoever approves. Until 4.2 each of
 * those three wrote its own labels.
 */

/** Levels of the address of a page: `/a/b/c` (`ContentAddresses.MaxDepth`). A page at the third has no pages under it. */
export const MAX_DEPTH = 3;

/** The value of a screen in a choice of the parent: never sent, since the choice is switched off. */
const SCREEN_VALUE = 'screen:';

/** One row of the tree: a page of the content, or a page written in code. */
export interface SiteTreeRow {
  /** The row of `cms_contents`, or null for a screen. */
  id: number | null;
  /** Where it is read, with the leading slash. */
  path: string;
  /** Its name: a translated title, or — for a screen — the name of its key in the language on screen. */
  title: LocalizedString;
  depth: number;
  source: 'Page' | 'Screen';
  ownerDepartment: Department | null;
  visibility: Visibility;
  /** The state of a page the person may edit; null for a page they only read online, and for a screen. */
  status: ContentStatus | null;
  /** Whether the person may open it in the editor. Never a screen. */
  mayEdit: boolean;
}

/**
 * Two addresses in the order of the tree: a level at a time, so a page comes right before the pages under it. A plain
 * comparison of the two strings would put `/a-b` between `/a` and `/a/b`, because `-` comes before `/`.
 */
export function byAddress(left: string, right: string): number {
  const a = left.split('/').filter((segment) => segment !== '');
  const b = right.split('/').filter((segment) => segment !== '');

  for (let index = 0; index < Math.min(a.length, b.length); index++) {
    const order = a[index]! < b[index]! ? -1 : a[index]! > b[index]! ? 1 : 0;
    if (order !== 0) {
      return order;
    }
  }

  return a.length - b.length;
}

function depthOf(path: string): number {
  return Math.max(1, path.split('/').filter((segment) => segment !== '').length);
}

/**
 * The rows of the tree. `name` turns the key of a screen into its name, in the language `locale` is: a screen has no
 * translated title of its own, so it is given one in the language on screen.
 */
export function siteTree(
  nodes: readonly ContentPageNodeDto[],
  screens: readonly SiteScreen[],
  name: (key: string) => string,
  locale: string,
): SiteTreeRow[] {
  return [
    ...nodes.map((node): SiteTreeRow => ({
      id: node.id,
      path: `/${node.path}`,
      title: node.title,
      depth: node.depth,
      source: 'Page',
      ownerDepartment: node.ownerDepartment ?? null,
      visibility: node.visibility ?? 'Public',
      status: node.status ?? null,
      mayEdit: node.mayEdit ?? false,
    })),
    ...screens.map((screen): SiteTreeRow => ({
      id: null,
      path: screen.path,
      title: { [locale]: name(screen.titleKey) },
      depth: depthOf(screen.path),
      source: 'Screen',
      ownerDepartment: null,
      visibility: screen.visibility,
      status: null,
      mayEdit: false,
    })),
  ].sort((left, right) => byAddress(left.path, right.path));
}

/** How far right a choice at that depth is drawn: none at the top of the site, so a choice there is as it always was. */
function levelOf(depth: number): Pick<ChoiceOption, 'level'> {
  return depth > 1 ? { level: depth - 1 } : {};
}

/**
 * The pages a page may be put under, as the choices of a select (design 13 §3.2): in the order of the tree, each pushed
 * right by its level (`level`, drawn as padding: spaces in the label would be read aloud, would stand in front of what
 * the select's typeahead matches and would show in the closed select — the review of #550, item 6), labelled by address and
 * title as they always were. A page at the third level is not one: nothing
 * sits at a fourth. A page written in code is shown and switched off, with the reason (answer 4 of the maintainer): the
 * route of its module would take an address under it before any page could.
 *
 * `own` is the address of the page being placed, without the leading slash: it and the pages under it are not offered.
 */
export function parentChoices(
  nodes: readonly ContentPageNodeDto[],
  screens: readonly SiteScreen[],
  read: (value: LocalizedString | null | undefined) => string,
  name: (key: string) => string,
  noPagesUnder: string,
  own?: string,
): ChoiceOption[] {
  const pages = nodes
    .filter((node) => node.depth < MAX_DEPTH)
    .filter((node) => own === undefined || (node.path !== own && !node.path.startsWith(`${own}/`)))
    .map((node) => ({
      path: `/${node.path}`,
      choice: {
        value: String(node.id),
        label: `/${node.path} — ${read(node.title) || node.path}`,
        ...levelOf(node.depth),
      } satisfies ChoiceOption,
    }));

  const fixed = screens
    // The front page is the top of the site, not a page beside the others to put something under.
    .filter((screen) => screen.path !== '/')
    .map((screen) => ({
      path: screen.path,
      choice: {
        value: `${SCREEN_VALUE}${screen.path}`,
        label: `${screen.path} — ${name(screen.titleKey)} (${noPagesUnder})`,
        disabled: true,
        ...levelOf(depthOf(screen.path)),
      } satisfies ChoiceOption,
    }));

  return [...pages, ...fixed]
    .sort((left, right) => byAddress(left.path, right.path))
    .map((entry) => entry.choice);
}
