import { useCallback, useEffect, useRef, useState } from 'react';

/**
 * The draft saving itself (G15, session 2; decided by Carmine on 11 September 2026 with the six
 * rules of `decisions/2026-09-11-l-editor-che-risponde.md`).
 *
 * What it costs the server is the whole reason the rules exist: every save is a `PUT` and an audit
 * row on a database shared with vIPI. So a save happens **after a pause** and not at every change;
 * **only when something changed** since the last one — the snapshot, not a flag; never while
 * another save is in flight; **never for a row that does not exist yet**, because an autosave that
 * created rows would leave a page behind for everybody who opened "new" and walked away; and a
 * refusal is not retried until the draft changes again, or an address the server refuses would
 * be sent back every ten seconds to be refused again.
 *
 * A **conflict** stops it for good: somebody else saved this row, and saving over them every ten
 * seconds is the one thing an editor must never do by itself. The screen says so; reloading is the
 * way on.
 *
 * ⚠️ It knows nothing about what a draft is. It is handed a string — whatever the screen thinks the
 * draft is, serialised — and a function that saves. That is what keeps it a hook of the editor and
 * not a second copy of the editor's state.
 */

/** Ten seconds without a change. Decided, not tuned: five saves twice as often, twenty loses more. */
export const AUTOSAVE_DELAY = 10_000;

export type SaveOutcome = 'saved' | 'conflict' | 'failed';

export interface Autosave {
  /** Whether the draft differs from what was last stored. */
  dirty: boolean;
  /** Whether a save this hook started is in flight. */
  saving: boolean;
  /** When the draft was last stored, by this hook or by `settle`; `null` until the first time. */
  savedAt: Date | null;
  /** True once a conflict came back: nothing is saved by itself any more. */
  stopped: boolean;
  /** True while the draft as it stands was refused, and has not changed since. */
  failed: boolean;
  /** Saves now, if there is anything to save. Resolves `true` when the draft is stored. */
  flush: () => Promise<boolean>;
  /** Told that somebody else stored this snapshot — a press on "save draft" — so it is not dirty. */
  settle: (snapshot: string) => void;
}

export function useAutosave({
  enabled,
  snapshot,
  busy,
  save,
  delay = AUTOSAVE_DELAY,
}: {
  enabled: boolean;
  snapshot: string;
  /** Whether the screen is already writing: a second save would carry a version the first is moving. */
  busy: boolean;
  save: () => Promise<SaveOutcome>;
  delay?: number;
}): Autosave {
  const [stored, setStored] = useState(snapshot);
  const [savedAt, setSavedAt] = useState<Date | null>(null);
  const [refused, setRefused] = useState<string | null>(null);
  const [stopped, setStopped] = useState(false);
  const [saving, setSaving] = useState(false);

  // The screen hands a fresh `save` every render. Kept here so that a render for any other reason
  // — a query answering, a block selected — does not restart the pause.
  const latestSave = useRef(save);
  useEffect(() => {
    latestSave.current = save;
  });

  const dirty = snapshot !== stored;

  const flush = useCallback(async (): Promise<boolean> => {
    if (!dirty) {
      return true;
    }

    if (stopped) {
      return false;
    }

    const attempt = snapshot;
    setSaving(true);

    try {
      const outcome = await latestSave.current();

      if (outcome === 'saved') {
        setStored(attempt);
        setSavedAt(new Date());
        setRefused(null);
        return true;
      }

      if (outcome === 'conflict') {
        setStopped(true);
      } else {
        setRefused(attempt);
      }

      return false;
    } finally {
      setSaving(false);
    }
  }, [dirty, stopped, snapshot]);

  useEffect(() => {
    if (!enabled || !dirty || busy || saving || stopped || refused === snapshot) {
      return undefined;
    }

    const timer = setTimeout(() => {
      void flush();
    }, delay);

    return () => clearTimeout(timer);
  }, [enabled, dirty, busy, saving, stopped, refused, snapshot, delay, flush]);

  return {
    dirty,
    saving,
    savedAt,
    stopped,
    failed: refused === snapshot,
    flush,
    settle: (saved) => {
      setStored(saved);
      setSavedAt(new Date());
      setRefused(null);
    },
  };
}
