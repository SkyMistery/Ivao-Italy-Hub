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
  /** Whether a write is in flight or waiting its turn: a save after the pause, a flush, or an `exclusive` one. */
  saving: boolean;
  /** When the draft was last stored, by this hook or by `settle`; `null` until the first time. */
  savedAt: Date | null;
  /** True once a conflict came back: nothing is saved by itself any more. */
  stopped: boolean;
  /** True while the draft as it stands was refused, and has not changed since. */
  failed: boolean;
  /**
   * Saves now, if there is anything to save, after any write already in flight. Resolves `true`
   * when the draft is stored.
   */
  flush: () => Promise<boolean>;
  /** Told that somebody else stored this snapshot — a press on "save draft" — so it is not dirty. */
  settle: (snapshot: string) => void;
  /**
   * Runs a write of the screen's own — a press on "save draft", a publication — after the one in
   * flight, and holds the next one until it is over. ⚠️ Every write of the row goes through here or
   * through `flush`: two at once carry the same version, and the second is answered 409 (issue #332,
   * item 8). A task must not call `flush` itself: it would wait for its own end.
   */
  exclusive: <T>(task: () => Promise<T>) => Promise<T>;
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
  // How many writes are in flight or waiting their turn.
  const [writes, setWrites] = useState(0);
  const saving = writes > 0;

  // The screen hands a fresh `save` and a fresh snapshot every render. Kept here so that a render
  // for any other reason — a query answering, a block selected — does not restart the pause, and so
  // that a save that waited for another one sends the draft as it stands **then**.
  const latest = useRef({ save, snapshot });
  useEffect(() => {
    latest.current = { save, snapshot };
  });

  // What was last stored and whether a conflict came back, as the writes see them: a write that
  // waited reads them after the one before it ended, which a render may not have drawn yet.
  const storedNow = useRef(snapshot);
  const stoppedNow = useRef(false);

  // The writes, one after the other: each one starts when the one before it has ended, however it ended.
  const queue = useRef<Promise<unknown>>(Promise.resolve());

  const dirty = snapshot !== stored;

  const exclusive = useCallback(<T>(task: () => Promise<T>): Promise<T> => {
    setWrites((count) => count + 1);
    const run = queue.current.then(task).finally(() => setWrites((count) => count - 1));
    queue.current = run.catch(() => undefined);
    return run;
  }, []);

  const flush = useCallback(
    (): Promise<boolean> =>
      exclusive(async () => {
        const attempt = latest.current.snapshot;
        if (attempt === storedNow.current) {
          return true;
        }

        if (stoppedNow.current) {
          return false;
        }

        const outcome = await latest.current.save();

        if (outcome === 'saved') {
          storedNow.current = attempt;
          setStored(attempt);
          setSavedAt(new Date());
          setRefused(null);
          return true;
        }

        if (outcome === 'conflict') {
          stoppedNow.current = true;
          setStopped(true);
        } else {
          setRefused(attempt);
        }

        return false;
      }),
    [exclusive],
  );

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
      storedNow.current = saved;
      setStored(saved);
      setSavedAt(new Date());
      setRefused(null);
    },
    exclusive,
  };
}
