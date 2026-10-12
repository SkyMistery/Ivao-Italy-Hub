import { H1, H2, Label, Lead, Select } from '@ivao/atmosphere-react';
import { useQuery, useQueryClient } from '@tanstack/react-query';
import { useNavigate, useParams, useSearch } from '@tanstack/react-router';
import { useCallback, useMemo } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { ContentRenderer, blockDataQuery, readBody } from '../../../blocks';
import { bootstrapQuery } from '../../../features/me/queries';
import { holdsPermissionAnywhere } from '../../../shared/api/bootstrap';
import { mediaFileUrl } from '../../../shared/api/mediaUrl';
import { describeProblem } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { ListFilter } from '../../../shared/list';
import { PageMetadata } from '../../../shared/seo/PageMetadata';
import {
  CALENDAR_SCREEN_VIEWS,
  CalendarView,
  EmptyState,
  NotFound,
  Notice,
  type CalendarViewMode,
} from '../../../shared/ui';
import { myBookingsQuery, publicEventQuery, type PublicEventDto } from '../api';
import { EVENTS_VIEW } from '../permissions';
import {
  eventPageSearchSchema,
  eventsPublicSearchSchema,
  type EventPageSearch,
  type EventsPublicSearch,
} from '../schemas';

import {
  EVENT_LIST_BLOCK,
  airportLabel,
  calendarItems,
  cardAirports,
  cardKinds,
  narrowCards,
  type EventListData,
} from './cards';
import { EventDayStrip } from './BookingOpening';
import { AirportName, EventCards } from './EventCards';
import { EventHead } from './EventHead';
import { EventSlots } from './EventSlots';
import { MY_EVENTS_PAGE, type SlotViewer } from './myBookings';
import { PrivateSlots } from './PrivateSlots';
import { RegistrationSection } from './Registration';

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
      {/* The tab says what the heading says (#224). */}
      <PageMetadata title={t('events:public.title')} description={t('events:public.description')} />

      <header className="flex flex-col gap-2">
        <H1>{t('events:public.title')}</H1>
        <Lead>{t('events:public.description')}</Lead>
        {/* A member's own bookings are a page of theirs (E6b): the way there is where the events are. */}
        {bootstrap?.user === null || bootstrap?.user === undefined ? null : (
          <RouterAnchor href={MY_EVENTS_PAGE} className="text-sm underline">
            {t('events:mine.title')}
          </RouterAnchor>
        )}
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
 * One event (§7.1): the banner; its head (`EventHead`, note 2026-10-11-la-testa-della-pagina-di-un-evento) — the kind, the title, the
 * summary, and two short lines with when, in UTC and in the division's time, its bookings, counted down to until they open (E6b), its
 * airports and who organises it when it is not the division —; on its day who is online at its airports (E6b); a cancelled one with
 * its note. Then what a pilot came for: its public slots (E5), narrowed by the filters of the address and booked from the dialog of a
 * slot (E6b), and its private slots by airport, direction and hour, booked with the flight the pilot flies (E7); for an event in person the
 * registration with its questions (E16). At the bottom the
 * description and the routes the flight operations wrote. To the staff, when nobody else sees it, a line that says so and why, and the
 * way back to the back office.
 */
function EventScreen({ event }: { event: PublicEventDto }) {
  const { t } = useTranslation();
  const read = useLocalized();
  const navigate = useNavigate();
  const queryClient = useQueryClient();
  const { data: bootstrap } = useQuery(bootstrapQuery);
  const timezone = bootstrap?.division.timezone;

  // What the slots are narrowed to lives in the address, as the filters of /events do: a link keeps them.
  const filters = eventPageSearchSchema.parse(useSearch({ strict: false }));
  const onFilter = (patch: Partial<EventPageSearch>) =>
    void navigate({
      search: ((previous: EventPageSearch) => ({ ...previous, ...patch })) as never,
      to: '.',
      replace: true,
    });

  // The reader's own bookings of this event, from their own list: the page never says who took a slot.
  const signedIn = bootstrap?.user !== null && bootstrap?.user !== undefined;
  const { data: mine } = useQuery({ ...myBookingsQuery(), enabled: signedIn });
  const viewer = useMemo<SlotViewer>(
    () => ({
      signedIn,
      mine: new Map(
        (mine ?? [])
          .filter((booking) => booking.eventId === event.id)
          .map((booking) => [booking.slotId, booking]),
      ),
    }),
    [signedIn, mine, event.id],
  );

  // When the count reaches the opening, the page is read again: the slots are offered from that moment.
  const slug = event.slug;
  const onOpen = useCallback(() => {
    void queryClient.invalidateQueries({ queryKey: publicEventQuery(slug).queryKey });
  }, [queryClient, slug]);

  return (
    <article className="mx-auto flex w-full max-w-4xl flex-col gap-8 px-4 py-10">
      {/* The row's title in the tab; the division's name after it comes from the root (#224). */}
      <PageMetadata title={event.title} description={event.summary} imageMediaId={event.bannerMediaId} />

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

      <EventHead event={event} timezone={timezone} onOpen={onOpen} />

      <EventDayStrip event={event} timezone={timezone} />

      {event.cancelledAt === null ? null : (
        <Notice
          tone="warning"
          title={t('events:public.cancelled')}
          description={read(event.cancellationNote ?? {})}
        />
      )}

      {event.slots.length === 0 ? null : (
        <EventSlots event={event} filters={filters} onFilter={onFilter} viewer={viewer} />
      )}

      {event.privateSlots.length === 0 ? null : <PrivateSlots event={event} viewer={viewer} />}

      {/* An event in person (E16): the registration, with the questions. */}
      <RegistrationSection event={event} signedIn={signedIn} />

      {/* A member's way to their bookings, all of them and the withdrawing (E6b). */}
      {viewer.mine.size === 0 ? null : (
        <RouterAnchor href={MY_EVENTS_PAGE} className="text-sm underline">
          {t('events:mine.title')}
        </RouterAnchor>
      )}

      <ContentRenderer body={readBody(event.body)} />

      {event.routes.length === 0 ? null : <EventRoutes event={event} />}
    </article>
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
