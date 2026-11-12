import { addProtocol, removeProtocol } from 'maplibre-gl';
import { Protocol } from 'pmtiles';

/**
 * The `pmtiles` protocol `RouteMap` reads its base map through, held by the maps that are drawn.
 *
 * MapLibre keeps one table of protocols for the whole page. Each map used to add its own and take it away when it went,
 * so a map that went broke every other map still drawn (issue #332, item 12): now the first map adds it and the last one
 * removes it.
 */
let holders = 0;

export function holdProtocol(): void {
  if (holders === 0) {
    addProtocol('pmtiles', new Protocol().tile);
  }
  holders += 1;
}

export function releaseProtocol(): void {
  holders -= 1;
  if (holders === 0) {
    removeProtocol('pmtiles');
  }
}
