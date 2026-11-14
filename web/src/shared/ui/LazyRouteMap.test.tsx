import { readdirSync, readFileSync } from 'node:fs';
import { relative, resolve, sep } from 'node:path';

import { screen } from '@testing-library/react';
import { describe, expect, test, vi } from 'vitest';

import { renderWithProviders } from '../../test/harness';

import { RouteMap } from './index';
import type { RouteMapLeg } from './RouteMap';

/**
 * The map behind `lazy` (issue #400): MapLibre and its stylesheet are a chunk of their own, fetched by the screens that
 * draw a map and by no other page.
 *
 * ⚠️ Mutation tried on 14 November 2026: with `export { RouteMap, … } from './RouteMap'` back in `index.ts` the build puts
 * MapLibre into the entry chunk again (2,114 KB instead of 1,077 KB) and both tests fail, the second naming
 * `shared/ui/index.ts`; with the types re-exported as `export { type … }` the entry gets MapLibre's CSS back and the
 * second test fails the same way.
 */

// The map itself is not what is tested here (`RouteMap.test.tsx` is): a stand-in that says what it was handed.
vi.mock('./RouteMap', () => ({
  RouteMap: ({ legs, className }: { legs: readonly RouteMapLeg[]; className?: string }) => (
    <div data-testid="route-map" className={className}>
      {legs.map((leg) => `${leg.from.code}-${leg.to.code}`).join(' ')}
    </div>
  ),
}));

const legs: RouteMapLeg[] = [
  {
    id: 1,
    from: { code: 'LIRF', latitude: 41.8, longitude: 12.25 },
    to: { code: 'LIMC', latitude: 45.63, longitude: 8.72 },
  },
];

describe('the lazy route map', () => {
  test('keeps the box of the map until the map has come, then draws it with what the page handed', async () => {
    const { container } = renderWithProviders(<RouteMap legs={legs} className="h-72" />);

    expect(container.querySelector('[aria-busy="true"]')).toHaveClass('h-72');

    const map = await screen.findByTestId('route-map');
    expect(map).toHaveTextContent('LIRF-LIMC');
    expect(map).toHaveClass('h-72');
    expect(container.querySelector('[aria-busy="true"]')).toBeNull();
  });

  test('is the only door to MapLibre: no other file imports it, or the map, without `type`', () => {
    // The two files that may name MapLibre, and are reached only through the dynamic import.
    const drawing = new Set(['shared/ui/RouteMap.tsx', 'shared/ui/mapProtocol.ts']);
    const root = resolve(process.cwd(), 'src');
    const offenders: string[] = [];

    for (const entry of readdirSync(root, { recursive: true, withFileTypes: true })) {
      if (!entry.isFile() || !/\.tsx?$/.test(entry.name) || /\.test\.tsx?$/.test(entry.name)) {
        continue;
      }

      const path = resolve(entry.parentPath, entry.name);
      const file = relative(root, path).split(sep).join('/');
      if (drawing.has(file)) {
        continue;
      }

      const source = readFileSync(path, 'utf8');
      // Every static import and re-export that is not `import type` / `export type`: those are erased, every other one
      // loads the module, an inline `{ type … }` included under `verbatimModuleSyntax`.
      const loaded = [
        ...source.matchAll(/(?:^|\n)\s*(?:import|export)(?!\s+type\b)[^;'"]*?\bfrom\s+'([^']+)'/g),
        ...source.matchAll(/(?:^|\n)\s*import\s+'([^']+)'/g),
      ].map((match) => match[1]!);

      if (
        loaded.some((specifier) => /^maplibre-gl|^pmtiles$|(?:^|\/)(?:RouteMap|mapProtocol)$/.test(specifier))
      ) {
        offenders.push(file);
      }
    }

    expect(offenders).toEqual([]);
  });
});
