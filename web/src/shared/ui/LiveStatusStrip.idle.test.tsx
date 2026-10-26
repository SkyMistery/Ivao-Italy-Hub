import { act, fireEvent } from '@testing-library/react';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

import { renderWithProviders } from '../../test/harness';

import { LiveStatusStrip } from './LiveStatusStrip';

/**
 * The strip stops asking when nobody is reading (issue #397, note 2026-10-26-la-foto-della-rete-oltre-il-processo): on a
 * host that stops the hub within the minute, each question of a tab left open was a whole start of the hub. It asks
 * every minute while somebody is at the page, nothing after ten minutes without a gesture, and at once at the first one.
 */

const api = vi.hoisted(() => ({ get: vi.fn() }));

vi.mock('../api/client', async () => ({
  ...(await vi.importActual<Record<string, unknown>>('../api/client')),
  api: { GET: api.get },
}));

const MINUTE = 60_000;

beforeEach(() => {
  vi.useFakeTimers();
  api.get.mockReset();
  api.get.mockResolvedValue({
    data: { updatedAt: '2026-10-26T12:00:00Z', figures: [] },
    response: new Response(null, { status: 200 }),
  });
});

afterEach(() => {
  vi.useRealTimers();
});

/** Lets the timers run and the questions they start settle. */
async function wait(ms: number) {
  await act(async () => {
    await vi.advanceTimersByTimeAsync(ms);
  });
}

test('asks every minute while somebody is reading, stops after ten idle minutes, asks at the first gesture', async () => {
  renderWithProviders(<LiveStatusStrip />);
  await wait(0);
  expect(api.get).toHaveBeenCalledTimes(1);

  // Somebody reading: a question a minute.
  await wait(MINUTE);
  expect(api.get).toHaveBeenCalledTimes(2);

  // A gesture at minute five keeps it asking past the tenth.
  await wait(4 * MINUTE);
  fireEvent.pointerMove(window);
  await wait(6 * MINUTE);
  expect(api.get).toHaveBeenCalledTimes(12);

  // Ten minutes from the last gesture it stops, and a tab left open asks nothing more, however long.
  await wait(5 * MINUTE);
  const asked = api.get.mock.calls.length;
  await wait(60 * MINUTE);
  expect(api.get).toHaveBeenCalledTimes(asked);

  // The first gesture asks at once, and the minute starts again.
  fireEvent.keyDown(window, { key: 'ArrowDown' });
  await wait(0);
  expect(api.get).toHaveBeenCalledTimes(asked + 1);
  await wait(MINUTE);
  expect(api.get).toHaveBeenCalledTimes(asked + 2);
});

test('given its answer, it asks nobody, even at a gesture after a pause', async () => {
  renderWithProviders(<LiveStatusStrip status={{ updatedAt: '2026-10-26T12:00:00Z', figures: [] }} />);

  await wait(11 * MINUTE);
  fireEvent.pointerDown(window);
  await wait(MINUTE);

  expect(api.get).not.toHaveBeenCalled();
});
