import { keepPreviousData, useQuery } from '@tanstack/react-query';
import type { TFunction } from 'i18next';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';

import { holdsPermissionAnywhere, type LocalizedString, type SiteScreen } from '../../shared/api/bootstrap';
import type { Suggestion } from '../../shared/forms';
import { useLocalized } from '../../shared/i18n/useLocalized';
import type { Visibility } from '../../shared/ui';
import { activeLinksQuery, type LinkPage } from '../links/queries';
import { bootstrapQuery } from '../me/queries';

import {
  menuDestinationPagesQuery,
  siteAddressesQuery,
  type ContentKind,
  type ContentPage,
  type ContentSiteAddressDto,
} from './queries';

/**
 * The addresses of the site, read in one place (design `12-design-editor-fase-3.md` §4.5, born in PR 3.0a).
 *
 * Until PR 3.0a this list was built by the screen of a menu entry, and only there. Three more readers
 * want the same list: the instructions «Download for Claude» (the addresses an assistant may link to),
 * the address fields of the blocks (PR 3.5) and the tree of the site (phase 4). A second copy of it
 * would be a second idea of where the site leads, so it lives here and the menu reads it from here.
 *
 * Three kinds of address, in the order the menu has always offered them:
 *
 * - **the rows of the content**, grouped by the department that wrote them. Two reads give them: the menu's
 *   (`pagesOfTheMenu`), pages only and drafts included and said so — a menu entry is written before the page
 *   goes out, and waits switched off —; and the site's (`pagesOfTheSite`, `GET /api/content/addresses`), every
 *   page, news item and document online that the reader may read, of every department
 *   (`decisions/2026-11-04-gli-indirizzi-del-sito-che-si-possono-leggere.md`), for the instructions for Claude,
 *   the page of the style and the address fields of the blocks;
 * - **the screens of the application**, which are routes and not rows: the server declares them, the core's and the
 *   enabled modules', and the bootstrap carries them (`registries.screens`, design `13-design-struttura-del-sito.md` §3.1);
 * - **the links of the library**, the only addresses outside the site a menu entry may lead to
 *   (decided 8 September 2026).
 */

/** One row of the content, from either read, as the list is built from it. */
export interface SitePage {
  readonly id: number;
  readonly kind: ContentKind;
  /** Where it is read: `/about/team`, `/news/<slug>`, `/documents/<slug>`. */
  readonly url: string;
  readonly title: LocalizedString;
  /** What it is called when it has no title in the language on screen. */
  readonly fallback: string;
  readonly ownerDepartment: string;
  readonly visibility: Visibility;
  readonly published: boolean;
}

/** The menu's read: the pages of the reader's departments, drafts included. */
export function pagesOfTheMenu(list: ContentPage | undefined): SitePage[] {
  return (list?.items ?? []).map((page) => ({
    id: page.id,
    kind: page.kind,
    url: `/${page.path}`,
    title: page.title,
    fallback: page.slug,
    ownerDepartment: page.ownerDepartment,
    visibility: page.visibility,
    published: page.publishedAt !== null,
  }));
}

/** The site's read: every page, news item and document online that the reader may read, of every department. */
export function pagesOfTheSite(rows: readonly ContentSiteAddressDto[] | undefined): SitePage[] {
  return (rows ?? []).map((row) => ({
    id: row.id,
    kind: row.kind,
    url: row.url,
    title: row.title,
    fallback: row.url,
    ownerDepartment: row.ownerDepartment,
    visibility: row.visibility,
    published: true,
  }));
}

/**
 * Where an address of this site may lead, for the import of a file (note 2026-10-30-una-pagina-scritta-con-claude §3.5):
 * the addresses the instructions listed, the rows of the site's read and the screens.
 */
export function knownAddresses(
  rows: readonly ContentSiteAddressDto[],
  screens: readonly SiteScreen[],
): ReadonlySet<string> {
  return new Set<string>([...screens.map((screen) => screen.path), ...rows.map((row) => row.url)]);
}

/** One address of the site, with what a screen needs to offer it and what the instructions say of it. */
export interface SiteAddress {
  /** The row, for an address that is one; null for a screen or a link. */
  readonly id: number | null;
  /** Where it leads: `/about/team` for a page or a screen, the whole URL for a link of the library. */
  readonly path: string;
  /** Its name in the language on screen; a draft says so. */
  readonly label: string;
  /** The heading it is offered under: the department of a page, «Screens», «Links». */
  readonly group: string;
  readonly kind: 'page' | 'news' | 'document' | 'screen' | 'link';
  /**
   * Whether a visitor can open it now. A page that was never published is offered to the menu and
   * not to the instructions; a page published once and being edited again is both.
   */
  readonly published: boolean;
  /** Who reads it. A screen of the application is for everybody. */
  readonly visibility: Visibility;
}

/** The list, from the two reads it is made of. Kept apart from the hook so that a download can read it once. */
export function siteAddresses(
  pages: readonly SitePage[],
  links: LinkPage | undefined,
  t: TFunction,
  read: (value: LocalizedString | null | undefined) => string,
  screens: readonly SiteScreen[],
): SiteAddress[] {
  return [
    ...pages.map((page): SiteAddress => ({
      id: page.id,
      path: page.url,
      // «(draft)» for a page that was never published, not for one online whose draft is being written again: that one
      // is an address a visitor can open (review of #449, finding 7).
      label: page.published
        ? read(page.title) || page.fallback
        : t('menu.draftSuffix', { title: read(page.title) || page.fallback }),
      group: t(`departments.${page.ownerDepartment}`),
      kind: page.kind === 'News' ? 'news' : page.kind === 'Document' ? 'document' : 'page',
      published: page.published,
      visibility: page.visibility,
    })),
    ...screens.map((screen): SiteAddress => ({
      id: null,
      path: screen.path,
      label: t(screen.titleKey),
      group: t('menu.screensGroup'),
      kind: 'screen',
      published: true,
      visibility: screen.visibility,
    })),
    ...(links?.items ?? []).map((link): SiteAddress => ({
      id: null,
      path: link.url,
      label: read(link.title) || link.url,
      group: t('menu.linksGroup'),
      kind: 'link',
      published: true,
      visibility: link.visibility,
    })),
  ];
}

/** The list as the suggestions of a field of the form generator. */
export function asSuggestions(addresses: readonly SiteAddress[]): Suggestion[] {
  return addresses.map((address) => ({ value: address.path, label: address.label, group: address.group }));
}

/**
 * What each read of `useAddressesToLinkTo` asks at the door. The site's addresses ask `Content.Edit`, the links of the
 * library `Links.View`: a read the person may not make is not made, and the list goes on without it.
 */
const CONTENT_EDIT = 'Content.Edit';
export const LINKS_VIEW = 'Links.View';

/**
 * The addresses a block may lead to, for its address fields (PR 3.5, design §4.5): the site's read, of every department
 * and unpaged, so the field filters what it holds and has nothing to ask the server while somebody types. A draft is not
 * offered: it is an address nobody can open yet, and the field stays open to write it in full.
 *
 * ⚠️ Whoever writes a page of a module without `Content.Edit` — the briefing of a tour — is offered the screens and, with
 * `Links.View`, the links. They write the address of a page in full, as they always did.
 *
 * Asked only by a block that has such a property (`wanted`). The same array until an answer arrives, so the schema made
 * from it is not made again at every keystroke.
 */
export function useAddressesToLinkTo(wanted: boolean): readonly SiteAddress[] {
  const { t } = useTranslation();
  const read = useLocalized();
  const { data: bootstrap } = useQuery(bootstrapQuery);
  const mayRead = (permission: string) =>
    wanted && bootstrap !== undefined && holdsPermissionAnywhere(bootstrap, permission);

  const pages = useQuery({ ...siteAddressesQuery(), enabled: mayRead(CONTENT_EDIT) });
  const links = useQuery({ ...activeLinksQuery(), enabled: mayRead(LINKS_VIEW) });

  const screens = bootstrap?.registries.screens;

  return useMemo(
    () => siteAddresses(pagesOfTheSite(pages.data), links.data, t, read, screens ?? []),
    [pages.data, links.data, t, read, screens],
  );
}

/**
 * The addresses of the site, for a field that offers them while somebody types.
 *
 * ⚠️ **It takes what is being typed.** Both reads are a page of a hundred rows — the ceiling of the list
 * engine — so a site with more pages than that has pages no list in hand can offer: what is typed is a
 * question to the server rather than a filter over rows already read (decided 9 September 2026).
 * `keepPreviousData` so the list does not blink empty between a keystroke and the answer: an empty
 * popover reads as "nothing matches", which is the one thing it must not say while asking. And
 * `searching` while either answer is on its way, so that a list the old page leaves empty says so (#384).
 */
export function useSiteAddresses(typed = ''): { addresses: SiteAddress[]; searching: boolean } {
  const { t } = useTranslation();
  const read = useLocalized();

  const { data: bootstrap } = useQuery(bootstrapQuery);
  const pages = useQuery({ ...menuDestinationPagesQuery(typed), placeholderData: keepPreviousData });
  const links = useQuery({ ...activeLinksQuery(typed), placeholderData: keepPreviousData });

  return {
    addresses: siteAddresses(
      pagesOfTheMenu(pages.data),
      links.data,
      t,
      read,
      bootstrap?.registries.screens ?? [],
    ),
    searching: pages.isFetching || links.isFetching,
  };
}
