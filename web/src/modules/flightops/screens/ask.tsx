import { Button, H1, Lead } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate, useParams, useSearch } from '@tanstack/react-router';
import { useMemo } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { SchemaForm } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import { NotFound } from '../../../shared/ui';
import {
  myTourQuery,
  publicTourQuery,
  useAskClarification,
  type MyTourDto,
  type PublicTourDto,
} from '../api';
import { clarificationSchema, type AskSearch, type ClarificationValues } from '../schemas';

import { clarificationChoices, initialReferences } from './reporting';

/**
 * «Explain this to me» (design M2 §3.10; note 2026-09-23-contestazioni-chiarimenti-segnalazioni §2): a page of its own, like
 * the report, reached from a decided report, a rule or the tour itself. The object the pilot came from is ticked already; the
 * others of the tour are there to add — their decided reports, the legs, the rules in force. It changes nothing of any of
 * them and does not count as a dispute.
 *
 * It is a message of the core's contacts, of the kind `clarification`, to the tour's department: the thread it opens is in
 * `/me/contacts`, where the pilot lands after sending it, and the validator of a cited report takes part in it.
 */
export function AskPage() {
  const { t } = useTranslation();
  const { slug = '' } = useParams({ strict: false });
  const search: AskSearch = useSearch({ strict: false });

  const tour = useQuery(publicTourQuery(slug));
  const tourId = tour.data?.id;
  const mine = useQuery({ ...myTourQuery(tourId ?? 0), enabled: tourId !== undefined });

  if (tour.isPending || (tourId !== undefined && mine.isPending)) {
    return (
      <p className="text-muted-foreground mx-auto w-full max-w-3xl px-4 py-10 text-sm">
        {t('common.loading')}
      </p>
    );
  }

  if (!tour.data || mine.data === undefined) {
    return <NotFound />;
  }

  return <AskScreen tour={tour.data} mine={mine.data} search={search} />;
}

function AskScreen({ tour, mine, search }: { tour: PublicTourDto; mine: MyTourDto; search: AskSearch }) {
  const { t } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();
  const navigate = useNavigate();
  const ask = useAskClarification();
  const back = `/tours/${tour.slug}`;
  const title = read(tour.title);

  const choices = useMemo(
    () =>
      clarificationChoices(tour, mine.reports, {
        report: (report) => {
          const leg = tour.legs.find((entry) => entry.id === report.legId);
          const route =
            leg === undefined
              ? t('flightops:review.route', { from: report.departureIcao, to: report.arrivalIcao })
              : t('flightops:report.leg', {
                  number: leg.number,
                  from: report.departureIcao,
                  to: report.arrivalIcao,
                });

          return t('flightops:ask.report', {
            route,
            date: moment(report.takeoffAt, { time: false }),
            status: t(`flightops:public.reportStatus.${report.status}`),
          });
        },
        leg: (leg) =>
          t('flightops:report.leg', { number: leg.number, from: leg.departureIcao, to: leg.arrivalIcao }),
        rule: (rule) => `${rule.code} ${read(rule.title)}`,
      }),
    [tour, mine.reports, t, moment, read],
  );

  const defaults: ClarificationValues = {
    subject: t('flightops:ask.subject', { tour: title }),
    body: '',
    references: initialReferences(search, tour.id, choices),
  };

  return (
    <article className="mx-auto flex w-full max-w-3xl flex-col gap-8 px-4 py-10">
      <header className="flex flex-col gap-2">
        <RouterAnchor href={back} className="text-sm underline">
          {title}
        </RouterAnchor>
        <H1>{t('flightops:ask.title')}</H1>
        <Lead>{t('flightops:ask.description')}</Lead>
      </header>

      <SchemaForm<ClarificationValues>
        schema={clarificationSchema(choices)}
        defaults={defaults}
        locales={[]}
        labels="flightops:ask"
        onSubmit={async (values) => {
          const sent = await ask.mutateAsync({
            department: tour.department,
            subject: values.subject,
            body: values.body,
            references: values.references.map((sourceId) => ({ sourceModule: 'flightops', sourceId })),
          });
          void navigate({ href: `/me/contacts/${sent.id}` });
        }}
        submitLabel={t('flightops:ask.send')}
        secondaryAction={
          <Button asChild variant="ghost">
            <RouterAnchor href={back}>{t('common.cancel')}</RouterAnchor>
          </Button>
        }
      />
    </article>
  );
}
