import { H2 } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useRouteContext } from '@tanstack/react-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { DataList, col, listSearchSchema, type ColumnSpec, type ListSearch } from '../../../shared/list';
import { Notice } from '../../../shared/ui';
import { networkBookingsQuery, type NetworkBookingRow } from '../api';

/**
 * The bookings of the network beside the roster (design M4 §4.4, §9.1, E15b; note 2026-09-29-il-roster-atc §2.9): who booked a position of
 * the event on the network, when and what for, and whether they hold that shift on the roster — so the staff see who has also booked, and
 * which position somebody else has taken, for a training or an exam too. Read fresh at every opening, in a query of its own: while the
 * network is slow this section waits and the roster above does not; when it does not answer, the section says so, never «nobody booked».
 */

const columns: readonly ColumnSpec<NetworkBookingRow>[] = [
  col.text('callsign'),
  col.date('fromUtc'),
  col.date('toUtc'),
  col.person('booker'),
  col.badge('kind', 'events:atc.network'),
  col.boolean('onRoster'),
];

export function NetworkBookingsSection({ eventId }: { eventId: number }) {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const [search, setSearch] = useState<ListSearch>(() => listSearchSchema.parse({ pageSize: 100 }));
  const query = networkBookingsQuery(eventId, search);
  // The same query the list reads, from the same cache: one question to the server.
  const answer = useQuery(query);

  return (
    <section className="flex flex-col gap-4" aria-label={t('events:atc.network.title')}>
      <H2>{t('events:atc.network.title')}</H2>
      <p className="text-muted-foreground text-sm">{t('events:atc.network.intro')}</p>
      {/* «Not available» from the server, or no answer from it at all: never drawn as an empty list. */}
      {answer.isError ? (
        <Notice tone="info" title={t('events:atc.network.unavailable')} />
      ) : (
        <DataList
          columns={columns}
          query={query}
          labels="events:atc.network"
          locale={i18n.language}
          defaultLocale={bootstrap.division.defaultLocale}
          timezone={bootstrap.division.timezone}
          search={search}
          onSearchChange={(patch) => setSearch((current) => ({ ...current, ...patch }))}
          emptyDescription={t('events:atc.network.empty')}
        />
      )}
    </section>
  );
}
