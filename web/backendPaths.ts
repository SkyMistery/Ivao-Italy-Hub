/**
 * Every path the **backend** answers, and therefore everything the development server has to hand
 * over to it.
 *
 * Published, one server serves the application and these together and the question never arises. In
 * development they are two servers on two ports, and anything missing from this list is answered by
 * Vite with `index.html` — a 200, of the wrong thing.
 *
 * ⚠️ That is how every picture in the media library was broken and nobody noticed:
 * `/media/{id}/{name}` is the **address of an upload**, which outlives the row it belongs to, so it
 * is a path of the site and not a call under `/api` (`MediaUrl` in the backend). It was not on this
 * list, so in development the library, the editor's preview and every document card showed an
 * `<img>` pointing at a page of HTML. Nothing caught it: the unit tests stub the API, the smoke
 * suite runs the built bundle, and the full round runs the published package where one server
 * serves both. Found by Carmine running the demo.
 *
 * ⚠️ And it happened a second time, on 12 September 2026, in exactly the same shape: `/embed/…` is
 * the frame of an interactive block and `/embed/guidelines` the file an editor downloads — addresses
 * of the site, not calls under `/api`. Carmine downloaded the guidelines from the editor and got
 * `index.html`: nineteen lines of Vite's own client instead of the document. The same three suites
 * were blind to it for the same three reasons, and the same person found it the same way. A path
 * this list does not know is a 200 of the wrong thing.
 *
 * It lives in a file of its own because two things read it — `vite.config.ts` builds the proxy from
 * it, and `src/test/devProxy.test.ts` checks it — and a list written twice is a list that drifts.
 */
export const BACKEND_PATHS = ['/api', '/auth', '/health', '/media', '/embed', '/sitemap.xml', '/robots.txt'];
