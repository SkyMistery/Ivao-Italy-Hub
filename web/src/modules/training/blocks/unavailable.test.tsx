import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen } from '@testing-library/react';
import type { ComponentType } from 'react';
import { I18nextProvider } from 'react-i18next';
import { describe, expect, test, vi } from 'vitest';

import type { BlockComponentProps } from '../../../shared/modules';
import { createTestI18n } from '../../../test/harness';
import englishTraining from '../locales/en/training.json';

import { ApprovalQueueBlock } from './approvalQueue';
import { MyTrainingBlock } from './myTraining';
import { TrainerQueueBlock } from './trainerQueue';
import { UpcomingSessionsBlock } from './upcomingSessions';

/**
 * The data blocks of the training when their request fails (#333, item 5): the renderer hands `null` for a request that failed and
 * `undefined` for one on its way (`ContentRenderer`), and a block says which — never «Loading…» for ever.
 */

// The bootstrap never answers: what the block says of a failure does not wait for it.
const api = vi.hoisted(() => ({ get: vi.fn(() => new Promise(() => undefined)) }));

vi.mock('../../../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../../../shared/api/client')),
  api: { GET: api.get },
}));

const i18n = createTestI18n();
i18n.addResourceBundle('en', 'training', englishTraining);

function draw(Block: ComponentType<BlockComponentProps>, data: unknown) {
  render(
    <I18nextProvider i18n={i18n}>
      <QueryClientProvider client={new QueryClient()}>
        <Block id="b1" props={{}} data={data} />
      </QueryClientProvider>
    </I18nextProvider>,
  );
}

const blocks: readonly [string, ComponentType<BlockComponentProps>][] = [
  ['training.approvalQueue', ApprovalQueueBlock],
  ['training.myTraining', MyTrainingBlock],
  ['training.trainerQueue', TrainerQueueBlock],
  ['training.upcomingSessions', UpcomingSessionsBlock],
];

describe.each(blocks)('%s', (_, Block) => {
  test('a request that failed says the block cannot be read', () => {
    draw(Block, null);

    expect(screen.getByText(englishTraining.blocks.unavailable)).toBeInTheDocument();
    expect(screen.queryByText('Loading...')).not.toBeInTheDocument();
  });

  test('a request on its way is loading', () => {
    draw(Block, undefined);

    expect(screen.getByText('Loading...')).toBeInTheDocument();
  });
});
