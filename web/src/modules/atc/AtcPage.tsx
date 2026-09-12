import { Card, H2, Lead } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { ContentRenderer, EmbeddingContext, readBody, usePublishedEmbedding } from '../../blocks';
import { publicContentQuery } from '../../features/content/queries';
import { useLocalized } from '../../shared/i18n/useLocalized';

/**
 * The public page of ATC operations, and it is two things joined at one address (design M1 §8.2):
 * the **system page** `atc`, which the staff writes in the editor like every other page, and the
 * **cards** this module adds under it, which lead to what still lives outside the hub.
 *
 * The dependency runs the way it is allowed to run: a module reads the core, never the other way
 * round. What the core knows about this file is a path and a component, out of the manifest.
 *
 * ⚠️ The addresses of the cards are the ones this module already declares as its own in
 * `AtcModule.SpaFallbackExclusions` — the application hands those paths back to the server rather
 * than drawing over them — so they are read from one list here too. Which of them a controller
 * should actually be sent to is for the department to say; the mechanism is what this phase owes.
 */

/**
 * What lives behind the same host and is not this application (`AtcModule.SpaFallbackExclusions`).
 * The keys are written out rather than built from the path: a key put together at run time is one
 * `pnpm i18n:check` cannot see, and a screen showing `atc.services.vsop.title` is what that costs.
 */
const LEGACY_SERVICES = [
  { path: '/vsop', title: 'atc.services.vsop.title', text: 'atc.services.vsop.text' },
  { path: '/services/vsop', title: 'atc.services.legacy.title', text: 'atc.services.legacy.text' },
] as const;

/** The slug of the system page this module puts its own cards under. */
const PAGE_SLUG = 'atc';

export function AtcPage() {
  const { t } = useTranslation();
  const read = useLocalized();

  const page = useQuery({ ...publicContentQuery('Page', PAGE_SLUG), retry: false });
  const embedding = usePublishedEmbedding(page.data);

  return (
    <div className="flex flex-col gap-8">
      {page.data ? (
        <article className="flex flex-col">
          <h1 className="sr-only">{read(page.data.title)}</h1>
          <EmbeddingContext.Provider value={embedding}>
            <ContentRenderer body={readBody(page.data.body)} />
          </EmbeddingContext.Provider>
        </article>
      ) : (
        // The module still draws its own half when the page has not been written yet: what belongs
        // to the module does not depend on somebody having published anything.
        page.isPending || <Lead>{t('atc.lead')}</Lead>
      )}

      <section className="flex flex-col gap-4">
        <H2>{t('atc.services.title')}</H2>

        <div className="grid gap-4 sm:grid-cols-2">
          {LEGACY_SERVICES.map((service) => (
            // A full navigation and not a router link: these addresses are served by something
            // else behind the same host, which is the whole reason the module excludes them from
            // the fallback of the single page application.
            <a key={service.path} href={service.path} className="block">
              {/* `Card` takes its parts as props and not as children: the title and the body are
                  named, which is measured rather than assumed (handoff §13, the fourth contract of
                  Atmosphere that had to be read before it could be used). */}
              <Card className="h-full" title={t(service.title)} content={t(service.text)} />
            </a>
          ))}
        </div>
      </section>
    </div>
  );
}
