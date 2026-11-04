import type { TFunction } from 'i18next';
import { expect, test } from 'vitest';

import type { LinkPage } from '../links/queries';

import type { ContentPage } from './queries';
import { asSuggestions, SITE_SCREENS, siteAddresses } from './siteAddresses';

/** The words of the screen, as their keys: what is tested is which key goes where, not a language. */
const t = ((key: string, values?: Record<string, string>) =>
  values === undefined ? key : `${key}(${Object.values(values).join(',')})`) as unknown as TFunction;
const read = (value: Record<string, string> | null | undefined) => value?.en ?? '';

function page(overrides: Partial<ContentPage['items'][number]>): ContentPage['items'][number] {
  return {
    id: 1,
    kind: 'Page',
    slug: 'team',
    parentId: null,
    path: 'about/team',
    ownerDepartment: 'Web',
    visibility: 'Public',
    status: 'Published',
    isTemplate: false,
    title: { en: 'Team' },
    collections: [],
    coverMediaId: null,
    pinned: false,
    sort: 0,
    fileMediaId: null,
    reviewOn: null,
    retiredAt: null,
    supersededById: null,
    publishedAt: '2026-10-01T00:00:00Z',
    updatedAt: '2026-10-01T00:00:00Z',
    ...overrides,
  } as ContentPage['items'][number];
}

const pages = (items: ContentPage['items']) => ({ items, total: items.length, page: 1, pageSize: 100 });

test('pages, then the screens of the application, then the links of the library', () => {
  const links = {
    items: [{ id: 3, url: 'https://forum.example', title: { en: 'Forum' }, visibility: 'Members' }],
  } as unknown as LinkPage;

  const list = siteAddresses(pages([page({})]), links, t, read);

  expect(list.map((address) => address.kind)).toEqual(['page', ...SITE_SCREENS.map(() => 'screen'), 'link']);
  expect(list[0]).toEqual({
    path: '/about/team',
    label: 'Team',
    group: 'departments.Web',
    kind: 'page',
    published: true,
    visibility: 'Public',
  });
  expect(list.at(-1)).toMatchObject({
    path: 'https://forum.example',
    group: 'menu.linksGroup',
    visibility: 'Members',
  });
});

test('a draft says so, and a page never published is not published', () => {
  const [draft] = siteAddresses(pages([page({ status: 'Draft', publishedAt: null })]), undefined, t, read);

  expect(draft).toMatchObject({ label: 'menu.draftSuffix(Team)', published: false });
});

test('a published page being edited again is still an address a visitor can open', () => {
  const [edited] = siteAddresses(pages([page({ status: 'Draft' })]), undefined, t, read);

  expect(edited?.published).toBe(true);
});

test('a page with no title in the language on screen is named by its slug', () => {
  const [untitled] = siteAddresses(pages([page({ title: { it: 'Squadra' } })]), undefined, t, read);

  expect(untitled?.label).toBe('team');
});

test('the suggestions of a field are the path, the name and the group', () => {
  const [first] = asSuggestions(siteAddresses(pages([page({})]), undefined, t, read));

  expect(first).toEqual({ value: '/about/team', label: 'Team', group: 'departments.Web' });
});
