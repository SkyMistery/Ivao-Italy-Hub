import { Ticket } from 'lucide-react';
import { z } from 'zod';

import type { BlockRegistration } from '../../../shared/modules';
import type { EventListData } from '../screens/cards';

import { EventListBlock } from './eventList';

/**
 * The blocks of the events (design M4 §7.3), as the manifest lists them. Each has its other half in `EventsModule.Blocks` on the
 * server, and the manifest test compares the two. The first is the list of the events (E4); the member's, the ATC's and the
 * staff's queue arrive with their phases (E6b, E12, E13b).
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
