import type { Suggestion } from '../../shared/forms';

/**
 * The positions an operational document may be about, proposed from the airport and the FIR it
 * names (implementation plan M1, G14): choose `LIRF` and the tower, the ground and the approach of
 * `LIRF` are offered; choose `LIRR` and its centre is. Offered, never imposed — a division has
 * positions no list knows of, `LIRR_N_CTR` among them, and the field stays free text held to the
 * shape of a callsign by the server (`ContentWriteDtoValidator.PositionPattern`).
 *
 * The suffixes are the network's own vocabulary of a callsign and nothing of one division: a
 * fork proposes the same ones for its own airports.
 */
const AIRPORT_SUFFIXES = ['DEL', 'GND', 'TWR', 'APP', 'DEP'] as const;
const CENTER_SUFFIXES = ['CTR', 'FSS'] as const;

export function suggestedPositions(icao: string | undefined, fir: string | undefined): Suggestion[] {
  const positions: Suggestion[] = [];

  const airport = icao?.trim().toUpperCase() ?? '';
  if (airport !== '') {
    for (const suffix of AIRPORT_SUFFIXES) {
      const value = `${airport}_${suffix}`;
      positions.push({ value, label: value, group: airport });
    }
  }

  const center = fir?.trim().toUpperCase() ?? '';
  if (center !== '') {
    for (const suffix of CENTER_SUFFIXES) {
      const value = `${center}_${suffix}`;
      positions.push({ value, label: value, group: center });
    }
  }

  return positions;
}
