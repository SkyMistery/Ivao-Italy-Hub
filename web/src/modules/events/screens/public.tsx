import { Badge, Button, H1, H2, Label, Lead, Select, Subtle } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate, useParams, useSearch } from '@tanstack/react-router';
import { ExternalLink } from 'lucide-react';
import { Fragment } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { ContentRenderer, blockDataQuery, readBody } from '../../../blocks';
import { bootstrapQuery } from '../../../features/me/queries';
import { holdsPermissionAnywhere } from '../../../shared/api/bootstrap';
import { mediaFileUrl } from '../../../shared/api/mediaUrl';
import { describeProblem } from '../../../shared/forms';
import { resolveLocalized } from '../../../shared/i18n/localized';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import { ListFilter } from '../../../shared/list';
import { PageMetadata } from '../../../shared/seo/PageMetadata';
import {
  CALENDAR_SCREEN_VIEWS,
  CalendarView,
  EmptyState,
  NotFound,
  Notice,
  calendarKindColour,
  type CalendarViewMode,
} from '../../../shared/ui';
import { publicEventQuery, type PublicEventDto, type PublicEventSlotDto } from '../api';
import { EVENTS_VIEW } from '../permissions';
import { eventsPublicSearchSchema, type EventsPublicSearch } from '../schemas';

import {
  EVENT_LIST_BLOCK,
  STATE_COLOURS,
  airportLabel,
  calendarItems,
  cardAirports,
  cardKinds,
  narrowCards,
  type EventListData,
} from './cards';
import { AirportName, EventCards, EventWhen } from './EventCards';
import { oneDay, slotGroups } from './slotList';

/**
 * The public side of the events (design M4 §7.1, E4): `/events`, the events to come and those in progress as cards, narrowed to a
 * kind and an airport, with the core's calendar on the same events under them; and `/events/{slug}`, the page of one, where its
 * calendar entry and its line in the search lead.
 *
 * What arrives is what the reader may see, decided by the server: an event the public does not see — a draft, one not seen yet,
 * one that ended, one of the members read by a visitor — is not on the list, and its page is not found. **No archive** (§2.4). The
 * staff of the events read the page of an event in every state, told that nobody else does, and why.
 */

const EVENTS_PAGE = '/events';

/**
 * As many cards as the server gives one list: zero asks for its bound, `PublicEvents.MaxItems` (50), the soonest first. The site
 * has no archive, so these are the events to come, unless a division ever announces more than fifty at once: then the page shows
 * the fifty soonest, and the filters and the calendar below work on those (the review of #223, point 5).
 */
const EVERY_CARD = { limit: 0 };

export function EventsPublicPage() {
  const { t, i18n } = useTranslation();
  const read = useLocalized();
  const navigate = useNavigate();
  const search = eventsPublicSearchSchema.parse(useSearch({ strict: false }));
  const { data: bootstrap } = useQuery(bootstrapQuery);
  // The block's own answer, live, as `/calendar` reads the calendar's: one place decides which events the public sees.
  const answer = useQuery(blockDataQuery(EVENT_LIST_BLOCK, EVERY_CARD));
  const cards = (answer.data as EventListData | null | undefined)?.items;

  const onFilter = (patch: Partial<EventsPublicSearch>) =>
    void navigate({
      search: ((previous: EventsPublicSearch) => ({ ...previous, ...patch })) as never,
      to: '.',
    });

  const kinds = bootstrap?.calendarKinds ?? [];
  const shown = cards === undefined ? [] : narrowCards(cards, search);
  const view: CalendarViewMode = search.view ?? 'month';

  return (
    <div className="mx-auto flex w-full max-w-6xl flex-col gap-8 px-4 py-10">
      <header className="flex flex-col gap-2">
        <H1>{t('events:public.title')}</H1>
        <Lead>{t('events:public.description')}</Lead>
      </header>

      {answer.isError ? (
        <Notice tone="error" title={describeProblem(answer.error, t, i18n.language) ?? t('errors.unknown')} />
      ) : cards === undefined || bootstrap === undefined ? (
        <p className="text-muted-foreground text-sm">{t('common.loading')}</p>
      ) : cards.length === 0 ? (
        <EmptyState title={t('events:public.none')} />
      ) : (
        <>
          <div className="flex flex-wrap items-end gap-4">
            {/* The choices are what the cards hold: a kind or an airport no event has would narrow to nothing. */}
            <ListFilter
              className="min-w-40"
              id="events-kind"
              label={t('events:public.filters.kind')}
              none={t('events:public.filters.anyKind')}
              value={search.kind}
              onChange={(kind) => onFilter({ kind })}
              items={cardKinds(cards).map((kind) => ({
                value: kind,
                label: read(kinds.find((word) => word.key === kind)?.label) || kind,
              }))}
            />
            <ListFilter
              className="min-w-40"
              id="events-airport"
              label={t('events:public.filters.airport')}
              none={t('events:public.filters.anyAirport')}
              value={search.airport}
              onChange={(airport) => onFilter({ airport })}
              items={cardAirports(cards).map((airport) => ({
                value: airport.icao,
                label: airportLabel(airport),
              }))}
            />
          </div>

          {shown.length === 0 ? (
            <EmptyState title={t('events:public.noneHere')} />
          ) : (
            <EventCards cards={shown} />
          )}

          <section className="flex flex-col gap-4" aria-label={t('events:public.calendar')}>
            <div className="flex flex-wrap items-end justify-between gap-4">
              <H2>{t('events:public.calendar')}</H2>
              <div className="flex min-w-40 flex-col gap-1">
                <Label htmlFor="events-view">{t('calendar.public.filters.view')}</Label>
                <Select
                  id="events-view"
                  value={view}
                  onValueChange={(chosen) =>
                    onFilter({ view: chosen as (typeof CALENDAR_SCREEN_VIEWS)[number] })
                  }
                  items={CALENDAR_SCREEN_VIEWS.map((mode) => ({
                    value: mode,
                    label: t(`calendar.public.views.${mode}`),
                  }))}
                />
              </div>
            </div>
            <CalendarView
              items={calendarItems(shown)}
              view={view}
              anchor={readAnchor(search.on)}
              onAnchorChange={(next) => onFilter({ on: next.toISOString().slice(0, 10) })}
              timezone={bootstrap.division.timezone}
              kinds={kinds}
              empty={t('events:public.calendarEmpty')}
            />
          </section>
        </>
      )}
    </div>
  );
}

/** The day the calendar is drawn around: what the address says, or today. */
function readAnchor(on: string | undefined): Date {
  if (on === undefined) {
    return new Date();
  }

  const parsed = new Date(`${on}T00:00:00Z`);
  return Number.isNaN(parsed.getTime()) ? new Date() : parsed;
}

export function EventPublicPage() {
  const { t } = useTranslation();
  // A module route is not in the generated tree, so its parameters are not typed: the manifest declares the path and this reads
  // the one segment it has.
  const { slug = '' } = useParams({ strict: false });
  const { data: event, isPending } = useQuery(publicEventQuery(slug));
  const { data: bootstrap } = useQuery(bootstrapQuery);

  if (isPending || bootstrap === undefined) {
    return (
      <p className="text-muted-foreground mx-auto w-full max-w-4xl px-4 py-10 text-sm">
        {t('common.loading')}
      </p>
    );
  }

  if (event === undefined || event === null) {
    return <NotFound />;
  }

  return <EventScreen event={event} />;
}

/**
 * One event (§7.1): the banner; the state, the kind, the title and the summary; when, in UTC and in the division's time; who
 * organises it and the airports; a cancelled one with its note; the routes the flight operations wrote; its public slots (E5); the
 * description. To the
 * staff, when nobody else sees it, a line that says so and why, and the way back to the back office.
 */
function EventScreen({ event }: { event: PublicEventDto }) {
  const { t, i18n } = useTranslation();
  const read = useLocalized();
  const { data: bootstrap } = useQuery(bootstrapQuery);
  const kinds = bootstrap?.calendarKinds ?? [];
  const summary = read(event.summary);

  return (
    <article className="mx-auto flex w-full max-w-4xl flex-col gap-8 px-4 py-10">
      <PageMetadata
        title={event.title}
        description={event.summary}
        imageMediaId={event.bannerMediaId}
        divisionName={resolveLocalized(
          bootstrap?.division.name,
          i18n.language,
          bootstrap?.division.defaultLocale ?? i18n.language,
        )}
      />

      <div className="flex flex-wrap items-center justify-between gap-2">
        <RouterAnchor href={EVENTS_PAGE} className="text-sm underline">
          {t('events:public.back')}
        </RouterAnchor>
        {bootstrap !== undefined && holdsPermissionAnywhere(bootstrap, EVENTS_VIEW) ? (
          <RouterAnchor href={`/staff/events/${event.id}`} className="text-sm underline">
            {t('events:public.backOffice')}
          </RouterAnchor>
        ) : null}
      </div>

      {/* Why nobody else sees it, as the server says: the state alone cannot tell a cancelled event not seen yet from one over. */}
      {event.unseen === null ? null : (
        <Notice tone="info" title={t(`events:public.staffOnly.${event.unseen}`)} />
      )}

      {event.bannerMediaId === null ? null : (
        <img
          src={mediaFileUrl(event.bannerMediaId)}
          alt=""
          className="bg-muted w-full rounded-lg object-cover"
        />
      )}

      <header className="flex flex-col gap-3">
        <div className="flex flex-wrap items-center gap-2">
          <Badge
            variant="flat"
            color={STATE_COLOURS[event.state] ?? 'gray'}
            text={t(`events:events.options.state.${event.state}`)}
          />
          <Badge
            variant="flat"
            color={calendarKindColour(event.kind, kinds)}
            text={read(kinds.find((word) => word.key === event.kind)?.label) || event.kind}
          />
        </div>
        <H1>{read(event.title)}</H1>
        {summary === '' ? null : <Lead>{summary}</Lead>}
      </header>

      {event.cancelledAt === null ? null : (
        <Notice
          tone="warning"
          title={t('events:public.cancelled')}
          description={read(event.cancellationNote ?? {})}
        />
      )}

      <dl className="grid grid-cols-1 gap-x-6 gap-y-3 sm:grid-cols-[max-content_1fr]">
        <dt className="text-muted-foreground text-sm">{t('events:public.when')}</dt>
        <dd>
          <EventWhen
            startsAtUtc={event.startsAtUtc}
            endsAtUtc={event.endsAtUtc}
            timezone={bootstrap?.division.timezone}
          />
        </dd>

        <dt className="text-muted-foreground text-sm">{t('events:events.fields.organizer')}</dt>
        <dd className="flex flex-wrap items-center gap-x-3">
          <span>{t(`events:events.options.organizer.${event.organizer}`)}</span>
          {event.externalUrl === null ? null : (
            <Button asChild size="sm" variant="ghost">
              <a href={event.externalUrl} target="_blank" rel="noreferrer">
                <ExternalLink aria-hidden className="mr-1 size-4" />
                {t('events:public.organizerPage')}
              </a>
            </Button>
          )}
        </dd>

        <dt className="text-muted-foreground text-sm">{t('events:events.fields.airports')}</dt>
        <dd>
          {event.wholeDivision ? (
            t('events:public.wholeDivision')
          ) : event.airports.length === 0 ? (
            '—'
          ) : (
            <ul className="flex flex-col gap-1">
              {event.airports.map((airport) => (
                <li key={airport.icao}>
                  <AirportName airport={airport} />
                </li>
              ))}
            </ul>
          )}
        </dd>
      </dl>

      {event.routes.length === 0 ? null : <EventRoutes event={event} />}

      {event.slots.length === 0 ? null : <EventSlots event={event} />}

      <ContentRenderer body={readBody(event.body)} />
    </article>
  );
}

/**
 * The public slots (§7.1, E5): each flight the staff published, by its off block, the legs of a rotation together by their places,
 * free or taken — never who took it (plan §9.7). The times are UTC, the network's; when they all fall on one day, the day is said
 * once above them. «Book» and the filters come with the bookings (E6b).
 */
function EventSlots({ event }: { event: PublicEventDto }) {
  const { t } = useTranslation();
  const moment = useMoment();
  const sameDay = oneDay(event.slots);
  const time = (value: string) => moment(value, sameDay ? { date: false } : {});
  const first = event.slots[0];

  return (
    <section className="flex flex-col gap-4" aria-label={t('events:public.slots')}>
      <div className="flex flex-col gap-1">
        <H2>{t('events:public.slots')}</H2>
        {sameDay && first !== undefined ? (
          <Subtle className="text-sm">
            {t('events:public.slotsOn', { day: moment(first.offBlockUtc, { time: false }) })}
          </Subtle>
        ) : null}
      </div>
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-muted-foreground border-b text-left">
              <th className="py-2 pr-3 font-medium">{t('events:slots.fields.callsign')}</th>
              <th className="py-2 pr-3 font-medium">{t('events:slots.fields.aircraftTypes')}</th>
              <th className="py-2 pr-3 font-medium">{t('events:slots.fields.departureIcao')}</th>
              <th className="py-2 pr-3 font-medium">{t('events:public.offBlock')}</th>
              <th className="py-2 pr-3 font-medium">{t('events:slots.fields.arrivalIcao')}</th>
              <th className="py-2 pr-3 font-medium">{t('events:public.onBlock')}</th>
              <th className="py-2 pr-3 font-medium">{t('events:slots.fields.stand')}</th>
              <th className="py-2 font-medium">{t('events:public.slotState')}</th>
            </tr>
          </thead>
          <tbody>
            {slotGroups(event.slots).map((group) =>
              group.rotation === null ? (
                group.slots.map((slot) => <SlotRow key={slot.id} slot={slot} time={time} />)
              ) : (
                <Fragment key={`rotation-${group.rotation}`}>
                  <tr className="border-b">
                    <th colSpan={8} scope="rowgroup" className="pt-4 pb-1 text-left font-semibold">
                      {t('events:public.rotation', { rotation: group.rotation, count: group.slots.length })}
                    </th>
                  </tr>
                  {group.slots.map((slot) => (
                    <SlotRow key={slot.id} slot={slot} time={time} />
                  ))}
                </Fragment>
              ),
            )}
          </tbody>
        </table>
      </div>
    </section>
  );
}

/** One public slot: its flight, its times, its stand, free or taken. */
function SlotRow({ slot, time }: { slot: PublicEventSlotDto; time: (value: string) => string }) {
  const { t } = useTranslation();

  return (
    <tr className="border-b align-top last:border-0">
      <td className="py-2 pr-3">
        <span className="font-mono">{slot.callsign}</span>
        {slot.flightNumber === null ? null : (
          <span className="text-muted-foreground"> · {slot.flightNumber}</span>
        )}
        {slot.leg === null ? null : (
          <Subtle className="text-xs">{t('events:public.leg', { leg: slot.leg })}</Subtle>
        )}
      </td>
      <td className="py-2 pr-3 font-mono">{slot.aircraftTypes.join(' / ')}</td>
      <td className="py-2 pr-3">
        <AirportName airport={slot.departure} />
      </td>
      <td className="py-2 pr-3 tabular-nums">{time(slot.offBlockUtc)}</td>
      <td className="py-2 pr-3">
        <AirportName airport={slot.arrival} />
      </td>
      <td className="py-2 pr-3 tabular-nums">{time(slot.onBlockUtc)}</td>
      <td className="py-2 pr-3">{slot.stand ?? '—'}</td>
      <td className="py-2">
        <Badge
          variant="flat"
          color={slot.taken ? 'gray' : 'green'}
          text={t(slot.taken ? 'events:public.taken' : 'events:public.free')}
        />
      </td>
    </tr>
  );
}

/**
 * The routes the flight operations wrote (§1.4), in the order they wrote them: from, to, the route to file, and what to know about
 * it.
 */
function EventRoutes({ event }: { event: PublicEventDto }) {
  const { t } = useTranslation();
  const read = useLocalized();

  return (
    <section className="flex flex-col gap-4">
      <H2>{t('events:public.routes')}</H2>
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="text-muted-foreground border-b text-left">
              <th className="py-2 pr-3 font-medium">{t('events:routes.fields.departureIcao')}</th>
              <th className="py-2 pr-3 font-medium">{t('events:routes.fields.arrivalIcao')}</th>
              <th className="py-2 pr-3 font-medium">{t('events:routes.fields.route')}</th>
              <th className="py-2 font-medium">{t('events:routes.fields.remarks')}</th>
            </tr>
          </thead>
          <tbody>
            {event.routes.map((route) => (
              <tr key={route.id} className="border-b align-top last:border-0">
                <td className="py-2 pr-3">
                  <AirportName airport={route.departure} />
                </td>
                <td className="py-2 pr-3">
                  <AirportName airport={route.arrival} />
                </td>
                <td className="py-2 pr-3 font-mono break-words">{route.route}</td>
                <td className="py-2">{read(route.remarks ?? {})}</td>
              </tr>
            ))}
          </tbody>
        </table>
      </div>
    </section>
  );
}
