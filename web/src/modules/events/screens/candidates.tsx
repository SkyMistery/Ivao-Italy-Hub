import { H2 } from '@ivao/atmosphere-react';
import { useRouteContext } from '@tanstack/react-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { DataList, col, listSearchSchema, type ColumnSpec, type ListSearch } from '../../../shared/list';
import { atcCandidatesQuery, type AtcCandidateDto } from '../api';

const candidateColumns: readonly ColumnSpec<AtcCandidateDto>[] = [
  col.person('controller'),
  col.text('rating'),
  col.boolean('wholePosition'),
  col.list('windows'),
  col.text('note'),
  col.date('createdAt'),
];

/**
 * Who is available on a position (§4.2, §6.3, E11a): the controllers with a window that meets its hours, the first given first — under a
 * position's form, and under a shift's (E11b), where the staff choose who to put on it.
 */
export function Candidates({ positionId }: { positionId: number }) {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const [search, setSearch] = useState<ListSearch>(() => listSearchSchema.parse({ pageSize: 50 }));

  return (
    <section className="flex flex-col gap-4" aria-label={t('events:atc.candidates.title')}>
      <H2>{t('events:atc.candidates.title')}</H2>
      <DataList
        columns={candidateColumns}
        query={atcCandidatesQuery(positionId, search)}
        labels="events:atc.candidates"
        locale={i18n.language}
        defaultLocale={bootstrap.division.defaultLocale}
        timezone={bootstrap.division.timezone}
        search={search}
        onSearchChange={(patch) => setSearch((current) => ({ ...current, ...patch }))}
        emptyDescription={t('events:atc.candidates.empty')}
      />
    </section>
  );
}
