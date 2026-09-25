import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useEffect, useMemo, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { tourLegsQuery, type LegDto, type TourDetailDto, type TourLegsDto } from '../api';

import { LegGrid } from './LegGrid';

/**
 * `LegGrid` in the gallery of the back office (T20c, note 2026-09-25-le-rifiniture-di-m2): three legs of a tour that does
 * not exist, one of them retired, in a query client of its own that already holds them — so the grid asks the server
 * nothing, and the gallery's own client never sees a tour. Read only: a gallery that saved a leg would write somewhere.
 */

const SAMPLE_TOUR_ID = -1;

const tour = { id: SAMPLE_TOUR_ID, kind: 'Sequential' } as unknown as TourDetailDto;

function leg(id: number, from: string, to: string, distanceNm: number, extra: Partial<LegDto> = {}): LegDto {
  return {
    id,
    tourId: SAMPLE_TOUR_ID,
    number: id,
    kind: 'Normal',
    rotationId: null,
    seqInRotation: null,
    departureIcao: from,
    departureIata: null,
    arrivalIcao: to,
    arrivalIata: null,
    distanceNm,
    estimatedMinutes: Math.round(distanceNm / 4 + 30),
    callsigns: [],
    flightNumbers: [],
    aircraft: { types: ['A320'], groupIds: [] },
    releaseAt: null,
    retiredAt: null,
    retiredReason: null,
    hasReports: false,
    updatedAt: '2026-09-25T10:00:00Z',
    rowVersion: `sample-${id}`,
    ...extra,
  };
}

/** The reason of the retired leg is what a member of the staff typed, so it comes in the language of the page. */
function sampleGrid(retiredReason: string): TourLegsDto {
  const legs = [
    leg(1, 'EGLL', 'LFPG', 188),
    leg(2, 'LFPG', 'EDDF', 243, { callsigns: ['ABC123'] }),
    leg(3, 'EDDF', 'LOWW', 335, { retiredAt: '2026-09-24T09:00:00Z', retiredReason, hasReports: true }),
  ];

  return { tourId: SAMPLE_TOUR_ID, legs, totalNm: 431, totalEstimatedMinutes: 168 };
}

export function LegGridSample() {
  const { t } = useTranslation();
  const reason = t('flightops:gallery.retiredReason');
  const grid = useMemo(() => sampleGrid(reason), [reason]);
  const [client] = useState(() => {
    const sample = new QueryClient({ defaultOptions: { queries: { staleTime: Infinity, retry: false } } });
    sample.setQueryData(tourLegsQuery(SAMPLE_TOUR_ID).queryKey, grid);
    return sample;
  });

  // The page's language changed: the reason follows it.
  useEffect(() => {
    client.setQueryData(tourLegsQuery(SAMPLE_TOUR_ID).queryKey, grid);
  }, [client, grid]);

  return (
    <QueryClientProvider client={client}>
      <LegGrid tour={tour} editable={false} />
    </QueryClientProvider>
  );
}
