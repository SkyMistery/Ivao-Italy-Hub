import { mkdirSync, mkdtempSync, renameSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { join } from 'node:path';

import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { localesStamp } from '../../localesStamp';

/**
 * The fingerprint the SPA asks the language files with (`?v=`): it moves when a word does, and only
 * then — a stamp that changed on every build would throw away the browser's cache for nothing, one
 * that did not change with a word would leave the new keys raw after a delivery.
 */
describe('localesStamp', () => {
  let directory: string;

  beforeEach(() => {
    directory = mkdtempSync(join(tmpdir(), 'hub-locales-'));
    mkdirSync(join(directory, 'en'));
    mkdirSync(join(directory, 'xx'));
    writeFileSync(join(directory, 'en', 'common.json'), '{"title":"Home"}');
    writeFileSync(join(directory, 'xx', 'common.json'), '{"title":"Xome"}');
  });

  afterEach(() => {
    rmSync(directory, { recursive: true, force: true });
  });

  it('is the same for the same files', () => {
    expect(localesStamp(directory)).toBe(localesStamp(directory));
  });

  it('changes when a word changes', () => {
    const before = localesStamp(directory);
    writeFileSync(join(directory, 'xx', 'common.json'), '{"title":"Xome!"}');

    expect(localesStamp(directory)).not.toBe(before);
  });

  it('changes when a file is added', () => {
    const before = localesStamp(directory);
    writeFileSync(join(directory, 'en', 'events.json'), '{}');

    expect(localesStamp(directory)).not.toBe(before);
  });

  it('changes when a file moves to another language', () => {
    writeFileSync(join(directory, 'en', 'events.json'), '{}');
    const before = localesStamp(directory);
    renameSync(join(directory, 'en', 'events.json'), join(directory, 'xx', 'events.json'));

    expect(localesStamp(directory)).not.toBe(before);
  });
});
