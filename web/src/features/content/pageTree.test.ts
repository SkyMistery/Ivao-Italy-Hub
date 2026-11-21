import { expect, test } from 'vitest';

import type { SiteScreen } from '../../shared/api/bootstrap';

import { byAddress, parentChoices, siteTree } from './pageTree';
import type { ContentPageNodeDto } from './queries';

/**
 * The tree of the site (design `13-design-struttura-del-sito.md` §3.2): the pages of the content and the pages written in
 * code in one order, and the choice of the page a page sits under.
 */

function node(path: string, overrides: Partial<ContentPageNodeDto> = {}): ContentPageNodeDto {
  return {
    id: path.length,
    path,
    title: { en: path.split('/').at(-1)!.toUpperCase() },
    depth: path.split('/').length,
    ownerDepartment: 'WD',
    visibility: 'Public',
    status: null,
    mayEdit: false,
    ...overrides,
  };
}

const screens: SiteScreen[] = [
  { path: '/', titleKey: 'menu.screens./', visibility: 'Public' },
  { path: '/tours', titleKey: 'flightops:public.title', visibility: 'Public' },
];

const name = (key: string) => `«${key}»`;
const read = (value: Record<string, string> | null | undefined) => value?.en ?? '';

test('a page comes right before the pages under it, whatever the letters of its neighbours', () => {
  // A comparison of the whole strings would put `/a-b` between `/a` and `/a/b`: `-` comes before `/`.
  expect(['/a/b', '/a-b', '/a', '/'].sort(byAddress)).toEqual(['/', '/a', '/a/b', '/a-b']);
});

test('the tree holds the pages and the pages written in code, in the order of the address', () => {
  const rows = siteTree(
    [node('training/guide', { mayEdit: true, status: 'Draft' }), node('training'), node('about')],
    screens,
    name,
    'en',
  );

  expect(rows.map((row) => row.path)).toEqual(['/', '/about', '/tours', '/training', '/training/guide']);

  const tours = rows.find((row) => row.path === '/tours')!;
  expect(tours).toMatchObject({
    id: null,
    source: 'Screen',
    title: { en: '«flightops:public.title»' },
    depth: 1,
    status: null,
    mayEdit: false,
  });

  const guide = rows.find((row) => row.path === '/training/guide')!;
  expect(guide).toMatchObject({
    source: 'Page',
    depth: 2,
    status: 'Draft',
    mayEdit: true,
    ownerDepartment: 'WD',
  });
});

test('a parent is chosen among the pages above the third level, and a page written in code is shown switched off', () => {
  const choices = parentChoices(
    [node('training'), node('training/guide'), node('training/guide/start'), node('about')],
    screens,
    read,
    name,
    'no pages under it',
  );

  // In the order of the tree; the third level is not offered, nor the front page.
  expect(choices.map((choice) => choice.value)).toEqual([
    String('about'.length),
    'screen:/tours',
    String('training'.length),
    String('training/guide'.length),
  ]);

  // Labelled as they always were, a level pushed right by spaces an accessible name leaves out.
  expect(choices[0]).toEqual({ value: '5', label: '/about — ABOUT' });
  expect(choices[3]!.label).toBe('    /training/guide — GUIDE');
  expect(choices[1]).toEqual({
    value: 'screen:/tours',
    label: '/tours — «flightops:public.title» (no pages under it)',
    disabled: true,
  });
});

test('a page is not offered to go under itself, nor under a page under it', () => {
  const choices = parentChoices(
    [node('training'), node('training/guide'), node('training-old')],
    [],
    read,
    name,
    'no pages under it',
    'training',
  );

  expect(choices.map((choice) => choice.label)).toEqual(['/training-old — TRAINING-OLD']);
});
