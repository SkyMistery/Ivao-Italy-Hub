/**
 * The shortest way between two airports, as a line a map can draw (design M2 §8.6).
 *
 * An aeroplane flies a great circle, which on the flat map of a screen is a curve — Rome to New York bends over
 * Ireland, not through the middle of the Atlantic — so a straight segment between two markers would say something
 * false about every flight longer than a few hundred miles. Twenty lines of spherical interpolation say it right, and
 * that is the reason the map carries no geometry library (note 2026-09-15-la-mappa).
 *
 * The distances themselves are the server's (`GreatCircle` in the module): this is only how a line is drawn.
 */

/** A point of the world as the hub holds one: degrees, latitude first, as every airport row has it. */
export interface GeoPoint {
  readonly latitude: number;
  readonly longitude: number;
}

/** How many segments a line is made of. Sixty-four is smooth at every zoom the base map has and costs nothing. */
export const GREAT_CIRCLE_SEGMENTS = 64;

const RADIANS = Math.PI / 180;
const DEGREES = 180 / Math.PI;

/**
 * The great circle from one point to the other, as GeoJSON wants it: `[longitude, latitude]`, in that order.
 *
 * ⚠️ The longitudes are **not** wrapped back into ±180. A flight from Tokyo to Los Angeles crosses the antimeridian,
 * and a line whose points jump from 179 to −179 is drawn by every renderer as a line straight back across the whole
 * world. So each point is moved by whole turns until it is within half a turn of the one before it, which is what
 * makes the line continue past the seam instead of leaping back over it.
 */
export function greatCirclePath(
  from: GeoPoint,
  to: GeoPoint,
  segments: number = GREAT_CIRCLE_SEGMENTS,
): [number, number][] {
  const steps = Math.max(1, Math.floor(segments));
  const lat1 = from.latitude * RADIANS;
  const lon1 = from.longitude * RADIANS;
  const lat2 = to.latitude * RADIANS;
  const lon2 = to.longitude * RADIANS;

  // The angle between the two points seen from the centre of the earth: the whole interpolation is a rotation by
  // fractions of it. Zero — the same airport twice — and π — two antipodes, where there is no shortest way but
  // infinitely many — both leave nothing to interpolate, so the line is the two ends.
  const angle = Math.acos(
    Math.min(
      1,
      Math.max(-1, Math.sin(lat1) * Math.sin(lat2) + Math.cos(lat1) * Math.cos(lat2) * Math.cos(lon2 - lon1)),
    ),
  );

  if (!Number.isFinite(angle) || angle === 0 || Math.sin(angle) === 0) {
    return unwrapped([
      [from.longitude, from.latitude],
      [to.longitude, to.latitude],
    ]);
  }

  const points: [number, number][] = [];

  for (let step = 0; step <= steps; step++) {
    const fraction = step / steps;
    const before = Math.sin((1 - fraction) * angle) / Math.sin(angle);
    const after = Math.sin(fraction * angle) / Math.sin(angle);

    const x = before * Math.cos(lat1) * Math.cos(lon1) + after * Math.cos(lat2) * Math.cos(lon2);
    const y = before * Math.cos(lat1) * Math.sin(lon1) + after * Math.cos(lat2) * Math.sin(lon2);
    const z = before * Math.sin(lat1) + after * Math.sin(lat2);

    points.push([Math.atan2(y, x) * DEGREES, Math.atan2(z, Math.sqrt(x * x + y * y)) * DEGREES]);
  }

  return unwrapped(points);
}

/**
 * A path somebody actually flew — the points of a track, in the order they were recorded — as GeoJSON wants it, and
 * unwrapped across the antimeridian like a great circle (T13b): a track over the Pacific is the same problem.
 */
export function trackPath(points: readonly GeoPoint[]): [number, number][] {
  return unwrapped(
    points
      .filter((point) => Number.isFinite(point.latitude) && Number.isFinite(point.longitude))
      .map((point): [number, number] => [point.longitude, point.latitude]),
  );
}

/**
 * Every point within half a turn of the one before it, so that a line crossing the antimeridian keeps going.
 *
 * ⚠️ Each point is compared with the point **already moved**, not with what it was in the input. Comparing with the
 * input leaves the second half of a Pacific crossing back where it started and the line jumps the whole world at the
 * seam — which is exactly what the test of the antimeridian found the first time this was written.
 */
function unwrapped(points: [number, number][]): [number, number][] {
  const moved: [number, number][] = [];

  for (const [longitude, latitude] of points) {
    const previous = moved.at(-1)?.[0];
    moved.push(
      previous === undefined
        ? [longitude, latitude]
        : [longitude + Math.round((previous - longitude) / 360) * 360, latitude],
    );
  }

  return moved;
}

/**
 * The box that holds every point, ready for `fitBounds`: `[west, south, east, north]`. Null when there is nothing to
 * hold — a tour with no legs yet — and the map keeps the whole world instead of zooming into the Gulf of Guinea.
 */
export function boundsOf(
  points: readonly (readonly [number, number])[],
): [number, number, number, number] | null {
  if (points.length === 0) {
    return null;
  }

  let west = points[0]![0];
  let east = points[0]![0];
  let south = points[0]![1];
  let north = points[0]![1];

  for (const [longitude, latitude] of points) {
    west = Math.min(west, longitude);
    east = Math.max(east, longitude);
    south = Math.min(south, latitude);
    north = Math.max(north, latitude);
  }

  return [west, south, east, north];
}
