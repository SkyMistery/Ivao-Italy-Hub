import { act, renderHook } from '@testing-library/react';
import { afterEach, beforeEach, expect, test, vi } from 'vitest';

import { useAutosave, type SaveOutcome } from './useAutosave';

/**
 * The six rules of the draft that saves itself, on a fake clock: after a pause and not at every
 * change, only when something changed, never for a row that does not exist yet, never twice at
 * once, not again after a refusal until the draft moves, and never again after a conflict.
 */

beforeEach(() => {
  vi.useFakeTimers();
});

afterEach(() => {
  vi.useRealTimers();
});

function saving(outcomes: SaveOutcome[] = []) {
  const queue = [...outcomes];
  return vi.fn(() => Promise.resolve(queue.shift() ?? ('saved' as const)));
}

function mount(save: () => Promise<SaveOutcome>, enabled = true) {
  return renderHook(
    ({ snapshot, busy }: { snapshot: string; busy: boolean }) =>
      useAutosave({ enabled, snapshot, busy, save, delay: 10_000 }),
    { initialProps: { snapshot: 'as loaded', busy: false } },
  );
}

test('a change is saved after the pause, once, and a change during the pause restarts it', async () => {
  const save = saving();
  const { result, rerender } = mount(save);

  expect(result.current.dirty).toBe(false);

  rerender({ snapshot: 'a letter', busy: false });
  expect(result.current.dirty).toBe(true);

  await act(() => vi.advanceTimersByTimeAsync(8_000));
  expect(save).not.toHaveBeenCalled();

  // Typing again: the ten seconds start over from here.
  rerender({ snapshot: 'a word', busy: false });
  await act(() => vi.advanceTimersByTimeAsync(8_000));
  expect(save).not.toHaveBeenCalled();

  await act(() => vi.advanceTimersByTimeAsync(2_000));
  expect(save).toHaveBeenCalledTimes(1);
  expect(result.current.dirty).toBe(false);
  expect(result.current.savedAt).not.toBeNull();

  // And nothing more, because nothing changed.
  await act(() => vi.advanceTimersByTimeAsync(60_000));
  expect(save).toHaveBeenCalledTimes(1);
});

test('a row that does not exist yet is never saved by itself', async () => {
  const save = saving();
  const { result, rerender } = mount(save, false);

  rerender({ snapshot: 'typed into a new page', busy: false });
  await act(() => vi.advanceTimersByTimeAsync(30_000));

  expect(save).not.toHaveBeenCalled();
  // But it is dirty, which is what the guard on the way out reads.
  expect(result.current.dirty).toBe(true);
});

test('nothing starts while the screen is already writing', async () => {
  const save = saving();
  const { rerender } = mount(save);

  rerender({ snapshot: 'changed', busy: true });
  await act(() => vi.advanceTimersByTimeAsync(20_000));
  expect(save).not.toHaveBeenCalled();

  rerender({ snapshot: 'changed', busy: false });
  await act(() => vi.advanceTimersByTimeAsync(10_000));
  expect(save).toHaveBeenCalledTimes(1);
});

test('a refusal is not sent back until the draft changes, and a conflict stops it for good', async () => {
  const save = saving(['failed', 'saved', 'conflict']);
  const { result, rerender } = mount(save);

  rerender({ snapshot: 'an address the server refuses', busy: false });
  await act(() => vi.advanceTimersByTimeAsync(10_000));
  expect(save).toHaveBeenCalledTimes(1);
  expect(result.current.failed).toBe(true);
  expect(result.current.dirty).toBe(true);

  // Ten more seconds of nothing: the same draft would only be refused again.
  await act(() => vi.advanceTimersByTimeAsync(10_000));
  expect(save).toHaveBeenCalledTimes(1);

  // A change is a new draft, and it goes.
  rerender({ snapshot: 'an address that is fine', busy: false });
  expect(result.current.failed).toBe(false);
  await act(() => vi.advanceTimersByTimeAsync(10_000));
  expect(save).toHaveBeenCalledTimes(2);
  expect(result.current.dirty).toBe(false);

  // Somebody else saved the row in between: this one stops, and says so.
  rerender({ snapshot: 'typed after somebody else saved', busy: false });
  await act(() => vi.advanceTimersByTimeAsync(10_000));
  expect(save).toHaveBeenCalledTimes(3);
  expect(result.current.stopped).toBe(true);

  rerender({ snapshot: 'typed again', busy: false });
  await act(() => vi.advanceTimersByTimeAsync(30_000));
  expect(save).toHaveBeenCalledTimes(3);
  expect(await result.current.flush()).toBe(false);
});

test('a press on save settles the draft, and flushing saves at once', async () => {
  const save = saving();
  const { result, rerender } = mount(save);

  rerender({ snapshot: 'pressed', busy: false });
  act(() => result.current.settle('pressed'));
  expect(result.current.dirty).toBe(false);
  await act(() => vi.advanceTimersByTimeAsync(20_000));
  expect(save).not.toHaveBeenCalled();

  rerender({ snapshot: 'leaving', busy: false });
  let stored = false;
  await act(async () => {
    stored = await result.current.flush();
  });
  expect(stored).toBe(true);
  expect(save).toHaveBeenCalledTimes(1);
  expect(result.current.dirty).toBe(false);
});
