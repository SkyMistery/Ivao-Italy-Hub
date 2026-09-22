import {
  addProtocol,
  AttributionControl,
  MapLibreMap,
  Marker,
  NavigationControl,
  removeProtocol,
  setWorkerUrl,
  type GeoJSONSource,
  type StyleSpecification,
} from 'maplibre-gl';
import { Protocol } from 'pmtiles';
import { useEffect, useMemo, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

import 'maplibre-gl/dist/maplibre-gl.css';
// ⚠️ `?worker&url` and not `?url`: MapLibre 6 has no "CSP build" any more, the worker is a file of
// its own, and with `?url` alone Vite hands back a module the browser refuses to start as a worker —
// silently, with a map that never draws (MapLibre issue 8168, measured in the note of 15 September).
import workerUrl from 'maplibre-gl/dist/maplibre-gl-worker.mjs?worker&url';

import { boundsOf, greatCirclePath, type GeoPoint } from './greatCircle';

/**
 * The legs of a tour on a map (design M2 §8.6, note 2026-09-15-la-mappa).
 *
 * **No tile provider.** The base map is one PMTiles archive of the world this hub serves itself, at
 * `/tiles/basemap.pmtiles` — no key, no quota, no third party watching who reads a tour page, and
 * `connect-src 'self'` unchanged. An installation that has not uploaded it yet gets the legs on a
 * neutral ground: a map without countries, never an error and never an empty box.
 *
 * **No place names.** The archive carries them, and drawing them would mean hosting the fonts of
 * every script the world writes in; a tour map is about airports, so the codes are the only words on
 * it and they are HTML markers (Carmine, 22 September 2026). That is one file for a fork to upload
 * rather than a directory of glyphs.
 *
 * **WebGL2 or nothing.** MapLibre throws without it, so the component says so in a line and the page
 * keeps the list of legs underneath, which is where the same information is in words.
 */

/** What a leg is to a map: two airports, and what it is to whoever is looking. */
export interface RouteMapLeg {
  readonly id: number | string;
  readonly from: RouteMapAirport;
  readonly to: RouteMapAirport;
  /** How it is drawn. The states of a pilot arrive with the reports (T11); a tour page draws `todo`. */
  readonly status?: RouteMapStatus;
}

export interface RouteMapAirport extends GeoPoint {
  /** The code written next to the marker; the ICAO, or the IATA where a page prefers it. */
  readonly code: string;
}

/**
 * The four states a leg is drawn in (design M2 §8.1): still to fly, flown and accepted, sent and
 * waiting for a validator, and not released yet. Closed, and each is a colour of the brand.
 */
export type RouteMapStatus = 'todo' | 'done' | 'pending' | 'locked';

const STATUS_TOKENS: Record<RouteMapStatus, { token: string; fallback: string }> = {
  todo: { token: '--ivao-color-atmos-500', fallback: '#1342e4' },
  done: { token: '--ivao-color-semantic-green-600', fallback: '#23984b' },
  pending: { token: '--ivao-color-semantic-yellow-600', fallback: '#c99a06' },
  locked: { token: '--ivao-color-fuselage-400', fallback: '#8b8ca9' },
};

/** The ground under the legs, so that a map without the archive is still a map and not a white box. */
const GROUND = { token: '--ivao-color-fuselage-100', fallback: '#eeeff5' };
const EARTH = { token: '--ivao-color-fuselage-150', fallback: '#e0e1ec' };
const WATER = { token: '--ivao-color-ocean-100', fallback: '#c8d0ec' };
const BORDER = { token: '--ivao-color-fuselage-300', fallback: '#a7a8bb' };

/** Where the archive lives. A path of the site, listed in `web/backendPaths.ts` like `/media` and `/embed`. */
export const BASE_MAP_URL = '/tiles/basemap.pmtiles';

/** What the archive is made of, and what the law asks be written on the map (ODbL). */
const ATTRIBUTION =
  '<a href="https://protomaps.com" target="_blank" rel="noreferrer">Protomaps</a> © <a href="https://www.openstreetmap.org/copyright" target="_blank" rel="noreferrer">OpenStreetMap</a>';

export interface RouteMapProps {
  readonly legs: readonly RouteMapLeg[];
  /** The height of the box. A map with no height is a map nobody sees; the default suits a tour page. */
  readonly className?: string;
  /** What a screen reader is told the map is of. */
  readonly label?: string;
}

export function RouteMap({ legs, className, label }: RouteMapProps) {
  const { t } = useTranslation();
  const container = useRef<HTMLDivElement | null>(null);
  // Asked once, while rendering rather than in the effect: whether this browser can draw a map is a
  // fact of the browser, not something that changes under us.
  const [supported] = useState(hasWebGl2);

  // The lines, computed once per set of legs: a great circle is what an aeroplane flies (`greatCircle`).
  const routes = useMemo(
    () =>
      legs
        .filter((leg) => located(leg.from) && located(leg.to))
        .map((leg) => ({
          leg,
          path: greatCirclePath(leg.from, leg.to),
        })),
    [legs],
  );

  // ⚠️ The map is built **once** and then fed. A page hands `legs` as a fresh array on every render —
  // a refetch, a language change, anything — so an effect that took the legs as a dependency would
  // tear the map down and build it again each time: a flash, a second download of the archive's
  // header, and the reader's own zoom thrown away.
  const map = useRef<MapLibreMap | null>(null);
  const [ready, setReady] = useState(false);
  const markers = useRef<Marker[]>([]);

  useEffect(() => {
    const element = container.current;
    if (element === null || !supported) {
      return;
    }

    // One protocol registration per map, and the worker served from this origin: `worker-src` is
    // not declared, so `script-src 'self'` is what allows it, and both are ours already.
    setWorkerUrl(workerUrl);
    const protocol = new Protocol();
    addProtocol('pmtiles', protocol.tile);

    const drawn = new MapLibreMap({
      container: element,
      style: styleOf(),
      attributionControl: false,
      // A tour page is read, not explored: the map answers the wheel and the drag, and asks for no
      // rotation a visitor would have to undo.
      dragRotate: false,
      pitchWithRotate: false,
      touchZoomRotate: false,
      center: [0, 20],
      zoom: 1,
    });

    drawn.addControl(new AttributionControl({ compact: true, customAttribution: ATTRIBUTION }));
    drawn.addControl(new NavigationControl({ showCompass: false }), 'top-right');

    // A base map that cannot be read is not an error a visitor has to see: the legs are the point,
    // and the ground under them is a colour. Said to whoever is developing, and to nobody else.
    drawn.on('error', (event) => {
      console.debug('RouteMap', event.error?.message ?? event.error);
    });

    drawn.on('load', () => {
      drawn.addSource('legs', { type: 'geojson', data: empty() });
      drawn.addLayer({
        id: 'legs-line',
        type: 'line',
        source: 'legs',
        layout: { 'line-cap': 'round', 'line-join': 'round' },
        paint: {
          'line-width': 2.5,
          'line-color': ['get', 'colour'],
        },
      });

      map.current = drawn;
      setReady(true);
    });

    return () => {
      map.current = null;
      setReady(false);
      markers.current = [];
      drawn.remove();
      removeProtocol('pmtiles');
    };
  }, [supported]);

  // The legs themselves, put on the map that exists: the line source is replaced, the markers are
  // drawn again — there is no "move a marker", they are HTML — and the view is fitted to them.
  useEffect(() => {
    const drawn = map.current;
    if (drawn === null || !ready) {
      return;
    }

    // ⚠️ `setData` answers with a promise — it is a message to the worker that parses the geometry —
    // and nothing here waits for it: the lines appear when they appear, and a map torn down first
    // simply never draws them.
    void drawn.getSource<GeoJSONSource>('legs')?.setData(collectionOf(routes));

    for (const marker of markers.current) {
      marker.remove();
    }

    markers.current = airportsOf(routes).map((airport) =>
      new Marker({ element: markerOf(airport.code) })
        .setLngLat([airport.longitude, airport.latitude])
        .addTo(drawn),
    );

    const bounds = boundsOf(routes.flatMap((route) => route.path));
    if (bounds !== null) {
      drawn.fitBounds(bounds, { padding: 56, maxZoom: 7, duration: 0 });
    }
  }, [ready, routes]);

  if (!supported) {
    return (
      <p className="border-border text-muted-foreground rounded-lg border p-4 text-sm" role="status">
        {t('map.noWebgl')}
      </p>
    );
  }

  return (
    <div
      ref={container}
      // A map is a picture of the list below it: a screen reader is told what it shows and then reads
      // the legs, which carry the same in words.
      role="img"
      aria-label={label ?? t('map.label')}
      data-testid="route-map"
      className={className ?? 'h-96 w-full overflow-hidden rounded-lg'}
    />
  );
}

/** Whether the browser can draw a map at all. MapLibre throws on the constructor without WebGL2. */
function hasWebGl2(): boolean {
  try {
    return document.createElement('canvas').getContext('webgl2') !== null;
  } catch {
    return false;
  }
}

/** A colour of the brand as the browser computed it, so the map follows the theme like everything else. */
function colour({ token, fallback }: { token: string; fallback: string }): string {
  if (typeof window === 'undefined') {
    return fallback;
  }

  const written = window.getComputedStyle(document.documentElement).getPropertyValue(token).trim();
  return written === '' ? fallback : written;
}

/**
 * The style of the base map, written here rather than fetched: four layers of the archive — land,
 * land cover, water and the borders between countries — and no labels at all.
 */
function styleOf(): StyleSpecification {
  return {
    version: 8,
    sources: {
      basemap: {
        type: 'vector',
        url: `pmtiles://${BASE_MAP_URL}`,
        attribution: ATTRIBUTION,
      },
    },
    layers: [
      { id: 'ground', type: 'background', paint: { 'background-color': colour(GROUND) } },
      {
        id: 'earth',
        type: 'fill',
        source: 'basemap',
        'source-layer': 'earth',
        paint: { 'fill-color': colour(EARTH) },
      },
      {
        id: 'water',
        type: 'fill',
        source: 'basemap',
        'source-layer': 'water',
        paint: { 'fill-color': colour(WATER) },
      },
      {
        id: 'boundaries',
        type: 'line',
        source: 'basemap',
        'source-layer': 'boundaries',
        filter: ['<=', ['get', 'kind_detail'], 2],
        paint: { 'line-color': colour(BORDER), 'line-width': 0.8 },
      },
    ],
  };
}

/** Nothing to draw yet: the source the map is built with, before the legs arrive. */
function empty() {
  return { type: 'FeatureCollection' as const, features: [] };
}

function collectionOf(routes: readonly { leg: RouteMapLeg; path: [number, number][] }[]) {
  return {
    type: 'FeatureCollection' as const,
    features: routes.map((route) => ({
      type: 'Feature' as const,
      id: String(route.leg.id),
      properties: { colour: colour(STATUS_TOKENS[route.leg.status ?? 'todo']) },
      geometry: { type: 'LineString' as const, coordinates: route.path },
    })),
  };
}

/** Every airport of the legs, once: two legs out of the same hub put one marker there, not two. */
function airportsOf(routes: readonly { leg: RouteMapLeg }[]): RouteMapAirport[] {
  const seen = new Map<string, RouteMapAirport>();

  for (const { leg } of routes) {
    for (const airport of [leg.from, leg.to]) {
      if (!seen.has(airport.code)) {
        seen.set(airport.code, airport);
      }
    }
  }

  return [...seen.values()];
}

/** The marker: a dot and the code beside it, as HTML — which is how a map with no fonts still has words. */
function markerOf(code: string): HTMLElement {
  const element = document.createElement('div');
  element.className = 'flex items-center gap-1';

  const dot = document.createElement('span');
  dot.className = 'border-background size-2.5 rounded-full border';
  dot.style.backgroundColor = colour(STATUS_TOKENS.todo);

  const name = document.createElement('span');
  name.className =
    'bg-background/85 text-foreground rounded px-1 py-0.5 text-[11px] leading-none font-semibold';
  name.textContent = code;

  element.append(dot, name);
  return element;
}

/** An airport the hub knows the position of. One at 0/0 is one the reference data has not got. */
function located(airport: GeoPoint): boolean {
  return Number.isFinite(airport.latitude) && Number.isFinite(airport.longitude);
}
