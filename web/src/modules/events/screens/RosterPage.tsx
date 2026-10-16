import { H1, Lead } from '@ivao/atmosphere-react';
import { queryOptions, useQuery } from '@tanstack/react-query';
import { useParams } from '@tanstack/react-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { bootstrapQuery } from '../../../features/me/queries';
import { loginHref } from '../../../shared/api/client';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import { DataList, col, listSearchSchema, type ColumnSpec, type ListSearch } from '../../../shared/list';
import { PageMetadata } from '../../../shared/seo/PageMetadata';
import { NotFound, Notice } from '../../../shared/ui';
import { publicRosterQuery, type PublicRosterDto, type PublicRosterRowDto } from '../api';

import { eventHref } from './cards';

/**
 * The page of a roster, `/events/{slug}/roster` (design M4 §4.4, §7.1, E12; note 2026-10-14-la-pubblicazione-del-roster-e-la-cessione,
 * reading 5): before its publication, when it will be; after it, a generated list by position and hour of the shifts and of the pieces
 * nobody holds. **The names only to a signed in reader**: the server sends no person to a visitor, and the page draws neither the
 * controller's column nor the rating's for one, with a line that asks to sign in.
 */

/** A row with its state as a word of a closed set, which the badge draws: held, or nobody's. */
type RosterPageRow = PublicRosterRowDto & { readonly state: 'Assigned' | 'Uncovered' };

const visitorColumns: readonly ColumnSpec<RosterPageRow>[] = [
  col.text('callsign'),
  col.text('name'),
  col.date('fromUtc'),
  col.date('toUtc'),
  col.badge('state', 'events:roster'),
];

const memberColumns: readonly ColumnSpec<RosterPageRow>[] = [
  ...visitorColumns,
  col.person('controller'),
  col.text('rating'),
];

/** The rows of a page of the list, out of the roster read once: the search box narrows them to a callsign, a position or a name. */
function rowsQuery(roster: PublicRosterDto, search: ListSearch) {
  return queryOptions({
    queryKey: ['events', 'public', 'roster', roster.slug, 'rows', search] as const,
    queryFn: () => {
      const term = search.q?.trim().toLowerCase() ?? '';
      const all = roster.rows
        .map((row): RosterPageRow => ({ ...row, state: row.covered ? 'Assigned' : 'Uncovered' }))
        .filter(
          (row) =>
            term === '' ||
            row.callsign.toLowerCase().includes(term) ||
            (row.name ?? '').toLowerCase().includes(term) ||
            (row.controller?.name ?? '').toLowerCase().includes(term) ||
            String(row.controller?.vid ?? '').includes(term),
        );
      const start = (search.page - 1) * search.pageSize;

      return Promise.resolve({
        items: all.slice(start, start + search.pageSize),
        page: search.page,
        pageSize: search.pageSize,
        total: all.length,
      });
    },
  });
}

export function RosterPage() {
  const { t, i18n } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();
  // A module route is not in the generated tree, so its parameters are not typed.
  const { slug = '' } = useParams({ strict: false });
  const { data: roster, isPending } = useQuery(publicRosterQuery(slug));
  const { data: bootstrap } = useQuery(bootstrapQuery);
  const [search, setSearch] = useState<ListSearch>(() => listSearchSchema.parse({ pageSize: 100 }));

  if (isPending || bootstrap === undefined) {
    return (
      <p className="text-muted-foreground mx-auto w-full max-w-4xl px-4 py-10 text-sm">
        {t('common.loading')}
      </p>
    );
  }

  if (roster === undefined || roster === null) {
    return <NotFound />;
  }

  const title = read(roster.title) || roster.slug;
  const signedIn = bootstrap.user !== null;

  return (
    <article className="mx-auto flex w-full max-w-4xl flex-col gap-6 px-4 py-10">
      <PageMetadata title={t('events:roster.title', { title })} description={t('events:roster.lead')} />

      <header className="flex flex-col gap-2">
        <H1>{t('events:roster.title', { title })}</H1>
        <Lead>{t('events:roster.lead')}</Lead>
        <p className="text-sm">
          <RouterAnchor href={eventHref(roster.slug)} className="underline">
            {t('events:roster.toEvent')}
          </RouterAnchor>
        </p>
      </header>

      {roster.cancelledAt === null ? null : <Notice tone="warning" title={t('events:roster.cancelled')} />}

      {!roster.published ? (
        <p className="text-muted-foreground text-sm">
          {t('events:roster.notYet', { date: moment(roster.publishesAtUtc) })}
        </p>
      ) : (
        <>
          {signedIn ? null : (
            <p className="text-sm">
              <a
                href={loginHref(`${window.location.pathname}${window.location.search}`)}
                className="underline"
              >
                {t('events:roster.signIn')}
              </a>
            </p>
          )}
          <DataList
            columns={signedIn ? memberColumns : visitorColumns}
            query={rowsQuery(roster, search)}
            labels="events:roster"
            locale={i18n.language}
            defaultLocale={bootstrap.division.defaultLocale}
            timezone={bootstrap.division.timezone}
            search={search}
            onSearchChange={(patch) => setSearch((current) => ({ ...current, ...patch }))}
            emptyDescription={t('events:roster.empty')}
          />
        </>
      )}
    </article>
  );
}
