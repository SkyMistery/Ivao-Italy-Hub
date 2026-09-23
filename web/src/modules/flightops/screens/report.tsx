import {
  Button,
  Checkbox,
  H1,
  H2,
  Label,
  Lead,
  RadioGroupItem,
  RadioGroupRoot,
} from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate, useParams, useSearch } from '@tanstack/react-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { ApiError } from '../../../shared/api/problem';
import { NEW_ROW_VERSION } from '../../../shared/api/rowVersion';
import { SchemaForm, describeProblem } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import { NotFound, Notice, useNotice } from '../../../shared/ui';
import {
  myTourQuery,
  publicTourQuery,
  reportQuery,
  trackerSessionsQuery,
  useSendReport,
  type MyTourDto,
  type PirepDto,
  type PirepWriteDto,
  type PublicLegDto,
  type PublicTourDto,
  type SessionSearch,
  type TrackerSessionDto,
} from '../api';
import {
  diversionSchema,
  reportDetailsSchema,
  type DiversionValues,
  type ReportDetailsValues,
  type ReportSearch,
} from '../schemas';

import { mergeSessions, sessionsOf, splitRefusal } from './reporting';

/**
 * The pilot's report (design M2 §3.2, §8.1), a page of its own at `/tours/{slug}/report` (note `2026-09-23-il-pirep` §4):
 * a direct link, the back button, a reload half way. First the flight — the pilot's sessions the tracker has between
 * the leg's airports, and a second one when the flight ended elsewhere — then the fields.
 *
 * `?leg=` is the leg, and the next one when it is missing; `?report=` is a report «to modify» sent again, on its own
 * leg and starting from its own flights. Every rule is the server's: this page offers what `…/reports/mine` says may be
 * flown and shows each refusal where it belongs.
 */
export function ReportPage() {
  const { t } = useTranslation();
  const { slug = '' } = useParams({ strict: false });
  const search: ReportSearch = useSearch({ strict: false });
  const correcting = search.report ?? null;

  const tour = useQuery(publicTourQuery(slug));
  const tourId = tour.data?.id;
  const mine = useQuery({ ...myTourQuery(tourId ?? 0), enabled: tourId !== undefined });
  const report = useQuery({ ...reportQuery(correcting ?? 0), enabled: correcting !== null });

  if (
    tour.isPending ||
    (tourId !== undefined && mine.isPending) ||
    (correcting !== null && report.isPending)
  ) {
    return (
      <p className="text-muted-foreground mx-auto w-full max-w-3xl px-4 py-10 text-sm">
        {t('common.loading')}
      </p>
    );
  }

  if (
    !tour.data ||
    mine.data === undefined ||
    (correcting !== null && (report.data === undefined || report.data.tourId !== tour.data.id))
  ) {
    return <NotFound />;
  }

  return (
    <ReportScreen
      tour={tour.data}
      mine={mine.data}
      report={report.data ?? null}
      wanted={search.leg ?? null}
    />
  );
}

function ReportScreen({
  tour,
  mine,
  report,
  wanted,
}: {
  tour: PublicTourDto;
  mine: MyTourDto;
  report: PirepDto | null;
  wanted: number | null;
}) {
  const { t } = useTranslation();
  const read = useLocalized();
  const back = `/tours/${tour.slug}`;
  const isOpen = tour.kind === 'Open';

  const legId = report !== null ? report.legId : isOpen ? null : (wanted ?? mine.next);
  const leg = tour.legs.find((entry) => entry.id === legId) ?? null;

  let refusal: string | null = null;
  if (report !== null && report.status !== 'ToModify') {
    refusal = t('flightops:errors.reportNotCorrectable');
  } else if (report === null && mine.blocked !== null) {
    refusal = t(mine.blocked);
  } else if (report === null && !isOpen && (leg === null || !mine.flyable.includes(leg.id))) {
    refusal = t(mine.flyable.length === 0 ? 'flightops:report.nothingToFly' : 'flightops:report.notFlyable');
  }

  return (
    <article className="mx-auto flex w-full max-w-3xl flex-col gap-8 px-4 py-10">
      <header className="flex flex-col gap-2">
        <RouterAnchor href={back} className="text-sm underline">
          {read(tour.title)}
        </RouterAnchor>
        <H1>{t(report === null ? 'flightops:report.title' : 'flightops:report.correctTitle')}</H1>
        <Lead>
          {leg === null
            ? t('flightops:report.openFlight')
            : t('flightops:report.leg', { number: leg.number, from: leg.departureIcao, to: leg.arrivalIcao })}
        </Lead>
      </header>

      {refusal === null ? (
        <ReportForm tour={tour} leg={leg} report={report} back={back} />
      ) : (
        <div className="flex flex-col gap-4">
          <Notice tone="warning" title={refusal} />
          <OtherLegs tour={tour} mine={mine} />
        </div>
      )}
    </article>
  );
}

/** When the leg asked for cannot be reported, the ones that can: one link each. */
function OtherLegs({ tour, mine }: { tour: PublicTourDto; mine: MyTourDto }) {
  const { t } = useTranslation();
  const legs = tour.legs.filter((leg) => mine.flyable.includes(leg.id));

  if (mine.blocked !== null || legs.length === 0) {
    return null;
  }

  return (
    <ul className="flex flex-col gap-1 text-sm">
      {legs.map((leg) => (
        <li key={leg.id}>
          <RouterAnchor href={`/tours/${tour.slug}/report?leg=${leg.id}`} className="underline">
            {t('flightops:report.leg', { number: leg.number, from: leg.departureIcao, to: leg.arrivalIcao })}
          </RouterAnchor>
        </li>
      ))}
    </ul>
  );
}

/** The names of the details form's own fields: a refusal on any other belongs to the flight half of the page. */
const DETAIL_FIELDS = Object.keys(reportDetailsSchema.shape);

function ReportForm({
  tour,
  leg,
  report,
  back,
}: {
  tour: PublicTourDto;
  leg: PublicLegDto | null;
  report: PirepDto | null;
  back: string;
}) {
  const { t, i18n } = useTranslation();
  const navigate = useNavigate();
  const notice = useNotice();
  const send = useSendReport(tour.id, report?.id ?? null);

  const kept = report === null ? [] : sessionsOf(report);
  const [first, setFirst] = useState<number | null>(kept[0]?.id ?? null);
  const [second, setSecond] = useState<number | null>(kept[1]?.id ?? null);
  const [diverted, setDiverted] = useState(report?.isDiversion ?? false);
  const [diversion, setDiversion] = useState<DiversionValues>({
    diversionIcao: report?.diversionIcao ?? '',
    diversionReason: report?.diversionReason ?? undefined,
    diversionNote: report?.diversionNote ?? '',
  });
  const [flightProblem, setFlightProblem] = useState<string | null>(null);

  const firstSearch: SessionSearch = leg === null ? {} : { legId: leg.id };
  const firstFound = useQuery(trackerSessionsQuery(tour.id, firstSearch));
  const firstSessions = mergeSessions(kept.slice(0, 1), firstFound.data ?? []);

  // The flight after a diversion goes from where the first one ended to where the leg ends — or, on an Open tour, to
  // where the first flight was planned to go.
  const destination =
    leg?.arrivalIcao ?? firstSessions.find((session) => session.id === first)?.arrivalIcao ?? null;
  const secondSearch: SessionSearch | null =
    diverted && diversion.diversionIcao.length === 4 && destination !== null
      ? { departure: diversion.diversionIcao, arrival: destination }
      : null;
  const secondFound = useQuery({
    ...trackerSessionsQuery(tour.id, secondSearch ?? {}),
    enabled: secondSearch !== null,
  });
  const secondSessions = mergeSessions(kept.slice(1, 2), secondFound.data ?? []);

  const submit = async (values: ReportDetailsValues) => {
    setFlightProblem(null);

    const body: PirepWriteDto = {
      legId: leg?.id ?? null,
      sessionIds: [first, ...(diverted ? [second] : [])].filter((id): id is number => id !== null),
      isDiversion: diverted,
      diversionIcao: diverted ? blank(diversion.diversionIcao) : null,
      diversionReason: diverted ? (diversion.diversionReason ?? null) : null,
      diversionNote: diverted ? blank(diversion.diversionNote) : null,
      sid: blank(values.sid),
      star: blank(values.star),
      approach: blank(values.approach),
      pilotRemarks: blank(values.pilotRemarks),
      rowVersion: report?.rowVersion ?? NEW_ROW_VERSION,
    };

    try {
      await send.mutateAsync(body);
    } catch (error) {
      const { details, flight } = splitRefusal(error, DETAIL_FIELDS);
      setFlightProblem(flight === null ? null : describeProblem(flight, t, i18n.language));
      if (details !== null) {
        throw details;
      }
      return;
    }

    notice({
      tone: 'success',
      title: t(report === null ? 'flightops:report.sent' : 'flightops:report.resent'),
    });
    await navigate({ href: back });
  };

  return (
    <div className="flex flex-col gap-8">
      <section className="flex flex-col gap-4">
        <H2>{t('flightops:report.flight')}</H2>
        <FlightChoice
          label={t('flightops:report.flight')}
          sessions={firstSessions}
          query={firstFound}
          value={first}
          onChange={setFirst}
          empty={t(leg === null ? 'flightops:report.noFlightsOpen' : 'flightops:report.noFlights', {
            days: tour.reportWindowDays,
          })}
        />

        <div className="flex items-center gap-2">
          <Checkbox
            id="report-diverted"
            checked={diverted}
            onCheckedChange={(checked) => setDiverted(checked === true)}
          />
          <Label htmlFor="report-diverted">{t('flightops:report.diverted')}</Label>
        </div>

        {diverted ? (
          <div className="flex flex-col gap-4 border-l-2 pl-4">
            <SchemaForm
              schema={diversionSchema}
              defaults={diversion}
              locales={[]}
              labels="flightops:report"
              onChange={setDiversion}
            />
            {secondSearch === null ? (
              <p className="text-muted-foreground text-sm">{t('flightops:report.diversionFirst')}</p>
            ) : (
              <FlightChoice
                label={t('flightops:report.secondFlight', {
                  from: secondSearch.departure,
                  to: secondSearch.arrival,
                })}
                sessions={secondSessions}
                query={secondFound}
                value={second}
                onChange={setSecond}
                empty={t('flightops:report.noSecondFlight', {
                  from: secondSearch.departure,
                  to: secondSearch.arrival,
                })}
              />
            )}
          </div>
        ) : null}

        {flightProblem === null ? null : <Notice tone="error" title={flightProblem} />}
      </section>

      <section className="flex flex-col gap-4">
        <H2>{t('flightops:report.details')}</H2>
        {tour.requiresProcedures ? (
          <p className="text-muted-foreground text-sm">{t('flightops:report.proceduresRequired')}</p>
        ) : null}
        <SchemaForm
          schema={reportDetailsSchema}
          defaults={{
            sid: report?.sid ?? '',
            star: report?.star ?? '',
            approach: report?.approach ?? '',
            pilotRemarks: report?.pilotRemarks ?? '',
          }}
          locales={[]}
          labels="flightops:report"
          onSubmit={submit}
          submitLabel={t(report === null ? 'flightops:report.send' : 'flightops:report.resend')}
          secondaryAction={
            <Button asChild variant="ghost">
              <RouterAnchor href={back}>{t('common.cancel')}</RouterAnchor>
            </Button>
          }
        />
      </section>
    </div>
  );
}

/**
 * One list of sessions to choose from, with what the tracker said when it had none: «it did not answer» is not «you did
 * not fly» (T2), and a pilot sure of having flown is told which of the two it is.
 */
function FlightChoice({
  label,
  sessions,
  query,
  value,
  onChange,
  empty,
}: {
  label: string;
  sessions: readonly TrackerSessionDto[];
  query: { isPending: boolean; error: unknown };
  value: number | null;
  onChange: (id: number) => void;
  empty: string;
}) {
  const { t, i18n } = useTranslation();
  const moment = useMoment();

  if (sessions.length === 0) {
    if (query.isPending) {
      return <p className="text-muted-foreground text-sm">{t('flightops:report.searching')}</p>;
    }

    if (query.error instanceof ApiError && query.error.status === 503) {
      return <Notice tone="warning" title={t('flightops:errors.trackerUnavailable')} />;
    }

    if (query.error !== null && query.error !== undefined) {
      return (
        <Notice tone="error" title={describeProblem(query.error, t, i18n.language) ?? t('errors.unknown')} />
      );
    }

    return <Notice tone="info" title={empty} />;
  }

  return (
    <RadioGroupRoot
      aria-label={label}
      value={value === null ? '' : String(value)}
      onValueChange={(chosen) => onChange(Number(chosen))}
      className="flex flex-col gap-2"
    >
      {sessions.map((session) => {
        const id = `session-${session.id}`;

        return (
          <div key={session.id} className="flex items-start gap-3 rounded-md border p-3">
            <RadioGroupItem id={id} value={String(session.id)} className="mt-1" />
            <Label htmlFor={id} className="flex flex-1 cursor-pointer flex-col gap-1 font-normal">
              <span className="font-mono font-semibold">
                {session.callsign}
                <span className="text-muted-foreground font-sans font-normal">
                  {' · '}
                  {session.departureIcao ?? '—'} → {session.arrivalIcao ?? '—'}
                  {session.aircraft === null ? '' : ` · ${session.aircraft}`}
                </span>
              </span>
              <span className="text-muted-foreground text-sm tabular-nums">
                {t('flightops:report.sessionTime', {
                  from: moment(session.startedAt),
                  to: moment(session.endedAt, { date: false }),
                })}
              </span>
            </Label>
          </div>
        );
      })}
    </RadioGroupRoot>
  );
}

/** An empty field is no value, which is what the server reads as «not written». */
function blank(text: string): string | null {
  const trimmed = text.trim();
  return trimmed === '' ? null : trimmed;
}
