import { readFile } from 'node:fs/promises';
import { resolve } from 'node:path';

import { expect, test } from 'vitest';

import { BACKEND_PATHS } from '../../backendPaths';

/**
 * The development server hands the backend's paths to the backend.
 *
 * ⚠️ A "do not delete this line" test, and it exists because the line that was missing cost a
 * fortnight of broken pictures: `/media/{id}/{name}` is the address of an upload, so it reads like a
 * page rather than like a call, and it was not proxied — every `<img>` in development pointed at
 * `index.html`.
 *
 * Nothing else could have caught it. The unit tests stub the API, the smoke suite runs the built
 * bundle under `vite preview`, and the full round runs the published package where **one** server
 * serves the application and the files together. Only the two-port arrangement of development has
 * this seam, and only a test of the configuration can look at it.
 */

test('the uploads are among the paths handed to the backend, and not only the calls', () => {
  // Named on its own because it is the one that is easy to forget: everything else the backend
  // answers begins with a word that reads like an API, and this one reads like a page.
  expect(BACKEND_PATHS).toContain('/media');

  // The two the server produces rather than serves. They are as easy to forget for the same reason.
  expect(BACKEND_PATHS).toContain('/sitemap.xml');
  expect(BACKEND_PATHS).toContain('/robots.txt');

  // ⚠️ And the one this cost twice: `/embed/…` is the frame of an interactive block, and
  // `/embed/guidelines` the file an editor downloads. Both read like pages, both are the backend's,
  // and in development both came back as `index.html` — the frame drawing the hub inside itself and
  // the guidelines arriving as nineteen lines of Vite. Found by Carmine, downloading them.
  expect(BACKEND_PATHS).toContain('/embed');
});

test('the proxy is built from that list and never written out beside it', async () => {
  // ⚠️ Read as text on purpose. Importing the configuration would load its plugins, and one of them
  // wants a real `TextEncoder` that jsdom does not provide — while what is worth asserting is that
  // nobody typed a second list next to the first one, which is exactly what the file says.
  // ⚠️ From the project root and not from `import.meta.url`: under jsdom the module's own URL is an
  // `http://` one, and reading a file from that is a scheme error.
  const config = await readFile(resolve(process.cwd(), 'vite.config.ts'), 'utf8');

  expect(config).toContain('proxy: Object.fromEntries(BACKEND_PATHS.map(');
  expect(config).not.toMatch(/proxy:\s*\{/);
});
