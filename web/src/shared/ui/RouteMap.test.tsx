import { act } from '@testing-library/react';
import { afterEach, beforeEach, describe, expect, test, vi } from 'vitest';

import { renderWithProviders } from '../../test/harness';

import { RouteMap, type RouteMapLeg } from './RouteMap';

/**
 * When the map draws its legs (note 2026-11-11-la-mappa-che-aspettava-la-mappa-di-base). MapLibre needs WebGL, which a test has
 * not got, so the map here is a stand-in that records what is done to it and fires the events the test chooses — the
 * ones the real map fires when the base map archive is missing: the style is there (`style.load`), the base map's
 * source fails, and `load`, which waits for every source, comes only at a repaint that a page at rest never makes.
 *
 * ⚠️ Mutation tried on 11 November 2026: with `drawn.on('load', …)` back in `RouteMap.tsx` both tests fail — no layer and
 * no marker in the first («expected [] to deeply equal [ 'legs-line', 'tracks-line' ]»), `load` subscribed in the second —
 * as the route tab of a tour did on #484.
 */

interface FakeMap {
  readonly fire: (name: string) => void;
  readonly layers: string[];
  readonly markers: string[];
  readonly subscribed: string[];
}

const created = vi.hoisted(() => [] as FakeMap[]);

vi.mock('maplibre-gl', () => {
  class MapLibreMap {
    readonly layers: string[] = [];
    readonly markers: string[] = [];
    readonly subscribed: string[] = [];
    private readonly handlers = new Map<string, ((event: unknown) => void)[]>();
    private readonly sources = new Map<string, { setData: () => Promise<void> }>();

    constructor() {
      created.push(this);
    }

    on(name: string, handler: (event: unknown) => void) {
      this.subscribed.push(name);
      this.handlers.set(name, [...(this.handlers.get(name) ?? []), handler]);
      return this;
    }

    once(name: string, handler: (event: unknown) => void) {
      return this.on(name, handler);
    }

    fire(name: string) {
      for (const handler of this.handlers.get(name) ?? []) {
        handler({});
      }
    }

    addControl() {
      return this;
    }

    addSource(id: string) {
      this.sources.set(id, { setData: () => Promise.resolve() });
    }

    addLayer(layer: { id: string }) {
      this.layers.push(layer.id);
    }

    getSource(id: string) {
      return this.sources.get(id);
    }

    fitBounds() {
      return this;
    }

    remove() {}
  }

  class Marker {
    private readonly code: string;

    constructor({ element }: { element: HTMLElement }) {
      this.code = element.textContent ?? '';
    }

    setLngLat() {
      return this;
    }

    addTo(map: { markers: string[] }) {
      map.markers.push(this.code);
      return this;
    }

    remove() {}
  }

  class Control {}

  return {
    MapLibreMap,
    Marker,
    AttributionControl: Control,
    NavigationControl: Control,
    addProtocol: vi.fn(),
    removeProtocol: vi.fn(),
    setWorkerUrl: vi.fn(),
  };
});

vi.mock('pmtiles', () => ({
  Protocol: class {
    tile = vi.fn();
  },
}));

vi.mock('maplibre-gl/dist/maplibre-gl-worker.mjs?worker&url', () => ({ default: '/maplibre-gl-worker.js' }));

const legs: RouteMapLeg[] = [
  {
    id: 1,
    from: { code: 'LIRF', latitude: 41.8, longitude: 12.25 },
    to: { code: 'LIMC', latitude: 45.63, longitude: 8.72 },
  },
];

beforeEach(() => {
  created.length = 0;
  // A browser that can draw a map: MapLibre itself is the stand-in above.
  vi.spyOn(HTMLCanvasElement.prototype, 'getContext').mockImplementation((kind: string) =>
    kind === 'webgl2' ? ({} as RenderingContext) : null,
  );
});

afterEach(() => {
  vi.restoreAllMocks();
});

describe('the legs of the route map', () => {
  test('are drawn once the style is there, without waiting for the base map, which may never load', () => {
    renderWithProviders(<RouteMap legs={legs} />);
    const map = created[0]!;

    // Only what the real map fires without an archive: the style. `load` never comes.
    act(() => {
      map.fire('style.load');
    });

    expect(map.layers).toEqual(['legs-line', 'tracks-line']);
    expect(map.markers).toEqual(['LIRF', 'LIMC']);
  });

  test('do not hang on `load`, the event that waits for every source', () => {
    renderWithProviders(<RouteMap legs={legs} />);

    expect(created[0]!.subscribed).not.toContain('load');
  });
});
