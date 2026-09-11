import { useEffect, useState } from 'react';

import type { Body } from '../../blocks';

/**
 * The body the editor is working on, and a way back and forth from the last things that happened
 * to it.
 *
 * ⚠️ It exists because of a friction recorded while `/about` and `/start` were being copied across
 * by hand (HANDOFF §27): *"no undo on the last structural move — the arrows sit two pixels from the
 * name of a section, and a section moved by mistake cannot be recovered"*. Everything the editor
 * does to a body goes through one function, so one function is where the way back belongs.
 *
 * It undoes **the body** and nothing else: the metadata form has its own fields and its own reset,
 * and a text box has the browser's undo, which this must not fight. That is also why the keyboard
 * shortcut below does nothing while the focus is in a field — ⌘Z inside a field means "undo what I
 * typed", and a page-wide handler that took it away would be a worse editor, not a better one.
 *
 * **Coalescing** (G15, 11 September 2026) is what lets the properties of a block apply while they
 * are typed without every keystroke becoming a step: two changes in a row with the same `coalesce`
 * key replace the top of the stack instead of adding to it, so a sentence written into a block is
 * one thing to undo. A change with another key, or with none — a move, an add, a swatch — closes
 * the run, and so does undoing.
 *
 * A stack rather than one step, capped: fifty is more moves than anybody remembers making now that
 * a run of typing counts as one, and a body is a few kilobytes.
 *
 * ⚠️ Saving does **not** clear it, and that is deliberate: somebody who saves and then notices that
 * the move before the save was wrong wants it back, and the screen already says "save the draft
 * before publishing" for as long as the two differ.
 */
const LIMIT = 50;

export interface BodyHistory {
  body: Body;
  /**
   * Replaces the body, remembering the one it replaces. With a `coalesce` key equal to the last
   * change's, the last remembered body stays what it was and this one merely replaces the present.
   */
  change: (next: Body, options?: { coalesce?: string }) => void;
  /** Puts the previous body back and returns it; `null` when there is none. */
  undo: () => Body | null;
  /** Puts back the body an undo took away and returns it; `null` when there is none. */
  redo: () => Body | null;
  canUndo: boolean;
  canRedo: boolean;
}

interface State {
  body: Body;
  past: Body[];
  future: Body[];
  /** The `coalesce` key of the last change, while the run it started is still open. */
  run: string | null;
}

export function useBodyHistory(initial: Body): BodyHistory {
  // One state and not three, so that a change and the memory of it can never be one render apart.
  const [state, setState] = useState<State>({ body: initial, past: [], future: [], run: null });

  return {
    body: state.body,
    change: (next, options) => {
      const key = options?.coalesce ?? null;

      setState((current) => ({
        body: next,
        past:
          key !== null && current.run === key
            ? current.past
            : [...current.past.slice(-(LIMIT - 1)), current.body],
        // Anything done after an undo is a new branch, and the old one is gone: two futures would
        // be a tree, and nobody navigates a tree with one key.
        future: [],
        run: key,
      }));
    },
    undo: () => {
      const last = state.past.at(-1);
      if (last === undefined) {
        return null;
      }

      setState({
        body: last,
        past: state.past.slice(0, -1),
        future: [state.body, ...state.future],
        run: null,
      });

      return last;
    },
    redo: () => {
      const next = state.future[0];
      if (next === undefined) {
        return null;
      }

      setState({
        body: next,
        past: [...state.past.slice(-(LIMIT - 1)), state.body],
        future: state.future.slice(1),
        run: null,
      });

      return next;
    },
    canUndo: state.past.length > 0,
    canRedo: state.future.length > 0,
  };
}

/**
 * Whether a key press landed somewhere that has its own idea of what ⌘Z means. A text box, a
 * select and anything editable keep the browser's undo; the page, the outline and the buttons are
 * the editor's.
 */
export function isEditableTarget(target: EventTarget | null): boolean {
  if (!(target instanceof HTMLElement)) {
    return false;
  }

  return (
    target instanceof HTMLInputElement ||
    target instanceof HTMLTextAreaElement ||
    target instanceof HTMLSelectElement ||
    target.isContentEditable
  );
}

/**
 * The keys that act on what is picked (Carmine, 11 September 2026): Delete removes it, ⌘D / Ctrl+D
 * duplicates it, Escape lets go of it. Outside a field only, like the history keys; each handler
 * says whether it did anything, and only then is the key kept from the browser — Ctrl+D is a
 * bookmark otherwise, and stays one when nothing is picked.
 */
export function useSelectionShortcuts(handlers: {
  remove: () => boolean;
  duplicate: () => boolean;
  release: () => boolean;
}): void {
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (event.altKey || isEditableTarget(event.target)) {
        return;
      }

      const modified = event.ctrlKey || event.metaKey;
      const done =
        event.key === 'Delete' && !modified
          ? handlers.remove()
          : event.key.toLowerCase() === 'd' && modified
            ? handlers.duplicate()
            : event.key === 'Escape' && !modified
              ? handlers.release()
              : false;

      if (done) {
        event.preventDefault();
      }
    };

    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [handlers]);
}

/**
 * ⌘Z / Ctrl+Z, ⌘⇧Z / Ctrl+Shift+Z and Ctrl+Y, on the document, for as long as the editor is on
 * screen — and only outside a field, for the reason `useBodyHistory` gives. `preventDefault` only
 * when there was something to do, so a page with nothing to undo leaves the key to the browser.
 */
export function useHistoryShortcuts(undo: () => boolean, redo: () => boolean): void {
  useEffect(() => {
    const onKeyDown = (event: KeyboardEvent) => {
      if (!(event.ctrlKey || event.metaKey) || event.altKey || isEditableTarget(event.target)) {
        return;
      }

      const key = event.key.toLowerCase();
      const wantsRedo = (key === 'z' && event.shiftKey) || key === 'y';
      const wantsUndo = key === 'z' && !event.shiftKey;

      if ((wantsRedo && redo()) || (wantsUndo && undo())) {
        event.preventDefault();
      }
    };

    document.addEventListener('keydown', onKeyDown);
    return () => document.removeEventListener('keydown', onKeyDown);
  }, [undo, redo]);
}
