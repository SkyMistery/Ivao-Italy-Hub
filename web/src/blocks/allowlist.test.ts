import { expect, test } from 'vitest';

import { embedSource, hostOf } from './allowlist';

/**
 * The allow list is the only thing standing between "an editor typed an address" and "this hub
 * framed a page of somebody else's site under its own". So what is tested here is not that the
 * happy addresses work — it is that the unhappy ones do not.
 */

test('the address of the page a video is on becomes the address of its player', () => {
  expect(embedSource('https://www.youtube.com/watch?v=dQw4w9WgXcQ')).toBe(
    'https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ',
  );
  expect(embedSource('https://youtu.be/dQw4w9WgXcQ')).toBe(
    'https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ',
  );
  expect(embedSource('https://vimeo.com/76979871')).toBe('https://player.vimeo.com/video/76979871');
  expect(embedSource('https://player.vimeo.com/video/76979871')).toBe(
    'https://player.vimeo.com/video/76979871',
  );
});

test('a host that is not on the list is refused, however much it looks like one that is', () => {
  // The trick this is here for: `endsWith('youtube.com')` would let this through, and a frame is
  // then loading whatever that site decides to serve.
  expect(hostOf('https://evil-youtube.com/watch?v=dQw4w9WgXcQ')).toBeNull();
  expect(hostOf('https://youtube.com.example.org/watch?v=dQw4w9WgXcQ')).toBeNull();
  expect(embedSource('https://example.org/watch?v=dQw4w9WgXcQ')).toBeNull();
});

test('a subdomain of an allowed host is allowed, because that is what the entry says', () => {
  expect(hostOf('https://www.youtube.com/watch?v=dQw4w9WgXcQ')?.key).toBe('youtube');
  expect(hostOf('https://m.youtube.com/watch?v=dQw4w9WgXcQ')?.key).toBe('youtube');
});

test('only https is framed', () => {
  // An http frame inside an https page is blocked by the browser, and what a reader sees is a hole
  // with no explanation. Refusing it here is what lets the block say so instead.
  expect(hostOf('http://www.youtube.com/watch?v=dQw4w9WgXcQ')).toBeNull();
  expect(hostOf('javascript:alert(1)')).toBeNull();
  expect(hostOf('not an address at all')).toBeNull();
});

test('nothing of what was typed is echoed into the address that gets framed', () => {
  // The identifier is read out and a new address is built from it. An address assembled by keeping
  // the query string would carry whatever else was in it.
  expect(embedSource('https://www.youtube.com/watch?v=dQw4w9WgXcQ&list=PL&autoplay=1')).toBe(
    'https://www.youtube-nocookie.com/embed/dQw4w9WgXcQ',
  );
  expect(embedSource('https://www.youtube.com/watch?v=../../elsewhere')).toBeNull();
  expect(embedSource('https://vimeo.com/76979871/../..')).toBeNull();
});

test('an allowed host with nothing recognisable in the address is refused, not guessed', () => {
  expect(embedSource('https://www.youtube.com/')).toBeNull();
  expect(embedSource('https://vimeo.com/')).toBeNull();
});
