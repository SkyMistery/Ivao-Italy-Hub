import { beforeEach, describe, expect, it, vi } from 'vitest';

import { claimReload, createBuildWatch, RELOADED_FOR, reloadWhenAChunkIsGone, stampOf } from './newBuild';

const old = { version: '0.5.0', commit: '6261ffd' };
const delivered = { version: '0.5.1', commit: 'a1b2c3d' };

describe('the build a page was served by', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  it('names a build by its number and commit, and by its number alone when it has no commit', () => {
    expect(stampOf(old)).toBe('0.5.0+6261ffd');
    expect(stampOf({ version: '0.5.0', commit: null })).toBe('0.5.0');
  });

  it('is the first bootstrap the page saw, and the same stamp again asks for nothing', () => {
    const watch = createBuildWatch(() => sessionStorage);

    expect(watch.shouldReload(old)).toBe(false);
    expect(watch.shouldReload(old)).toBe(false);
  });

  it('asks for one reload when the server answers with another build', () => {
    const watch = createBuildWatch(() => sessionStorage);
    watch.shouldReload(old);

    expect(watch.shouldReload(delivered)).toBe(true);
    expect(sessionStorage.getItem(RELOADED_FOR + stampOf(delivered))).not.toBeNull();
  });

  it('two commits under one number are two builds', () => {
    const watch = createBuildWatch(() => sessionStorage);
    watch.shouldReload(old);

    expect(watch.shouldReload({ version: '0.5.0', commit: 'fedcba9' })).toBe(true);
  });

  it('never reloads twice for the same build, even when two releases answer in turn', () => {
    // The page before the reload, which met the new build and reloaded for it.
    const before = createBuildWatch(() => sessionStorage);
    before.shouldReload(old);
    expect(before.shouldReload(delivered)).toBe(true);

    // The page after it, first answered by the old process that has not stopped yet: the new build
    // comes back on the next click, and this time the page stays.
    const after = createBuildWatch(() => sessionStorage);
    after.shouldReload(old);
    expect(after.shouldReload(delivered)).toBe(false);
  });

  it('keeps the page when there is nowhere to write the mark', () => {
    const watch = createBuildWatch(() => undefined);
    watch.shouldReload(old);

    expect(watch.shouldReload(delivered)).toBe(false);
  });

  it('keeps the page when the storage refuses to be written', () => {
    const refusing = {
      getItem: () => null,
      setItem: () => {
        throw new DOMException('blocked', 'SecurityError');
      },
    } as unknown as Storage;

    expect(claimReload(refusing, 'x')).toBe(false);
  });
});

describe('a chunk that a delivery took away', () => {
  beforeEach(() => {
    sessionStorage.clear();
  });

  function failedChunk(target: EventTarget, message: string) {
    const event = new Event('vite:preloadError', { cancelable: true }) as Event & { payload?: unknown };
    event.payload = new TypeError(message);
    target.dispatchEvent(event);
    return event;
  }

  it('reloads the page once for that chunk, and lets the error go on to whoever imported it', () => {
    const reload = vi.fn();
    const target = Object.assign(new EventTarget(), { location: { reload } }) as unknown as Window;
    reloadWhenAChunkIsGone(target, () => sessionStorage);

    const message = 'Failed to fetch dynamically imported module: /assets/legs-Ab12.js';
    const first = failedChunk(target, message);
    failedChunk(target, message);

    expect(reload).toHaveBeenCalledTimes(1);
    expect(first.defaultPrevented).toBe(false);

    failedChunk(target, 'Failed to fetch dynamically imported module: /assets/exams-Cd34.js');
    expect(reload).toHaveBeenCalledTimes(2);
  });
});
