import { expect, test, vi } from 'vitest';

const maplibre = vi.hoisted(() => ({ addProtocol: vi.fn(), removeProtocol: vi.fn() }));

vi.mock('maplibre-gl', () => maplibre);
vi.mock('pmtiles', () => ({
  Protocol: class {
    tile = () => undefined;
  },
}));

const { holdProtocol, releaseProtocol } = await import('./mapProtocol');

test('the base map protocol stays while a map is still drawn, and goes with the last one (issue #332, item 12)', () => {
  holdProtocol();
  holdProtocol();
  expect(maplibre.addProtocol).toHaveBeenCalledTimes(1);

  releaseProtocol();
  expect(maplibre.removeProtocol).not.toHaveBeenCalled();

  releaseProtocol();
  expect(maplibre.removeProtocol).toHaveBeenCalledExactlyOnceWith('pmtiles');

  // A map drawn again after all of them went registers it again.
  holdProtocol();
  expect(maplibre.addProtocol).toHaveBeenCalledTimes(2);
  releaseProtocol();
});
