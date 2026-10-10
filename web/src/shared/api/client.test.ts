import { expect, test } from 'vitest';

import { loginHref } from './client';

/**
 * The return address of a login, by its value.
 *
 * The server takes back only a path of this site (`SafeReturnUrl`) and answers `/` to anything else
 * without a word, so the mistake these tests are for shows nowhere: the visitor signs in and lands
 * on the home page. `AuthenticationTests` proves the server's half; this is the browser's.
 */

/** What the server would read. */
const returnUrlOf = (href: string) => new URL(href, window.location.origin).searchParams.get('returnUrl');

test('the login goes to the server endpoint and carries the address whole: path, query and hash', () => {
  const href = loginHref('/tours/xx-test/report?leg=2&from=a%20b#notes');

  expect(href).toBe('/auth/login?returnUrl=%2Ftours%2Fxx-test%2Freport%3Fleg%3D2%26from%3Da%2520b%23notes');
  expect(returnUrlOf(href)).toBe('/tours/xx-test/report?leg=2&from=a%20b#notes');
});

test('the address of the window, which is absolute, is reduced to its path', () => {
  // `location.href` is two things under one name: the router's begins with a slash, the window's
  // with the scheme. The second one used to go out as it was.
  const absolute = `${window.location.origin}/training/sessions/41?tab=people#trainer`;

  expect(returnUrlOf(loginHref(absolute))).toBe('/training/sessions/41?tab=people#trainer');
});

test.each([
  'https://elsewhere.example/tours',
  '//elsewhere.example/tours',
  // Browsers read a backslash as a slash: this one is `//elsewhere.example`.
  '/\\elsewhere.example/tours',
  'javascript:alert(1)',
  'http://',
])('an address that is not of this site (%s) becomes the home page', (address) => {
  expect(returnUrlOf(loginHref(address))).toBe('/');
});
