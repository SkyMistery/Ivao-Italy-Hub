import { Lead } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { ContentRenderer, readBody } from '../../blocks';
import { resolveLocalized } from '../../shared/i18n/localized';
import { useLocalized } from '../../shared/i18n/useLocalized';
import { PageMetadata } from '../../shared/seo/PageMetadata';
import { publicContentQuery } from '../content/queries';
import { bootstrapQuery } from '../me/queries';

/**
 * The front page of the division site, which is a published `cms_contents` row and not a screen.
 *
 * That is the whole answer M1 exists to give: what a visitor reads here is what somebody wrote in
 * the editor and published, and this file draws it without knowing a word of it (design M1 §8.2).
 * The row is seeded on the first start, with filler prose that names no division in particular, and
 * whoever runs the site replaces it from the back office.
 *
 * ⚠️ The address `/` is a route of the application and `home` is an ordinary slug, so the two never
 * collide: `/{slug}` serves every other page and this one is reached by the shorter address.
 */
export const HOME_SLUG = 'home';

export function HomePage() {
  const { t, i18n } = useTranslation();
  const read = useLocalized();
  const { data: bootstrap } = useQuery(bootstrapQuery);

  // Not the route's loader: a site whose home has never been published still has to open, and a
  // failure here is a page to draw rather than an error boundary to hit.
  const home = useQuery({ ...publicContentQuery('Page', HOME_SLUG), retry: false });

  if (!home.data) {
    return home.isPending ? null : <Lead>{t('home.empty')}</Lead>;
  }

  return (
    <article className="flex flex-col">
      <PageMetadata
        title={home.data.title}
        description={home.data.summary}
        seo={home.data.seo}
        divisionName={resolveLocalized(
          bootstrap?.division.name,
          i18n.language,
          bootstrap?.division.defaultLocale ?? i18n.language,
        )}
      />

      {/* The title of the row is what a browser tab and a search result use; what the page itself
          shows is whatever heading block the editor put at the top of it. */}
      <h1 className="sr-only">{read(home.data.title)}</h1>
      <ContentRenderer body={readBody(home.data.body)} />
    </article>
  );
}
