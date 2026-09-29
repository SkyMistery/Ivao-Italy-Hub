import { expect, test } from 'vitest';

import { TRAINING, sessionHref, sessionTitle } from './site';

/**
 * What the public pages of the training work out from a session the server answered (A10b): the address of its page — the one its entry
 * of the calendar points at — and how it is named, as that entry is titled. The ratings and the positions are made up.
 */

test('a session is at the address its entry of the calendar points at', () => {
  expect(TRAINING).toBe('/training');
  expect(sessionHref(42)).toBe('/training/sessions/42');
});

test('a session is named by its rating and position, the rating alone for a pilot, its number when neither is known', () => {
  expect(sessionTitle({ id: 42, ratingShortName: 'R3', position: 'XXAA_BOX' })).toBe('R3 · XXAA_BOX');
  expect(sessionTitle({ id: 43, ratingShortName: 'P3', position: null })).toBe('P3');
  expect(sessionTitle({ id: 44, ratingShortName: null, position: null })).toBe('#44');
  expect(sessionTitle({ id: 45, ratingShortName: '', position: 'XXAA_BOX' })).toBe('XXAA_BOX');
});
