import { useTranslation } from 'react-i18next';

import type { LocalizedString } from '../api/bootstrap';
import { mediaFileUrl } from '../api/mediaUrl';
import { resolveLocalized } from '../i18n/localized';

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
 * nothing at all.
 */
export function PageMetadata({
  title,
  description,
  seo,
  imageMediaId,
  divisionName,
}: {
  /** The title of the row, translated. */
  title: LocalizedString | null | undefined;
  /** The summary of the row, when it has one. */
  description?: LocalizedString | null;
  /** The `seo` column: `{ title, description, ogImageMediaId }` per language. */
  seo?: Record<string, unknown> | null;
  /** A cover, when the row has one and `seo` names no picture of its own. */
  imageMediaId?: number | null;
  /** What the site is called, so a tab says which site it belongs to. */
  divisionName: string;
}) {
  const { i18n } = useTranslation();
  const locale = i18n.language;

  // The `seo` column is opaque to the server by design (plan §16.5), so its shape is read here,
  // where the schema that writes it also lives.
  const entry = (seo?.[locale] ?? seo?.[locale.split('-')[0] ?? locale] ?? {}) as Record<string, unknown>;

  const own = typeof entry.title === 'string' && entry.title.trim() !== '' ? entry.title : null;
  const rowTitle = resolveLocalized(title, locale, locale);
  const pageTitle = own ?? rowTitle;

  const summary =
    typeof entry.description === 'string' && entry.description.trim() !== ''
      ? entry.description
      : resolveLocalized(description, locale, locale);

  const picture = typeof entry.ogImageMediaId === 'number' ? entry.ogImageMediaId : (imageMediaId ?? null);

  // "Page — Division", and the division alone when the page has no title of its own to put first.
  const documentTitle = pageTitle === '' ? divisionName : `${pageTitle} — ${divisionName}`;

  return (
    <>
      <title>{documentTitle}</title>
      {summary === '' ? null : <meta name="description" content={summary} />}

      <meta property="og:type" content="website" />
      <meta property="og:site_name" content={divisionName} />
      <meta property="og:title" content={pageTitle === '' ? divisionName : pageTitle} />
      {summary === '' ? null : <meta property="og:description" content={summary} />}
      {picture === null ? null : <meta property="og:image" content={mediaFileUrl(picture)} />}
    </>
  );
}
