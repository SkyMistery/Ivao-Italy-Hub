import { lazy, Suspense } from 'react';

import type { RouteMapProps } from './RouteMap';

/**
 * `RouteMap` as every screen gets it: the map itself, MapLibre and its stylesheet, fetched only when a map is drawn
 * (issue #400).
 *
 * ⚠️ **Why not the barrel's plain export.** `RouteMap.tsx` imports MapLibre's CSS at the top, and a module with a side
 * effect is kept by Rollup wherever it is imported, used or not: through `shared/ui/index.ts`, which the public layout
 * reads for `LiveStatusStrip`, MapLibre was in the entry chunk of every page — about half a megabyte for the two
 * screens that draw a map. Behind `lazy` the map, `mapProtocol`, `pmtiles` and the CSS are a chunk of their own, from
 * this origin like the rest (`script-src 'self'` unchanged). The barrel re-exports the map's types with `export type`
 * only: with `verbatimModuleSyntax` an `export { type … } from './RouteMap'` still imports the module, and measured on
 * 14 November 2026 it put MapLibre's stylesheet and its worker's URL back into the entry (81 KB of CSS).
 *
 * Until the chunk is there the page keeps the map's box — same size, nothing in it — so the list under it does not
 * jump when the map arrives.
 */
const DrawnMap = lazy(() => import('./RouteMap').then((module) => ({ default: module.RouteMap })));

/** The box of a map whose page names no other: the height a tour page wants. */
export const ROUTE_MAP_BOX = 'h-96 w-full overflow-hidden rounded-lg';

export function RouteMap(props: RouteMapProps) {
  return (
    <Suspense fallback={<div className={props.className ?? ROUTE_MAP_BOX} aria-busy="true" />}>
      <DrawnMap {...props} />
    </Suspense>
  );
}
