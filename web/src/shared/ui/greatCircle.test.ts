import { describe, expect, test } from 'vitest';

import { boundsOf, greatCirclePath } from './greatCircle';

/**
 * The one piece of geometry the hub owns (design M2 §8.6): the line an aeroplane flies, drawn on a map. The map itself
 * is not tested here — a canvas of WebGL2 under jsdom is a test of jsdom — but the numbers it is handed are.
 */

describe('the great circle between two airports', () => {
  test('starts where it departs and ends where it arrives', () => {
    const path = greatCirclePath(
      { latitude: 41.8003, longitude: 12.2389 },
      { latitude: 45.63, longitude: 8.7281 },
      8,
    );

    expect(path).toHaveLength(9);
    expect(path[0]![0]).toBeCloseTo(12.2389, 4);
    expect(path[0]![1]).toBeCloseTo(41.8003, 4);
    expect(path.at(-1)![0]).toBeCloseTo(8.7281, 4);
    expect(path.at(-1)![1]).toBeCloseTo(45.63, 4);
  });

  test('bends towards the pole, which is what a straight segment gets wrong', () => {
    // Paris to New York: the great circle passes well north of the middle of the two latitudes, which
    // is the whole reason this function exists.
    const path = greatCirclePath(
      { latitude: 49.0097, longitude: 2.5479 },
      { latitude: 40.6413, longitude: -73.7781 },
      64,
    );

    const middle = path[32]!;
    const flat = (49.0097 + 40.6413) / 2;

    expect(middle[1]).toBeGreaterThan(flat + 3);
  });

  test('crosses the antimeridian without leaping back across the world', () => {
    // Tokyo to Los Angeles. Every step stays small: a line whose longitudes jump from 179 to −179 is
    // drawn straight back over Europe, which is the fault this guards against.
    const path = greatCirclePath(
      { latitude: 35.7647, longitude: 140.3863 },
      { latitude: 33.9416, longitude: -118.4085 },
      64,
    );

    for (let step = 1; step < path.length; step++) {
      expect(Math.abs(path[step]![0] - path[step - 1]![0])).toBeLessThan(180);
    }

    // It keeps going east past the seam rather than coming back: the last point is written beyond 180.
    expect(path.at(-1)![0]).toBeGreaterThan(180);
    expect(path.at(-1)![0]).toBeCloseTo(360 - 118.4085, 4);
  });

  test('the same airport twice is a line of two points and no arithmetic', () => {
    const path = greatCirclePath(
      { latitude: 41.8003, longitude: 12.2389 },
      { latitude: 41.8003, longitude: 12.2389 },
    );

    expect(path).toHaveLength(2);
    expect(path[0]).toEqual([12.2389, 41.8003]);
  });
});

describe('the box around the lines', () => {
  test('holds every point', () => {
    expect(
      boundsOf([
        [12, 41],
        [8, 45],
        [-73, 40],
      ]),
    ).toEqual([-73, 40, 12, 45]);
  });

  test('is nothing at all when there are no legs, so the map keeps the world', () => {
    expect(boundsOf([])).toBeNull();
  });
});
