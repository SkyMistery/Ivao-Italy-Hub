import { screen, waitFor } from '@testing-library/react';
import { beforeEach, expect, test, vi } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';
import { renderWithProviders } from '../../test/harness';

import { LiveStatusStrip } from './LiveStatusStrip';

/**
 * The strip with the airports a page names (M4, E4b; design M4 §7.1): it counts those airports instead
 * of the division, under a title of its own, and the question it asks the server carries them. Without
 * them the question is the division's, word for word — the strip on top of every public page must not
 * change because an event's page can now ask for something else.
 *
 * The codes are made up on purpose: the strip never reads them, it hands them to the server.
 */

const api = vi.hoisted(() => ({ get: vi.fn() }));

vi.mock('../api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../api/client')),
  api: { GET: api.get },
}));

const answered = {
  updatedAt: '2026-10-06T16:00:00Z',
  figures: [
    { figure: 'divisionAtc', value: 2 },
    { figure: 'divisionPilots', value: 15 },
  ],
};

beforeEach(() => {
  api.get.mockReset();
  api.get.mockResolvedValue({ data: answered, response: new Response(null, { status: 200 }) });
});

/** The properties of the one question the strip asked, decoded from the address as the server decodes them. */
async function askedProps(): Promise<Record<string, unknown>> {
  await waitFor(() => expect(api.get).toHaveBeenCalledTimes(1));

  const [path, init] = api.get.mock.calls[0] as [
    string,
    { params: { path: { type: string }; query: { props: string } } },
  ];
  expect(path).toBe('/api/blocks/data/{type}');
  expect(init.params.path.type).toBe('networkStats');

  const base64 = init.params.query.props.replaceAll('-', '+').replaceAll('_', '/');
  return JSON.parse(
    new TextDecoder().decode(Uint8Array.from(atob(base64), (char) => char.charCodeAt(0))),
  ) as Record<string, unknown>;
}

test('with airports, the title says so and the figures are drawn as for the division', () => {
  renderWithProviders(<LiveStatusStrip airports={['XAAA', 'XBBB']} status={answered} />);

  expect(screen.getByText(englishCommon.liveStatus.airportsTitle)).toBeInTheDocument();
  expect(screen.queryByText(englishCommon.liveStatus.title)).not.toBeInTheDocument();

  // The same two figures and the same words: "controllers" and "pilots" say what they say of any area.
  expect(screen.getByText('2')).toBeInTheDocument();
  expect(screen.getByText(englishCommon.blocks.networkStats.captions.divisionAtc)).toBeInTheDocument();
  expect(screen.getByText('15')).toBeInTheDocument();
  expect(screen.getByText(englishCommon.blocks.networkStats.captions.divisionPilots)).toBeInTheDocument();

  // Given its answer, it asks nobody, as without airports.
  expect(api.get).not.toHaveBeenCalled();
});

test('with airports, the question to the server carries them', async () => {
  renderWithProviders(<LiveStatusStrip airports={['XAAA', 'XBBB']} />);

  expect(await askedProps()).toEqual({
    figures: [{ figure: 'divisionAtc' }, { figure: 'divisionPilots' }],
    showPositions: false,
    airports: ['XAAA', 'XBBB'],
  });

  expect(await screen.findByText(englishCommon.liveStatus.airportsTitle)).toBeInTheDocument();
});

test('without airports, the question is the division one, as it always was', async () => {
  renderWithProviders(<LiveStatusStrip />);

  expect(await askedProps()).toEqual({
    figures: [{ figure: 'divisionAtc' }, { figure: 'divisionPilots' }],
    showPositions: false,
  });

  expect(await screen.findByText(englishCommon.liveStatus.title)).toBeInTheDocument();
  expect(screen.queryByText(englishCommon.liveStatus.airportsTitle)).not.toBeInTheDocument();
});

test('a list with no airport in it is still a list of airports, never the division', async () => {
  // The server reads an empty list as "these airports, none of them", and counts nobody: an event that
  // named no airport must not draw the division's figures under the airports' title.
  renderWithProviders(<LiveStatusStrip airports={[]} />);

  expect(await askedProps()).toMatchObject({ airports: [] });
  expect(await screen.findByText(englishCommon.liveStatus.airportsTitle)).toBeInTheDocument();
});
