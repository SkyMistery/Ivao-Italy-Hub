import { useState } from 'react';

import type { Body } from '../../blocks';

/**
 * The body the editor is working on, and a way back from the last thing that happened to it.
 *
 * ⚠️ It exists because of a friction recorded while `/about` and `/start` were being copied across
 * by hand (HANDOFF §27): *"no undo on the last structural move — the arrows sit two pixels from the
 * name of a section, and a section moved by mistake cannot be recovered"*. Everything the editor
 * does to a body goes through one function, so one function is where the way back belongs.
 *
 * It undoes **the body** and nothing else: the metadata form has its own fields and its own reset,
 * and a text box has the browser's undo, which this must not fight. That is also why there is no
 * keyboard shortcut — ⌘Z inside a field means "undo what I typed", and a page-wide handler would
 * take it away.
 *
 * A stack rather than one step, capped: twenty moves is more than anybody remembers making, and an
 * uncapped one keeps every version of a body a long editing session ever produced.
 *
 * ⚠️ Saving does **not** clear it, and that is deliberate: somebody who saves and then notices that
 * the move before the save was wrong wants it back, and the screen already says "save the draft
 * before publishing" for as long as the two differ.
 */
const LIMIT = 20;

export interface BodyHistory {
  body: Body;
  /** Replaces the body, remembering the one it replaces. */
  change: (next: Body) => void;
  /** Puts the previous body back. Does nothing when there is none. */
  undo: () => void;
  canUndo: boolean;
}

export function useBodyHistory(initial: Body): BodyHistory {
  const [body, setBody] = useState<Body>(initial);
  const [past, setPast] = useState<Body[]>([]);

  return {
    body,
    change: (next) => {
      setPast((previous) => [...previous.slice(-(LIMIT - 1)), body]);
      setBody(next);
    },
    undo: () => {
      const last = past.at(-1);
      if (last === undefined) {
        return;
      }

      setBody(last);
      setPast(past.slice(0, -1));
    },
    canUndo: past.length > 0,
  };
}
