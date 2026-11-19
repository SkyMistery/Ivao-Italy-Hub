import { expect, test } from 'vitest';

import security from '../../../config/security.json';

import { embedSource } from './allowlist';
import cases from './embedHosts.cases.json';
import list from './embedHosts.json';

/**
 * `embedHosts.json` is read twice: here, to build the frame, and by the server, to refuse publishing an address that
 * would not be framed (#524, note 2026-11-18-l-indirizzo-che-l-hub-non-incorpora). The rules that read it are written
 * once on each side, so `embedHosts.cases.json` is the table both are held to: this file checks the browser's answer,
 * `EmbedAllowlistTests` the server's, and a case added there is asked of both. `allowlist.test.ts` stays what it was,
 * the guard of the unhappy addresses.
 */

test.each(cases)('$address is framed as $framed', ({ address, framed }) => {
  expect(embedSource(address)).toBe(framed);
});

test('the table asks both answers: some addresses framed and some refused, for every site of the list', () => {
  const framed = cases.flatMap((entry) => (entry.framed === null ? [] : [new URL(entry.framed).origin]));
  const origins = list.sites.flatMap((site) => site.addresses.map((shape) => origin(shape.frame)));

  expect(cases.some((entry) => entry.framed === null)).toBe(true);
  expect([...new Set(origins)].filter((wanted) => !framed.includes(wanted))).toEqual([]);
});

/** The origin a frame address is built on, its names filled with anything. */
function origin(frame: string): string {
  return new URL(frame.replace(/\{\w+\}/g, 'x')).origin;
}

test('every address the list can build is allowed by frame-src, not only the ones a sample reaches', () => {
  const frameSrc: readonly string[] = security.contentSecurityPolicy.directives['frame-src'] ?? [];

  for (const site of list.sites) {
    for (const shape of site.addresses) {
      expect(frameSrc, `frame-src does not allow ${shape.frame}`).toContain(origin(shape.frame));
    }
  }
});

test('a frame names only what its shape reads, and the page that frames it', () => {
  for (const site of list.sites) {
    for (const shape of site.addresses) {
      const read = [...(shape.path ?? []), ...Object.values(shape.query ?? {})]
        .map((part) => /^\{(\w+)\}$/.exec(part)?.[1])
        .filter((name) => name !== undefined);
      const named = [...shape.frame.matchAll(/\{(\w+)\}/g)].map((match) => match[1]!);

      expect(
        named.filter((name) => name !== 'parent' && !read.includes(name)),
        `${site.key}: ${shape.frame}`,
      ).toEqual([]);
    }
  }
});

test('a frame is built on https and on a host of its own site, never on what was typed', () => {
  for (const site of list.sites) {
    for (const shape of site.addresses) {
      const url = new URL(shape.frame.replace(/\{\w+\}/g, 'x'));

      expect(url.protocol).toBe('https:');
      expect(site.hosts.some((host) => url.hostname === host || url.hostname.endsWith(`.${host}`))).toBe(
        true,
      );
    }
  }
});
