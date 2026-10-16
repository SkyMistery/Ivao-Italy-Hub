import { screen, waitFor, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { beforeEach, expect, test, vi } from 'vitest';

import englishCommon from '../../../../../locales/en/common.json';
import { renderWithProviders } from '../../../test/harness';

import type { ConnectionCheckDto } from './queries';

/**
 * The connection check of the super administrator (note `2026-10-16-la-prova-dei-collegamenti`): nothing is asked until the
 * button is pressed, and the kind of a failure the server sends becomes the sentence of the language files.
 */

const api = vi.hoisted(() => ({ post: vi.fn() }));

vi.mock('../../../shared/api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../../../shared/api/client')),
  api: { POST: api.post },
}));

const { ConnectionCheckPanel } = await import('./ConnectionCheckPanel');

const words = englishCommon.connections;

const ok = <T,>(data: T) => ({ data, response: new Response(null, { status: 200 }) });

function answer(overrides: Partial<ConnectionCheckDto> = {}): ConnectionCheckDto {
  return {
    database: {
      outcome: 'Ok',
      error: null,
      milliseconds: 12,
      serverVersion: '11.4.10-MariaDB',
      migrations: [
        { context: 'HubDbContext', latest: '20261001_Latest', pending: [] },
        { context: 'EventsDbContext', latest: '20261002_Events', pending: ['20261010_Next'] },
      ],
    },
    archive: { outcome: 'Ok', error: null, milliseconds: 34, latestSessionStart: '2026-10-15T21:30:00Z' },
    ...overrides,
  };
}

beforeEach(() => {
  api.post.mockReset();
});

test('nothing is asked until the button is pressed, then both databases are shown', async () => {
  api.post.mockResolvedValue(ok(answer()));
  renderWithProviders(<ConnectionCheckPanel />);

  expect(api.post).not.toHaveBeenCalled();
  await userEvent.click(screen.getByRole('button', { name: words.run }));

  const database = await screen.findByTestId('connection-database');
  expect(api.post).toHaveBeenCalledWith('/api/admin/diagnostics/connections');
  expect(within(database).getByText(words.outcomes.Ok)).toBeTruthy();
  expect(within(database).getByText('12 ms')).toBeTruthy();
  expect(within(database).getByText('Server: 11.4.10-MariaDB')).toBeTruthy();
  expect(within(database).getByText(words.migrations.upToDate)).toBeTruthy();
  expect(within(database).getByText('1 migration pending: 20261010_Next')).toBeTruthy();

  const archive = screen.getByTestId('connection-archive');
  expect(within(archive).getByText('34 ms')).toBeTruthy();
  expect(within(archive).getByText(/Newest connection in the archive: .*21:30 UTC/)).toBeTruthy();
});

test('a failure is said in words, and an archive the division does not read is said as such', async () => {
  api.post.mockResolvedValue(
    ok(
      answer({
        archive: { outcome: 'Failed', error: 'AccessDenied', milliseconds: 5, latestSessionStart: null },
      }),
    ),
  );
  renderWithProviders(<ConnectionCheckPanel />);
  await userEvent.click(screen.getByRole('button', { name: words.run }));

  const archive = await screen.findByTestId('connection-archive');
  expect(within(archive).getByText(words.outcomes.Failed)).toBeTruthy();
  expect(within(archive).getByText(words.errors.AccessDenied)).toBeTruthy();

  api.post.mockResolvedValue(
    ok(
      answer({
        archive: { outcome: 'NotConfigured', error: null, milliseconds: null, latestSessionStart: null },
      }),
    ),
  );
  await userEvent.click(screen.getByRole('button', { name: words.run }));

  await waitFor(() =>
    expect(
      within(screen.getByTestId('connection-archive')).getByText(words.archive.notConfigured),
    ).toBeTruthy(),
  );
  expect(within(screen.getByTestId('connection-archive')).queryByText(/ ms$/)).toBeNull();
});
