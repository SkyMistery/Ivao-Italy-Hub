import { act, fireEvent, render, renderHook, screen } from '@testing-library/react';
import { expect, test, vi } from 'vitest';

import { emptyBody, type Body } from '../../blocks';

import { useBodyHistory, useHistoryShortcuts, useSelectionShortcuts } from './useBodyHistory';

/**
 * The way back — and forth — from the last things that happened to a body.
 *
 * ⚠️ It answers a friction the hand copy of `/about` and `/start` recorded and nothing else fixed
 * (HANDOFF §27): *"no undo on the last structural move — the arrows sit two pixels from the name of
 * a section, and a section moved by mistake cannot be recovered"*. What is asserted here is what
 * makes it usable: the way back exists, it is more than one step, a save forgets it, an undo can be
 * undone, and a run of typing is one step and not twenty (G15).
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
  act(() => {
    result.current.undo();
  });
  expect(result.current.body.sections).toHaveLength(2);

  act(() => {
    result.current.undo();
  });
  expect(result.current.body.sections).toHaveLength(1);

  // And it stops at the beginning rather than emptying the page.
  expect(result.current.canUndo).toBe(false);
  act(() => {
    result.current.undo();
  });
  expect(result.current.body.sections).toHaveLength(1);
});

test('the way back survives a save, because a mistake is often noticed just after one', () => {
  const { result } = renderHook(() => useBodyHistory(bodyWith(1)));

  act(() => result.current.change(bodyWith(2)));

  // Saving is not an event this hook hears, and that is the decision: somebody who saves and then
  // notices the move before it was wrong wants it back. The screen keeps saying "save the draft
  // before publishing" for as long as the two differ, so nothing is lost quietly.
  expect(result.current.canUndo).toBe(true);
  act(() => {
    result.current.undo();
  });
  expect(result.current.body.sections).toHaveLength(1);
});

test('an undo can be undone, until something new is done', () => {
  const { result } = renderHook(() => useBodyHistory(bodyWith(1)));

  expect(result.current.canRedo).toBe(false);

  act(() => result.current.change(bodyWith(2)));
  act(() => result.current.change(bodyWith(3)));
  act(() => {
    result.current.undo();
  });
  act(() => {
    result.current.undo();
  });

  expect(result.current.body.sections).toHaveLength(1);
  expect(result.current.canRedo).toBe(true);

  act(() => {
    result.current.redo();
  });
  expect(result.current.body.sections).toHaveLength(2);

  // Doing something else after an undo is a new branch: the old future is gone, because two
  // futures would be a tree and nobody navigates a tree with one key.
  act(() => result.current.change(bodyWith(5)));
  expect(result.current.canRedo).toBe(false);
  act(() => {
    result.current.redo();
  });
  expect(result.current.body.sections).toHaveLength(5);

  // And what was just done can still be undone, back to where the branch started.
  act(() => {
    result.current.undo();
  });
  expect(result.current.body.sections).toHaveLength(2);
});

test('a run of changes with the same key is one step, and another key closes the run', () => {
  const { result } = renderHook(() => useBodyHistory(bodyWith(1)));

  // Typing a sentence into a block: the properties apply at every pause, and every one of those
  // is a change with the same key.
  act(() => result.current.change(bodyWith(2), { coalesce: 'props:b_1' }));
  act(() => result.current.change(bodyWith(3), { coalesce: 'props:b_1' }));
  act(() => result.current.change(bodyWith(4), { coalesce: 'props:b_1' }));

  expect(result.current.body.sections).toHaveLength(4);

  // One undo takes the whole sentence away, back to before the first letter.
  act(() => {
    result.current.undo();
  });
  expect(result.current.body.sections).toHaveLength(1);
  expect(result.current.canUndo).toBe(false);

  // Typing into another block is another run.
  act(() => result.current.change(bodyWith(2), { coalesce: 'props:b_1' }));
  act(() => result.current.change(bodyWith(3), { coalesce: 'props:b_2' }));
  act(() => result.current.change(bodyWith(4), { coalesce: 'props:b_2' }));

  act(() => {
    result.current.undo();
  });
  expect(result.current.body.sections).toHaveLength(2);
  act(() => {
    result.current.undo();
  });
  expect(result.current.body.sections).toHaveLength(1);
});

test('a move in the middle of typing closes the run, and typing after an undo opens a new one', () => {
  const { result } = renderHook(() => useBodyHistory(bodyWith(1)));

  act(() => result.current.change(bodyWith(2), { coalesce: 'props:b_1' }));
  // A move, an add, a swatch: a change with no key.
  act(() => result.current.change(bodyWith(3)));
  act(() => result.current.change(bodyWith(4), { coalesce: 'props:b_1' }));

  act(() => {
    result.current.undo();
  });
  expect(result.current.body.sections).toHaveLength(3);
  act(() => {
    result.current.undo();
  });
  expect(result.current.body.sections).toHaveLength(2);

  // Undoing closed the run: the next letter is a step of its own, not a continuation of the one
  // that was just taken away.
  act(() => result.current.change(bodyWith(6), { coalesce: 'props:b_1' }));
  act(() => {
    result.current.undo();
  });
  expect(result.current.body.sections).toHaveLength(2);
});

function Keys({ undo, redo }: { undo: () => boolean; redo: () => boolean }) {
  useHistoryShortcuts(undo, redo);
  return (
    <>
      <textarea aria-label="A field" />
      <button type="button">A button</button>
    </>
  );
}

function PickedKeys({ handlers }: { handlers: Parameters<typeof useSelectionShortcuts>[0] }) {
  useSelectionShortcuts(handlers);
  return (
    <>
      <textarea aria-label="A field" />
      <button type="button">A button</button>
    </>
  );
}

test('Delete, ⌘D and Escape act on what is picked, outside a field, and only when there is something to do', () => {
  const remove = vi.fn(() => true);
  const duplicate = vi.fn(() => false);
  const release = vi.fn(() => true);
  render(<PickedKeys handlers={{ remove, duplicate, release }} />);

  const button = screen.getByRole('button', { name: 'A button' });

  const deleting = fireEvent.keyDown(button, { key: 'Delete' });
  expect(remove).toHaveBeenCalledTimes(1);
  // Kept from the browser, because something was done.
  expect(deleting).toBe(false);

  // Nothing to duplicate: the key stays the browser's — Ctrl+D is a bookmark there.
  const bookmarking = fireEvent.keyDown(button, { key: 'd', ctrlKey: true });
  expect(duplicate).toHaveBeenCalledTimes(1);
  expect(bookmarking).toBe(true);

  fireEvent.keyDown(button, { key: 'Escape' });
  expect(release).toHaveBeenCalledTimes(1);

  // Inside a field, Delete deletes a character and nothing else.
  fireEvent.keyDown(screen.getByRole('textbox', { name: 'A field' }), { key: 'Delete' });
  expect(remove).toHaveBeenCalledTimes(1);
});

test('⌘Z is the editor’s outside a field and the browser’s inside one', () => {
  const undo = vi.fn(() => true);
  const redo = vi.fn(() => true);
  render(<Keys undo={undo} redo={redo} />);

  // Inside a field the key means "undo what I typed", and a page-wide handler must leave it alone.
  fireEvent.keyDown(screen.getByRole('textbox', { name: 'A field' }), { key: 'z', ctrlKey: true });
  expect(undo).not.toHaveBeenCalled();

  const button = screen.getByRole('button', { name: 'A button' });
  fireEvent.keyDown(button, { key: 'z', ctrlKey: true });
  expect(undo).toHaveBeenCalledTimes(1);

  fireEvent.keyDown(button, { key: 'z', ctrlKey: true, shiftKey: true });
  fireEvent.keyDown(button, { key: 'y', ctrlKey: true });
  expect(redo).toHaveBeenCalledTimes(2);

  // ⌘ on a Mac is the same key as Ctrl here.
  fireEvent.keyDown(button, { key: 'z', metaKey: true });
  expect(undo).toHaveBeenCalledTimes(2);

  // And without a modifier it is a letter.
  fireEvent.keyDown(button, { key: 'z' });
  expect(undo).toHaveBeenCalledTimes(2);
});
