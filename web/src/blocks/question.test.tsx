import { act, screen, waitFor } from '@testing-library/react';
import { useState } from 'react';
import { beforeEach, describe, expect, test, vi } from 'vitest';

import { renderWithProviders } from '../test/harness';

import { ContentRenderer } from './ContentRenderer';
import { blockDataQuery, encodeProps, questionOf } from './data';
import type { Body } from './envelope';
import { networkStatsSchema, statsSchema } from './schemas';

/**
 * A data block asks the server its question without its words (PR 3.13, finding 1 of the review on #511): the label
 * under a figure is written around the answer, and a letter typed in it is not a new question.
 */

const api = vi.hoisted(() => ({ get: vi.fn() }));

vi.mock('../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../shared/api/client')),
  api: { GET: api.get },
}));

const en = (value: string) => ({ en: value, it: value });

beforeEach(() => {
  api.get.mockReset();
  api.get.mockResolvedValue({
    data: {
      metrics: [
        { metric: 'knownMembers', value: 1284 },
        { metric: 'staffMembers', value: 37 },
        { metric: 'publishedNews', value: 96 },
      ],
    },
    response: new Response(null, { status: 200 }),
  });
});

describe('questionOf', () => {
  test('takes the words out, inside the entries of a list too, and leaves the rest where it was', () => {
    const props = {
      metrics: [{ metric: 'knownMembers', label: en('pilots and controllers') }, { metric: 'staffMembers' }],
      columns: 3,
    };

    const question = questionOf(statsSchema, props);

    expect(question).toEqual({
      metrics: [{ metric: 'knownMembers' }, { metric: 'staffMembers' }],
      columns: 3,
    });
    expect(Object.keys(question)).toEqual(['metrics', 'columns']);
    // The block keeps its words: only the question goes without them.
    expect(props.metrics[0]).toHaveProperty('label');
  });

  test('a block with no words asks exactly what it asked before: same properties, same request, same cache', () => {
    const props = {
      figures: [{ figure: 'divisionAtc' }, { figure: 'divisionPilots' }],
      showPositions: false,
    };

    const question = questionOf(networkStatsSchema, props);

    expect(question).toBe(props);
    expect(encodeProps(question)).toBe(encodeProps(props));
    expect(blockDataQuery('networkStats', question).queryKey).toEqual(
      blockDataQuery('networkStats', props).queryKey,
    );
  });

  test('two blocks asking the same figures under different words ask one question', () => {
    const one = questionOf(statsSchema, {
      metrics: [{ metric: 'knownMembers', label: en('members') }],
      columns: 3,
    });
    const other = questionOf(statsSchema, {
      metrics: [{ metric: 'knownMembers', label: en('people') }],
      columns: 3,
    });

    expect(encodeProps(one)).toBe(encodeProps(other));
  });
});

describe('a live figure on a page', () => {
  function bodyWith(metrics: Record<string, unknown>[]): Body {
    return {
      schemaVersion: 1,
      sections: [
        {
          id: 's_1',
          layout: 'stacked',
          width: 'default',
          background: 'none',
          padding: 'md',
          blocks: [
            {
              id: 'b_stats',
              type: 'stats',
              version: 1,
              props: { metrics, columns: 3 },
              renderMode: 'live',
              frozen: null,
              column: null,
              source: null,
              span: null,
            },
          ],
          sections: [],
        },
      ],
    } as unknown as Body;
  }

  /** The page, with a way to hand it another body the way the editor does at every keystroke. */
  let show: (body: Body) => void = () => undefined;

  function Page({ initial }: { initial: Body }) {
    const [body, setBody] = useState(initial);
    show = setBody;

    return <ContentRenderer body={body} />;
  }

  test('a label typed under a figure starts no request, and a figure changed starts one', async () => {
    renderWithProviders(<Page initial={bodyWith([{ metric: 'knownMembers' }])} />);

    expect(await screen.findByText('members')).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledTimes(1);

    // Three letters of a label, each one a new body, as the panel applies them.
    for (const typed of ['p', 'pi', 'pil']) {
      act(() => show(bodyWith([{ metric: 'knownMembers', label: en(typed) }])));
    }

    // The figure stays drawn under the new word, never back to "loading", and the server was not asked again.
    expect(await screen.findByText('pil')).toBeInTheDocument();
    expect(screen.getByText('1,284')).toBeInTheDocument();
    expect(api.get).toHaveBeenCalledTimes(1);

    // Another figure is another question.
    act(() => show(bodyWith([{ metric: 'staffMembers', label: en('pil') }])));

    await waitFor(() => expect(api.get).toHaveBeenCalledTimes(2));
  });
});
