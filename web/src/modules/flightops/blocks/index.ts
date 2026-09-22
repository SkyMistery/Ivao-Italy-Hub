import { ListChecks } from 'lucide-react';
import { z } from 'zod';

import type { BlockRegistration } from '../../../shared/modules';

import { ErrorCatalogBlock, type ErrorCatalogData } from './errorCatalog';

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
