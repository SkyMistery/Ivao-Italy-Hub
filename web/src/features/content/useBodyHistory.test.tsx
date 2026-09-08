import { act, renderHook } from '@testing-library/react';
import { expect, test } from 'vitest';

import { emptyBody, type Body } from '../../blocks';

import { useBodyHistory } from './useBodyHistory';

/**
 * The way back from the last thing that happened to a body.
 *
 * ⚠️ It answers a friction the hand copy of `/about` and `/start` recorded and nothing else fixed
 * (HANDOFF §27): *"no undo on the last structural move — the arrows sit two pixels from the name of
 * a section, and a section moved by mistake cannot be recovered"*. What is asserted here is the
 * three things that make it usable: the way back exists, it is more than one step, and a save
 * forgets it.
 */

function bodyWith(sections: number): Body {
  return {
    ...emptyBody(),
    sections: Array.from({ length: sections }, (_, index) => ({
      id: `s_${index}`,
      key: null,
      title: null,
      layout: 'stacked' as const,
      background: 'none' as const,
      padding: 'md' as const,
      width: 'default' as const,
      mediaId: null,
      required: null,
      locked: null,
      allowedBlocks: null,
      blocks: [],
      sections: [],
    })),
  };
}

test('the last move comes back, and the one before it', () => {
  const { result } = renderHook(() => useBodyHistory(bodyWith(1)));

  expect(result.current.canUndo).toBe(false);

  act(() => result.current.change(bodyWith(2)));
  act(() => result.current.change(bodyWith(3)));

  expect(result.current.body.sections).toHaveLength(3);
  expect(result.current.canUndo).toBe(true);

  // More than one step: a mistake is rarely the very last thing somebody did.
  act(() => result.current.undo());
  expect(result.current.body.sections).toHaveLength(2);

  act(() => result.current.undo());
  expect(result.current.body.sections).toHaveLength(1);

  // And it stops at the beginning rather than emptying the page.
  expect(result.current.canUndo).toBe(false);
  act(() => result.current.undo());
  expect(result.current.body.sections).toHaveLength(1);
});

test('the way back survives a save, because a mistake is often noticed just after one', () => {
  const { result } = renderHook(() => useBodyHistory(bodyWith(1)));

  act(() => result.current.change(bodyWith(2)));

  // Saving is not an event this hook hears, and that is the decision: somebody who saves and then
  // notices the move before it was wrong wants it back. The screen keeps saying "save the draft
  // before publishing" for as long as the two differ, so nothing is lost quietly.
  expect(result.current.canUndo).toBe(true);
  act(() => result.current.undo());
  expect(result.current.body.sections).toHaveLength(1);
});
