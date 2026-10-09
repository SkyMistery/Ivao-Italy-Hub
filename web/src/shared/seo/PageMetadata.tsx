import { createContext, useContext, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import type { LocalizedString } from '../api/bootstrap';
import { mediaFileUrl } from '../api/mediaUrl';
import { resolveLocalized } from '../i18n/localized';

/** The division's name in the language on screen, said once by `DivisionTitle` for the tree under it. */
const DivisionName = createContext('');

/**
 * What a browser tab, a search result and a link pasted into a chat show of a page (design M1 §8.4).
 *
 * It draws nothing. React 19 hoists a `<title>` or a `<meta>` rendered anywhere in the tree into the
 * document head, so the page that knows its own title says so where it is drawn, and no screen has
 * to remember to clean up after itself when the reader navigates away.
 *
 * ⚠️ This is the whole of it, and the limit is worth writing down rather than discovering: **there
 * is no prerendering** (plan §16.11), so a crawler that does not run JavaScript sees the tags of
 * `index.html`. What this buys is the readers who share a link in a client that does execute the
 * page, and an honest `<title>` for the person with twenty tabs open. `sitemap.xml` is the half a
 * crawler always sees, and it is served by the server.
 *
 * The words come from the `seo` column when the editor filled it in, from the row itself when they
 * did not: a page nobody wrote a description for is better described by its own summary than by
 * nothing at all. A page that is not a row — a list, the page that is not there — hands its own
 * sentences of the language files instead, already translated (#224).
 */
export function PageMetadata({
  title,
  description,
  seo,
  imageMediaId,
  divisionName,
}: {
  /** The title of the row, translated; or the sentence a page that is not a row is called by. */
  title: LocalizedString | string | null | undefined;
  /** The summary of the row, when it has one; or the sentence under the title of a page. */
  description?: LocalizedString | string | null;
  /** The `seo` column: `{ title, description, ogImageMediaId }` per language. */
  seo?: Record<string, unknown> | null;
  /** A cover, when the row has one and `seo` names no picture of its own. */
  imageMediaId?: number | null;
  /**
   * What the site is called, so a tab says which site it belongs to. A page need not pass it: the
   * root of the tree says it for every page (`DivisionTitle`, #224). It stays for the pages written
   * before the root did, which pass the same name.
   */
  divisionName?: string;
}) {
  const { i18n } = useTranslation();
  const locale = i18n.language;
  const fromRoot = useContext(DivisionName);
  const division = divisionName ?? fromRoot;

  // The `seo` column is opaque to the server by design (plan §16.5), so its shape is read here,
  // where the schema that writes it also lives.
  const entry = (seo?.[locale] ?? seo?.[locale.split('-')[0] ?? locale] ?? {}) as Record<string, unknown>;

  const read = (value: LocalizedString | string | null | undefined) =>
    typeof value === 'string' ? value : resolveLocalized(value, locale, locale);

  const own = typeof entry.title === 'string' && entry.title.trim() !== '' ? entry.title : null;
  const pageTitle = own ?? read(title);

  const summary =
    typeof entry.description === 'string' && entry.description.trim() !== ''
      ? entry.description
      : read(description);

  const picture = typeof entry.ogImageMediaId === 'number' ? entry.ogImageMediaId : (imageMediaId ?? null);

  // "Page — Division", and either alone when the other is missing: a page with no title of its own
  // is the division's, and a tree with no division to name says the page alone.
  const documentTitle = [pageTitle, division].filter((part) => part !== '').join(' — ');

  return (
    <>
      {documentTitle === '' ? null : <title>{documentTitle}</title>}
      {summary === '' ? null : <meta name="description" content={summary} />}

      <meta property="og:type" content="website" />
      {division === '' ? null : <meta property="og:site_name" content={division} />}
      <meta property="og:title" content={pageTitle === '' ? division : pageTitle} />
      {summary === '' ? null : <meta property="og:description" content={summary} />}
      {picture === null ? null : <meta property="og:image" content={mediaFileUrl(picture)} />}
    </>
  );
}

/**
 * The tab of every page that says nothing of itself, and the name every page that does ends with:
 * the division's (#224). Mounted once, by the root of the route tree, above every layout and the
 * page that is not there.
 *
 * Before it a list, a form of the back office and the page that is not there kept the `<title>` of
 * `index.html` — the product's name, the same in every tab of every division — while a page of the
 * site said "Page — Division": with a few tabs open, the first ones could not be told apart.
 *
 * ⚠️ It works because of where it stands, and the order is React's and not the browser's. React 19
 * puts a `<title>` it mounts **before** the titles already in the head (`mountHoistable` in
 * react-dom), and a browser shows the first one of the document. This one is mounted above every
 * page, so it goes in first: the title of a page below, mounted after it, is the one a tab shows
 * while that page is drawn, and this one is what is left when it goes. A default mounted by a page
 * itself, or by anything below one, would take a page's tab from it.
 */
export function DivisionTitle({
  division,
  children,
}: {
  division: { name: LocalizedString; defaultLocale: string };
  children: ReactNode;
}) {
  // No suspense, like the language above every route: a title must never hold the tree up. And no
  // language yet is the division's own.
  const { i18n } = useTranslation(undefined, { useSuspense: false });
  const current = i18n.language as string | undefined;
  const name = resolveLocalized(division.name, current ?? division.defaultLocale, division.defaultLocale);

  return (
    <DivisionName.Provider value={name}>
      {name === '' ? null : <title>{name}</title>}
      {children}
    </DivisionName.Provider>
  );
}
