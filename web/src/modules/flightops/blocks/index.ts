import { ClipboardCheck, ListChecks, Map as MapIcon, MessagesSquare, Plane } from 'lucide-react';
import { z } from 'zod';

import type { BlockRegistration } from '../../../shared/modules';

import { ErrorCatalogBlock, type ErrorCatalogData } from './errorCatalog';
import { MyToursBlock, type MyToursData } from './myTours';
import { OpenIssuesBlock, type OpenIssuesData } from './openIssues';
import { ReviewQueueBlock, type ReviewQueueData } from './reviewQueue';
import { TourCardsBlock, type TourCardsData } from './tourCards';

/** The three states a card can be in, as `PublicTours.CardStates` names them on the server. */
export const TOUR_CARD_STATES = ['Open', 'Closing', 'Upcoming'] as const;

/**
 * The blocks of the tours, as the manifest lists them. Each has its other half in `FlightOpsModule.Blocks` on the server,
 * and the manifest test compares the two.
 */

export const errorCatalogBlock: BlockRegistration = {
  type: 'flightops.errorCatalog',
  version: 1,
  kind: 'Data',
  alwaysLive: true,
  schema: z.object({}),
  component: ErrorCatalogBlock,
  example: {},
  exampleData: {
    items: [
      {
        id: 1,
        name: { en: 'Disconnection in flight', it: 'Disconnessione in volo' },
        description: {
          en: 'The pilot was **offline** longer than the rule allows.',
          it: 'Il pilota è rimasto **disconnesso** più a lungo di quanto la regola consenta.',
        },
        examples: {
          en: 'Twenty minutes off the network over the sea.',
          it: 'Venti minuti fuori rete sul mare.',
        },
        category: 'Warning',
        yearlyMax: 3,
        rules: [{ code: 'GR4', title: { en: 'Connection', it: 'Connessione' } }],
      },
    ],
  } satisfies ErrorCatalogData,
  editorLabelKey: 'flightops:blocks.errorCatalog.label',
  group: 'data',
  icon: ListChecks,
};

export const tourCardsBlock: BlockRegistration = {
  type: 'flightops.tourCards',
  version: 1,
  kind: 'Data',
  alwaysLive: true,
  schema: z.object({
    // Nothing chosen shows all three, which is what `/tours` shows: a property that narrows never
    // widens, and an empty one is not a way of asking for nothing.
    states: z.array(z.string()).meta({ multi: true, choices: [...TOUR_CARD_STATES] }),
    limit: z.number().int().min(0).default(0),
  }),
  component: TourCardsBlock,
  example: { states: [], limit: 3 },
  exampleData: {
    items: [
      {
        id: 1,
        slug: 'round-the-alps',
        kind: 'Sequential',
        title: { en: 'Round the Alps', it: 'Giro delle Alpi' },
        summary: {
          en: 'Eight legs between the valleys, one a week.',
          it: 'Otto tratte fra le valli, una a settimana.',
        },
        coverMediaId: null,
        state: 'Open',
        releaseAt: '2026-09-01T00:00:00.000Z',
        closeAt: '2026-12-31T23:59:00.000Z',
        legs: 8,
        totalNm: 1420,
      },
    ],
  } satisfies TourCardsData,
  editorLabelKey: 'flightops:blocks.tourCards.label',
  // Its properties are the module's words, not the core's: the first block that needed this
  // (`BlockRegistration.propertyLabels`, T10).
  propertyLabels: 'flightops:blocks.tourCards',
  group: 'data',
  icon: MapIcon,
};

/**
 * The queue of the validators on a dashboard (T13b): one line per tour, for whoever is looking. No property — a dashboard
 * that wants it shows it, and what it holds is the reader's.
 */
export const reviewQueueBlock: BlockRegistration = {
  type: 'flightops.reviewQueue',
  version: 1,
  kind: 'Data',
  alwaysLive: true,
  schema: z.object({}),
  component: ReviewQueueBlock,
  example: {},
  exampleData: {
    items: [
      {
        tourId: 1,
        title: { en: 'Round the Alps', it: 'Giro delle Alpi' },
        count: 4,
        oldest: '2026-09-20T08:30:00.000Z',
      },
    ],
  } satisfies ReviewQueueData,
  editorLabelKey: 'flightops:blocks.reviewQueue.label',
  group: 'data',
  icon: ClipboardCheck,
};

/**
 * What else waits for the tours' staff (T14b): the issues on the legs, the disputes and the clarifications nobody answered,
 * each a number and a link. No property: it is the reader's.
 */
export const openIssuesBlock: BlockRegistration = {
  type: 'flightops.openIssues',
  version: 1,
  kind: 'Data',
  alwaysLive: true,
  schema: z.object({}),
  component: OpenIssuesBlock,
  example: {},
  exampleData: { legIssues: 2, disputes: 1, clarifications: 3, department: 'fod' } satisfies OpenIssuesData,
  editorLabelKey: 'flightops:blocks.openIssues.label',
  group: 'data',
  icon: MessagesSquare,
};

/**
 * The reader's own tours (T15b): started tours with how far and the next leg, the reports to correct, the answers to read,
 * and the summary. No property — it is the reader's — and nothing for a visitor: it belongs on `/me`.
 */
export const myToursBlock: BlockRegistration = {
  type: 'flightops.myTours',
  version: 1,
  kind: 'Data',
  alwaysLive: true,
  schema: z.object({}),
  component: MyToursBlock,
  example: {},
  exampleData: {
    signedIn: true,
    tours: [
      {
        tourId: 1,
        slug: 'round-the-alps',
        title: { en: 'Round the Alps', it: 'Giro delle Alpi' },
        parentTourId: null,
        startedAt: '2026-09-02T18:00:00.000Z',
        completedAt: null,
        done: 3,
        target: 8,
        unit: 'Legs',
        next: { id: 4, number: 4, departureIcao: 'XXAA', arrivalIcao: 'XXBB' },
      },
    ],
    toModify: [],
    answered: [
      {
        id: 12,
        kind: 'clarification',
        subject: 'The approach at leg 2',
        updatedAt: '2026-09-20T09:15:00.000Z',
      },
    ],
    summary: { legsAccepted: 11, minutesFlown: 1265, toursCompleted: 1 },
  } satisfies MyToursData,
  editorLabelKey: 'flightops:blocks.myTours.label',
  group: 'data',
  icon: Plane,
};
