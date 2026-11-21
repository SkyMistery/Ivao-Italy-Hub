import { H2, Tabs } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useId, useState, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { describeProblem } from '../../../shared/forms';
import { Notice } from '../../../shared/ui';
import { tourPreviewQuery, type TourDetailDto } from '../api';
import type { TourPageTab } from '../schemas';

import { TourHead } from './TourFace';
import { Overview, RouteTab, TourFacts, TourRulesSection } from './public';
import { openTab, pageTabs } from './tourPage';

/**
 * The preview of a tour (design 11 §5): the page of §4 drawn with the tour as the back office holds it, before its release —
 * the same head and the same tabs the public page draws, from the same `PublicTourDto`, which the server composes for the
 * draft (`/tours/{id}/preview`, the maintainer, 19 November 2026). What belongs to one pilot — «Your tour», their reports, the
 * buttons to report and to plan — is not here: nobody is flying a draft. Its tab is kept on the screen, not in the address,
 * which is the editor's.
 */
export function TourPreview({ tour, identityHref }: { tour: TourDetailDto; identityHref: string }) {
  const { t, i18n } = useTranslation();
  // Without an address or a date there is no page: the server would say so, and the step says where to write them.
  const drawable = tour.slug !== null && tour.releaseAt !== null && tour.closeAt !== null;
  const preview = useQuery({ ...tourPreviewQuery(tour.id), enabled: drawable });
  const page = preview.data;
  // A refusal — the permission gone meanwhile, a row another department took — is said in the core's words, never left
  // as "loading" for ever.
  const refusal = preview.isError ? describeProblem(preview.error, t, i18n.language) : null;
  const [asked, setAsked] = useState<TourPageTab | undefined>(undefined);
  const heading = useId();

  return (
    // Named by its heading: the page inside has its own title, and the editor around it another.
    <section aria-labelledby={heading} className="flex flex-col gap-4">
      <H2 id={heading}>{t('flightops:tours.steps.preview')}</H2>
      {!drawable ? (
        <Notice
          tone="info"
          title={t('flightops:tours.steps.previewNeeds')}
          description={
            <RouterAnchor href={identityHref} className="underline">
              {t('flightops:tours.steps.goTo', { step: t('flightops:tours.steps.identity') })}
            </RouterAnchor>
          }
        />
      ) : preview.isError ? (
        <Notice tone="error" title={refusal ?? t('errors.title')} />
      ) : page === undefined ? (
        <p className="text-muted-foreground text-sm">{t('common.loading')}</p>
      ) : (
        <>
          <p className="text-muted-foreground text-sm">{t('flightops:tours.steps.previewLead')}</p>
          <PreviewPage tour={page} asked={asked} onTab={setAsked} />
        </>
      )}
    </section>
  );
}

/** The page itself, in a frame that sets it apart from the editor around it. */
function PreviewPage({
  tour,
  asked,
  onTab,
}: {
  tour: Parameters<typeof TourHead>[0]['tour'];
  asked: TourPageTab | undefined;
  onTab: (tab: TourPageTab) => void;
}) {
  const { t } = useTranslation();
  // As a visitor sees it: nobody signed in, so no tab of reports.
  const tabs = pageTabs(tour, false, null);
  const tab = openTab(asked, tabs);
  const contents: Partial<Record<TourPageTab, { trigger: string; content: ReactNode }>> = {
    overview: { trigger: t('flightops:public.tabs.overview'), content: <Overview tour={tour} /> },
    route: { trigger: t('flightops:public.tabs.route'), content: <RouteTab tour={tour} mine={undefined} /> },
    rules: {
      trigger: t('flightops:public.tabs.rules'),
      content: (
        <div className="flex flex-col gap-10">
          <TourFacts tour={tour} />
          <TourRulesSection tour={tour} signedIn={false} />
        </div>
      ),
    },
  };

  return (
    <div className="flex flex-col gap-8 rounded-lg border p-4 sm:p-6">
      <TourHead tour={tour} />
      <Tabs
        // ⚠️ `w-full` is not decoration: Atmosphere pins `Tabs` to `w-[400px]` (HANDOFF §13).
        className="w-full"
        value={tab}
        onValueChange={(next) => onTab(next as TourPageTab)}
        tabs={Object.fromEntries(
          tabs.flatMap((key) => {
            const entry = contents[key];
            return entry === undefined
              ? []
              : [[key, { trigger: entry.trigger, content: <div className="pt-6">{entry.content}</div> }]];
          }),
        )}
      />
    </div>
  );
}
