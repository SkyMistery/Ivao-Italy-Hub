import { keepPreviousData, useQuery } from '@tanstack/react-query';
import type { TFunction } from 'i18next';
import { useTranslation } from 'react-i18next';

import type { LocalizedString } from '../../shared/api/bootstrap';
import type { Suggestion } from '../../shared/forms';
import { useLocalized } from '../../shared/i18n/useLocalized';
import type { Visibility } from '../../shared/ui';
import { activeLinksQuery, type LinkPage } from '../links/queries';

import { menuDestinationPagesQuery, type ContentPage } from './queries';

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
 * - **the pages**, grouped by the department that wrote them, drafts included and said so — a menu entry
 *   is written before the page goes out, and waits switched off;
 * - **the screens of the application**, which are routes and not rows;
 * - **the links of the library**, the only addresses outside the site a menu entry may lead to
 *   (decided 8 September 2026).
 */

/**
 * The addresses of this application that are not rows: they are routes, so they cannot be read from
 * a table and they are written here. A fork that adds a screen adds a line; a fork that removes one
 * removes a line, and nothing of that installation offers an address it does not have.
 *
 * ⚠️ The other half is `MenuItemWriteDtoValidator.Screens`, and the two agree **by hand** — a
 * route of this client is not something the contract can carry. An integration test posts a screen
 * the server does not know, exactly as one does for the backgrounds of a section.
 */
export const SITE_SCREENS = ['/', '/calendar', '/news', '/documents', '/search', '/contact'] as const;

/** One address of the site, with what a screen needs to offer it and what the instructions say of it. */
export interface SiteAddress {
  /** Where it leads: `/about/team` for a page or a screen, the whole URL for a link of the library. */
  readonly path: string;
  /** Its name in the language on screen; a draft says so. */
  readonly label: string;
  /** The heading it is offered under: the department of a page, «Screens», «Links». */
  readonly group: string;
  readonly kind: 'page' | 'screen' | 'link';
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
  pages: ContentPage | undefined,
  links: LinkPage | undefined,
  t: TFunction,
  read: (value: LocalizedString | null | undefined) => string,
): SiteAddress[] {
  return [
    ...(pages?.items ?? []).map((page): SiteAddress => ({
      path: `/${page.path}`,
      label:
        page.status === 'Draft'
          ? t('menu.draftSuffix', { title: read(page.title) || page.slug })
          : read(page.title) || page.slug,
      group: t(`departments.${page.ownerDepartment}`),
      kind: 'page',
      published: page.publishedAt !== null,
      visibility: page.visibility,
    })),
    ...SITE_SCREENS.map((path): SiteAddress => ({
      path,
      label: t(`menu.screens.${path}`),
      group: t('menu.screensGroup'),
      kind: 'screen',
      published: true,
      visibility: 'Public',
    })),
    ...(links?.items ?? []).map((link): SiteAddress => ({
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
 * The addresses of the site, for a field that offers them while somebody types.
 *
 * ⚠️ **It takes what is being typed.** Both reads are a page of a hundred rows — the ceiling of the list
 * engine — so a site with more pages than that has pages no list in hand can offer: what is typed is a
 * question to the server rather than a filter over rows already read (decided 9 September 2026).
 * `keepPreviousData` so the list does not blink empty between a keystroke and the answer: an empty
 * popover reads as "nothing matches", which is the one thing it must not say while asking.
 */
export function useSiteAddresses(typed = ''): SiteAddress[] {
  const { t } = useTranslation();
  const read = useLocalized();

  const pages = useQuery({ ...menuDestinationPagesQuery(typed), placeholderData: keepPreviousData });
  const links = useQuery({ ...activeLinksQuery(typed), placeholderData: keepPreviousData });

  return siteAddresses(pages.data, links.data, t, read);
}
