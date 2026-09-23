#!/usr/bin/env node
/**
 * The base map of the tours: what to run to make it, and where to put it.
 *
 * The maps of the hub (`RouteMap`) read one PMTiles archive of the world that this server serves itself, at
 * `/tiles/basemap.pmtiles` — no tile provider, no API key, no quota, and nobody outside this host sees who is
 * looking at a map. The archive is **not** in the repository and not in the release package: it is 179 MB of
 * coastlines that change with nothing we ship, it belongs to an installation, and a deployment must not overwrite
 * it (note `docs/internal/decisions/2026-09-15-la-mappa.md`).
 *
 * This script writes the two commands that make it. It runs nothing itself: extracting downloads gigabytes of
 * ranges from a public build, and that is a thing somebody decides to do, not a side effect of running a script.
 *
 *   node tools/basemap.mjs [YYYYMMDD]
 *
 * Without a date it uses today's. Protomaps keeps about a week of daily builds, so an old date answers 404 — and a
 * date is the only thing worth passing: the build of the world is dated, and the one you extracted is the one your
 * installation shows until you upload another.
 */

const MAX_ZOOM = 7;
const OUTPUT = 'tiles/basemap.pmtiles';

const [, , asked] = process.argv;
const date = asked ?? new Date().toISOString().slice(0, 10).replaceAll('-', '');

if (!/^\d{8}$/.test(date)) {
  console.error(`Not a build date: ${date}. Use YYYYMMDD, for example ${new Date().toISOString().slice(0, 10).replaceAll('-', '')}.`);
  process.exit(1);
}

console.log(`
The base map of the tours — the world to zoom ${MAX_ZOOM}, about 180 MB.

1. Get the extractor (one binary, no installation), from
   https://github.com/protomaps/go-pmtiles/releases — the archive for your operating system.

2. Extract the world of the build of ${date}:

   pmtiles extract https://build.protomaps.com/${date}.pmtiles ${OUTPUT} --maxzoom=${MAX_ZOOM}

   It reads the ranges it needs out of a 130 GB file over HTTP and writes about 180 MB here; it takes
   seconds on a fast line. A date more than a week old is gone from the build server: pass a recent one.

3. Put the file where the hub keeps the data of an installation, next to \`media/\`:

   development   ${OUTPUT} in the repository (git ignores it)
   production    tiles/basemap.pmtiles next to the application, over FTP

Zoom ${MAX_ZOOM} is the choice of 15 September 2026: zoom 6 is 43 MB, zoom 8 is 528 MB and past what
Cloudflare caches for free. Beyond the last level the map still zooms in — vector tiles stay sharp,
with less detail.

Without the file the maps draw their legs on a neutral ground: no error, no empty box, no countries.

The data is OpenStreetMap's, under the ODbL, and the maps say so on their attribution control.
`);
