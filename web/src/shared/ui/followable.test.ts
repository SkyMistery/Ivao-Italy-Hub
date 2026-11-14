import { expect, test } from 'vitest';

import addresses from '../../../../tests/fixtures/content-addresses.json';
import { isFollowable } from './followable';

/** The same cases the server's half is held to (`BlockDocumentWalkerTests`): the two halves of one rule cannot drift. */
test.each(addresses.followable)('%j is an address to follow', (address) => {
  expect(isFollowable(address)).toBe(true);
});

test.each(addresses.refused)('%j is not', (address) => {
  expect(isFollowable(address)).toBe(false);
});
