import { ListChecks, Map as MapIcon } from 'lucide-react';
import { z } from 'zod';

import type { BlockRegistration } from '../../../shared/modules';

import { ErrorCatalogBlock, type ErrorCatalogData } from './errorCatalog';
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
