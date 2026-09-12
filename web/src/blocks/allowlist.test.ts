import { expect, test } from 'vitest';

import security from '../../../config/security.json';

import { EMBED_HOSTS, embedSource, hostOf } from './allowlist';

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

/**
 * The half of the allow list that lives outside this file: `frame-src` in `config/security.json`.
 *
 * ⚠️ A host added here and not there is a block that says "allowed" and a frame the browser refuses
 * — and it fails **silently**, because a refused frame is an empty box and no assertion anywhere
 * mentions it. So the two are checked against each other, the way the backgrounds of a section are
 * checked against the list the server keeps: one sample address per host, and a host with no sample
 * fails rather than being skipped.
 */
const SAMPLES: Record<string, string> = {
  youtube: 'https://www.youtube.com/watch?v=dQw4w9WgXcQ',
  vimeo: 'https://vimeo.com/76979871',
  twitch: 'https://www.twitch.tv/videos/123456789',
};

test('every host the allow list can frame is allowed by the content security policy', () => {
  const frameSrc: readonly string[] = security.contentSecurityPolicy.directives['frame-src'] ?? [];

  for (const host of EMBED_HOSTS) {
    const sample = SAMPLES[host.key];
    expect(sample, `no sample address for the "${host.key}" host`).toBeTruthy();

    const framed = embedSource(sample!);
    expect(framed, `the sample for "${host.key}" is not framed at all`).toBeTruthy();

    expect(frameSrc, `frame-src does not allow ${host.key}`).toContain(new URL(framed!).origin);
  }
});
