import { PlaneTakeoff, RadioTower, Ticket } from 'lucide-react';
import { z } from 'zod';

import type { BlockRegistration } from '../../../shared/modules';
import type { EventListData } from '../screens/cards';

import { AtcCoverageBlock, type AtcCoverageData } from './atcCoverage';
import { EventListBlock } from './eventList';
import { MyEventsBlock, type MyEventsData } from './myEvents';

/**
 * The blocks of the events (design M4 §7.3), as the manifest lists them. Each has its other half in `EventsModule.Blocks` on the
 * server, and the manifest test compares the two. The first is the list of the events (E4), the second the member's bookings still
 * to fly (E6b) and shifts (E12), the third the coverage of the rosters for the staff (E12); the staff's queue arrives with its phase (E13b).
 *
 * The example the gallery draws is of its own making — kinds and airports no division has —, and its dates are counted from when
 * the gallery is drawn, so that what is to come stays to come.
 */

const DAY = 24 * 60 * 60 * 1000;

/** A moment this many days from when the gallery is drawn. */
function inDays(days: number): string {
  return new Date(Date.now() + days * DAY).toISOString();
}

/**
 * The events to come and those in progress (§7.1, §7.3): on the home page, on a page of the site, on a dashboard. Two properties —
 * the kinds to show, none for every kind, and how many at most —; who sees an event for the members is the server's answer.
 */
export const eventListBlock: BlockRegistration = {
  type: 'events.eventList',
  version: 1,
  kind: 'Data',
  alwaysLive: true,
  schema: z.object({
    // The words of the calendar, as the calendar's block names them: none named, every kind.
    kinds: z.array(z.object({ kind: z.string() })),
    // Zero is every event, up to the server's bound of 50: a property that is not written is not a way of asking for none, and
    // the server takes the same ten for it.
    limit: z.number().int().min(0).max(50).default(10),
  }),
  component: EventListBlock,
  example: { kinds: [], limit: 3 },
  exampleData: {
    items: [
      {
        id: 1,
        slug: 'gallery-evening',
        kind: 'gallery',
        title: { en: 'An evening at two airports', it: 'Una sera su due scali' },
        summary: {
          en: 'Arrivals and departures every few minutes.',
          it: 'Arrivi e partenze ogni pochi minuti.',
        },
        bannerMediaId: null,
        state: 'BookingOpen',
        startsAtUtc: inDays(5),
        endsAtUtc: inDays(5.2),
        wholeDivision: false,
        airports: [
          { icao: 'XX01', name: 'North Field' },
          { icao: 'XX02', name: 'South Field' },
        ],
      },
      {
        id: 2,
        slug: 'gallery-everywhere',
        kind: 'gallery',
        title: { en: 'A night everywhere', it: 'Una notte ovunque' },
        summary: {
          en: 'Every position of the division open.',
          it: 'Ogni postazione della divisione aperta.',
        },
        bannerMediaId: null,
        state: 'Announced',
        startsAtUtc: inDays(12),
        endsAtUtc: inDays(12.25),
        wholeDivision: true,
        airports: [],
      },
    ],
  } satisfies EventListData,
  editorLabelKey: 'events:blocks.eventList.label',
  // Its properties are the module's words, not the core's (`BlockRegistration.propertyLabels`).
  propertyLabels: 'events:blocks.eventList',
  group: 'data',
  icon: Ticket,
};

/** A booking of the gallery's own making: a flight of an event of no division, some days from when the gallery is drawn. */
function galleryBooking(id: number, callsign: string, days: number, from: string, to: string) {
  return {
    id,
    slotId: id,
    eventId: 1,
    eventSlug: 'gallery-evening',
    eventTitle: { en: 'An evening at two airports', it: 'Una sera su due scali' },
    eventState: 'BookingOpen',
    kind: 'Public',
    callsign,
    flightNumber: null,
    aircraftIcao: 'XA20',
    departureIcao: from,
    offBlockUtc: inDays(days),
    arrivalIcao: to,
    onBlockUtc: inDays(days + 0.05),
    isArrival: false,
    stand: null,
    rotation: 'R1',
    leg: id,
    withdrawable: true,
    createdAt: inDays(-1),
    pairedBookingId: null,
  } as const;
}

/**
 * The reader's bookings still to fly (§7.3, E6b) and shifts still to come (E12): on `/me`, where a dashboard puts it from the back office.
 * No property — it is the reader's —, and a line that asks a visitor to sign in.
 */
export const myEventsBlock: BlockRegistration = {
  type: 'events.myEvents',
  version: 1,
  kind: 'Data',
  alwaysLive: true,
  schema: z.object({}),
  component: MyEventsBlock,
  example: {},
  exampleData: {
    signedIn: true,
    bookings: [
      galleryBooking(1, 'XXA101', 5, 'XX01', 'XX02'),
      galleryBooking(2, 'XXA102', 5.1, 'XX02', 'XX01'),
    ],
    shifts: [
      {
        id: 1,
        eventId: 2,
        eventSlug: 'gallery-everywhere',
        eventTitle: { en: 'A night everywhere', it: 'Una notte ovunque' },
        eventState: 'Announced',
        callsign: 'XX01_TWX',
        fromUtc: inDays(12),
        toUtc: inDays(12.04),
        transferable: true,
        transfer: null,
      },
    ],
  } satisfies MyEventsData,
  editorLabelKey: 'events:blocks.myEvents.label',
  group: 'data',
  icon: PlaneTakeoff,
};

/**
 * The coverage of the rosters of the next days (§7.3, E12): on `/staff` and the dashboards of the staff of the events and of the ATC. One
 * property, how many days ahead it looks; what each reader sees of it is the server's answer, on the positions they may read.
 */
export const atcCoverageBlock: BlockRegistration = {
  type: 'events.atcCoverage',
  version: 1,
  kind: 'Data',
  alwaysLive: true,
  schema: z.object({
    // The server's bounds: 1 to 60 days, 14 when nothing is written.
    days: z.number().int().min(1).max(60).default(14),
  }),
  component: AtcCoverageBlock,
  example: { days: 14 },
  exampleData: {
    signedIn: true,
    days: 14,
    events: [
      {
        id: 1,
        slug: 'gallery-evening',
        title: { en: 'An evening at two airports', it: 'Una sera su due scali' },
        startsAtUtc: inDays(5),
        closesAtUtc: inDays(2),
        publishesAtUtc: inDays(3),
        stage: 'Open',
        availabilities: 4,
        pieces: 12,
        uncovered: 12,
        transfers: 0,
      },
      {
        id: 2,
        slug: 'gallery-everywhere',
        title: { en: 'A night everywhere', it: 'Una notte ovunque' },
        startsAtUtc: inDays(1.5),
        closesAtUtc: inDays(-1.5),
        publishesAtUtc: inDays(-0.5),
        stage: 'Published',
        availabilities: 9,
        pieces: 18,
        uncovered: 3,
        transfers: 1,
      },
    ],
  } satisfies AtcCoverageData,
  editorLabelKey: 'events:blocks.atcCoverage.label',
  // Its property is the module's word, not the core's (`BlockRegistration.propertyLabels`).
  propertyLabels: 'events:blocks.atcCoverage',
  group: 'data',
  icon: RadioTower,
};
