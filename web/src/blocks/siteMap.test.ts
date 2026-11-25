import { describe, expect, test } from 'vitest';

import { siteMapTree, type SiteMapEntry, type SiteMapNode } from './siteMap';

/** The tree as lines, indented by level, so that a failure reads as the map would. */
function lines(nodes: readonly SiteMapNode[], depth = 0): string[] {
  return nodes.flatMap((node) => [
    `${'  '.repeat(depth)}${node.name} ${node.href}`,
    ...lines(node.children, depth + 1),
  ]);
}

const page = (path: string, en: string): SiteMapEntry => ({ path, title: { en }, titleKey: null });
const screen = (path: string, key: string): SiteMapEntry => ({ path, title: null, titleKey: key });

const names: Record<string, string> = {
  'menu.screens./': 'Home screen',
  'menu.screens./calendar': 'Calendar',
  'flightops:public.title': 'Tours',
};

const name = (entry: SiteMapEntry) =>
  entry.titleKey ? (names[entry.titleKey] ?? '') : (entry.title?.en ?? '');

describe('the map of the site', () => {
  test('a page sits under its mother, and sisters are in the order of their names', () => {
    const tree = siteMapTree(
      [
        page('/about', 'About'),
        page('/about/team', 'The staff'),
        page('/about/history', 'History'),
        screen('/calendar', 'menu.screens./calendar'),
        screen('/tours', 'flightops:public.title'),
        page('/academy', 'Academy'),
      ],
      name,
      'en',
    );

    expect(lines(tree)).toEqual([
      'About /about',
      '  History /about/history',
      '  The staff /about/team',
      'Academy /academy',
      'Calendar /calendar',
      'Tours /tours',
    ]);
  });

  test('the front page is one line, at the top, under the title of its row', () => {
    const tree = siteMapTree(
      [
        screen('/calendar', 'menu.screens./calendar'),
        screen('/', 'menu.screens./'),
        page('/home', 'Welcome'),
      ],
      name,
      'en',
    );

    expect(lines(tree)).toEqual(['Welcome /', 'Calendar /calendar']);
  });

  test('without a row of the front page the screen is the line', () => {
    const tree = siteMapTree([page('/about', 'About'), screen('/', 'menu.screens./')], name, 'en');

    expect(lines(tree)).toEqual(['Home screen /', 'About /about']);
  });

  test('a page whose mother the reader may not open is drawn under the nearest page they may', () => {
    // `/about/team` is for the staff, and this reader is a visitor: its public daughter stays on the map.
    const tree = siteMapTree([page('/about', 'About'), page('/about/team/roles', 'Roles')], name, 'en');

    expect(lines(tree)).toEqual(['About /about', '  Roles /about/team/roles']);
  });

  test('the order is the language on screen', () => {
    // In Swedish `ä` comes after `z`; in English beside `a`.
    const entries = [page('/zeta', 'Zeta'), page('/aether', 'Äther')];

    expect(lines(siteMapTree(entries, name, 'en'))).toEqual(['Äther /aether', 'Zeta /zeta']);
    expect(lines(siteMapTree(entries, name, 'sv'))).toEqual(['Zeta /zeta', 'Äther /aether']);
  });

  test('an entry without an address is left out, and a name left empty is the address', () => {
    const tree = siteMapTree([{ title: { en: 'Nowhere' } }, page('/blank', '')], name, 'en');

    expect(lines(tree)).toEqual(['/blank /blank']);
  });
});
