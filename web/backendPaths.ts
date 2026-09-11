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
 * It lives in a file of its own because two things read it — `vite.config.ts` builds the proxy from
 * it, and `src/test/devProxy.test.ts` checks it — and a list written twice is a list that drifts.
 */
export const BACKEND_PATHS = ['/api', '/auth', '/health', '/media', '/sitemap.xml', '/robots.txt'];
