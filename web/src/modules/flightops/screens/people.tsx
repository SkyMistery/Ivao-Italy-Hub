import { Badge, Button, H2, H3 } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate, useParams, useSearch } from '@tanstack/react-router';
import type { ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { holdsPermissionAnywhere } from '../../../shared/api/bootstrap';
import { SchemaForm, type ChoiceOption } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import { DataList, ListFilter, col, type ColumnSpec } from '../../../shared/list';
import { ConfirmDialog, NotFound, PageShell, useNotice } from '../../../shared/ui';
import {
  banQuery,
  banToFormValues,
  bansListQuery,
  emptyBan,
  memberName,
  pilotQuery,
  toursListQuery,
  useSaveBan,
  useValidatorGrant,
  validatorsQuery,
  type BanDto,
  type BanRow,
  type MemberDto,
  type PilotPageDto,
  type ValidatorsDto,
} from '../api';
import { TOURS_BAN, TOURS_MANAGE_VALIDATORS } from '../permissions';
import {
  banFormSearchSchema,
  banSchema,
  bansSearchSchema,
  pilotLookupSchema,
  validatorSchema,
  yearSearchSchema,
  type BanFormValues,
  type ValidatorFormValues,
} from '../schemas';

import { ProgressLine } from './ProgressLine';
import { REPORT_STATUS_COLOURS } from './reviewing';
import { useStaff } from './hooks';
import { earlierYears } from './progress';

/**
 * The people of the tours (design M2 §8.7), the pages of T15b over the server of T15a: the statistics of the validators with
 * «add a validator» and «remove», the pilot's page asked by VID, and the bans — a generated list and form. The note is
 * 2026-09-24-le-pagine-delle-persone; everything counted here is counted by the server.
 */

export const VALIDATORS = '/staff/tours/validators';
export const PILOTS = '/staff/tours/pilots';
export const BANS = '/staff/tours/bans';

/** The pilot's page of the staff, which the validation page links to as well. */
function pilotHref(vid: number): string {
  return `${PILOTS}/${vid}`;
}

// ---- what the pages share ---------------------------------------------------------------------------

/** The calendar year a page counts, out of `?year=`: the current one when none is asked (UTC, as the server counts). */
function useYear() {
  const search = yearSearchSchema.parse(useSearch({ strict: false }));
  const navigate = useNavigate();
  const current = new Date().getUTCFullYear();

  return {
    year: search.year ?? current,
    current,
    onYear: (year: number | undefined) =>
      void navigate({
        search: ((previous: Record<string, unknown>) => ({ ...previous, year })) as never,
        to: '.',
      }),
  };
}

/** The year, as a filter whose «nothing chosen» is the current year and whose choices are the years before it. */
function YearFilter({ year, current, onYear }: ReturnType<typeof useYear>) {
  const { t } = useTranslation();

  return (
    <ListFilter
      id="people-year"
      label={t('flightops:people.year')}
      none={String(current)}
      value={year === current ? undefined : String(year)}
      onChange={(value) => onYear(value === undefined ? undefined : Number(value))}
      items={earlierYears(current).map((earlier) => ({ value: String(earlier), label: String(earlier) }))}
      className="min-w-32"
    />
  );
}

function Section({ title, children }: { title: string; children: ReactNode }) {
  return (
    <section className="flex flex-col gap-3">
      <H2 className="text-lg">{title}</H2>
      {children}
    </section>
  );
}

/** A table to read, not a list to page: the counts of a year are few, and the server sends them whole. */
function Table({
  head,
  rows,
  empty,
}: {
  head: readonly string[];
  rows: readonly ReactNode[][];
  empty: string;
}) {
  if (rows.length === 0) {
    return <p className="text-muted-foreground text-sm">{empty}</p>;
  }

  return (
    <div className="overflow-x-auto">
      <table className="w-full text-sm">
        <thead>
          <tr className="text-muted-foreground border-b text-left">
            {head.map((label, index) => (
              <th key={index} className="py-2 pr-3 font-medium">
                {label}
              </th>
            ))}
          </tr>
        </thead>
        <tbody>
          {rows.map((cells, index) => (
            <tr key={index} className="border-b align-top last:border-0">
              {cells.map((cell, column) => (
                <td key={column} className="py-2 pr-3">
                  {cell}
                </td>
              ))}
            </tr>
          ))}
        </tbody>
      </table>
    </div>
  );
}

function Count({ value }: { value: number }) {
  return <span className="tabular-nums">{value}</span>;
}

// ---- the validators ---------------------------------------------------------------------------------

export function ValidatorsPage() {
  const { t } = useTranslation();
  const { bootstrap } = useStaff();
  const year = useYear();
  const stats = useQuery(validatorsQuery(year.year));
  const manages = holdsPermissionAnywhere(bootstrap, TOURS_MANAGE_VALIDATORS);

  return (
    <PageShell
      title={t('flightops:validators.title')}
      description={t('flightops:validators.description')}
      breadcrumb={[{ label: t('flightops:nav.section') }, { label: t('flightops:validators.title') }]}
      actions={<YearFilter {...year} />}
    >
      {stats.data === undefined ? (
        <p className="text-muted-foreground text-sm">{t('common.loading')}</p>
      ) : (
        <div className="flex flex-col gap-8">
          <Section title={t('flightops:validators.sections.validators', { year: year.year })}>
            <ValidatorTable stats={stats.data} manages={manages} />
          </Section>
          <Section title={t('flightops:validators.sections.tours', { year: year.year })}>
            <ToursOfTheYear stats={stats.data} />
          </Section>
          {manages ? (
            <Section title={t('flightops:validators.sections.add')}>
              <AddValidator />
            </Section>
          ) : null}
        </div>
      )}
    </PageShell>
  );
}

function ValidatorTable({ stats, manages }: { stats: ValidatorsDto; manages: boolean }) {
  const { t } = useTranslation();
  const read = useLocalized();
  const titles = new Map(stats.titles.map((entry) => [entry.tourId, read(entry.title)]));

  return (
    <Table
      head={[
        t('flightops:validators.fields.member'),
        t('flightops:validators.fields.enabledOn'),
        t('flightops:validators.fields.accepted'),
        t('flightops:validators.fields.rejected'),
        t('flightops:validators.fields.toModify'),
      ]}
      empty={t('flightops:validators.none')}
      rows={stats.validators.map((validator) => [
        <span key="member" className="flex flex-wrap items-center gap-2">
          {memberName(validator.member, t)}
          {validator.suspended ? (
            <Badge variant="flat" color="gray" text={t('flightops:validators.suspended')} />
          ) : null}
        </span>,
        <Enabled
          key="enabled"
          vid={validator.member.vid}
          allTours={validator.allTours}
          tours={validator.tourIds.map((id) => ({ id, title: titles.get(id) ?? `#${id}` }))}
          manages={manages}
        />,
        <Count key="accepted" value={validator.accepted} />,
        <Count key="rejected" value={validator.rejected} />,
        <Count key="toModify" value={validator.toModify} />,
      ])}
    />
  );
}

/**
 * Where a validator is enabled by a grant of their own — every tour, or tours of the first level — each with «remove». Who
 * validates through a position has none of these, and reads «by position».
 */
function Enabled({
  vid,
  allTours,
  tours,
  manages,
}: {
  vid: number;
  allTours: boolean;
  tours: readonly { id: number; title: string }[];
  manages: boolean;
}) {
  const { t } = useTranslation();
  const grant = useValidatorGrant();
  const notice = useNotice();

  const entries: { tourId: number | null; label: string }[] = [
    ...(allTours ? [{ tourId: null, label: t('flightops:validators.allTours') }] : []),
    ...tours.map((tour) => ({ tourId: tour.id, label: tour.title })),
  ];

  if (entries.length === 0) {
    return <span className="text-muted-foreground">{t('flightops:validators.byPosition')}</span>;
  }

  return (
    <ul className="flex flex-col gap-1">
      {entries.map((entry) => (
        <li key={entry.tourId ?? 'all'} className="flex flex-wrap items-center gap-2">
          <span>{entry.label}</span>
          {manages ? (
            <ConfirmDialog
              triggerText={t('flightops:validators.remove')}
              title={t('flightops:validators.removeTitle', { tour: entry.label })}
              description={t('flightops:validators.removeDescription')}
              confirmText={t('flightops:validators.remove')}
              disabled={grant.isPending}
              onConfirm={() =>
                grant.mutate(
                  { add: false, vid, tourId: entry.tourId },
                  {
                    onSuccess: () => notice({ tone: 'success', title: t('flightops:validators.removed') }),
                  },
                )
              }
            />
          ) : null}
        </li>
      ))}
    </ul>
  );
}

/** For each tour decided in the year, what every validator decided on it. */
function ToursOfTheYear({ stats }: { stats: ValidatorsDto }) {
  const { t } = useTranslation();
  const read = useLocalized();
  const members = new Map<number, MemberDto>(stats.validators.map((row) => [row.member.vid, row.member]));

  if (stats.tours.length === 0) {
    return <p className="text-muted-foreground text-sm">{t('flightops:validators.noTours')}</p>;
  }

  return (
    <div className="flex flex-col gap-6">
      {stats.tours.map((tour) => (
        <div key={tour.tourId} className="flex flex-col gap-2">
          <H3 className="text-base">{read(tour.title)}</H3>
          <Table
            head={[
              t('flightops:validators.fields.member'),
              t('flightops:validators.fields.accepted'),
              t('flightops:validators.fields.rejected'),
              t('flightops:validators.fields.toModify'),
            ]}
            empty=""
            rows={tour.counts.map((count) => [
              members.has(count.vid) ? memberName(members.get(count.vid) as MemberDto, t) : String(count.vid),
              <Count key="accepted" value={count.accepted} />,
              <Count key="rejected" value={count.rejected} />,
              <Count key="toModify" value={count.toModify} />,
            ])}
          />
        </div>
      ))}
    </div>
  );
}

/** The tours of the first level a validator or a ban may name: the back office's list without subtours and templates. */
function useFirstLevelTours(): ChoiceOption[] {
  const read = useLocalized();
  const tours = useQuery(toursListQuery({ page: 1, pageSize: 100, dir: 'asc' }, { isTemplate: false }));

  return (tours.data?.items ?? []).map((tour) => ({
    value: String(tour.id),
    label: read(tour.title) || `#${tour.id}`,
  }));
}

/**
 * «Add a validator» (§7.2): a VID and a tour of the first level, or nothing chosen for every tour. The refusals — not staff,
 * not a tour of the first level — come back on their field. The one enabled has it from their next sign in.
 */
function AddValidator() {
  const { t } = useTranslation();
  const { bootstrap } = useStaff();
  const grant = useValidatorGrant();
  const notice = useNotice();
  const tours = useFirstLevelTours();

  return (
    <SchemaForm<ValidatorFormValues>
      key={tours.length}
      schema={validatorSchema(tours)}
      defaults={{}}
      locales={bootstrap.division.locales}
      labels="flightops:validators"
      onSubmit={async (values) => {
        await grant.mutateAsync({
          add: true,
          vid: values.vid ?? 0,
          tourId: values.tourId === undefined || values.tourId === '' ? null : Number(values.tourId),
        });
        notice({
          tone: 'success',
          title: t('flightops:validators.added'),
          description: t('flightops:validators.addedDescription'),
        });
      }}
      submitLabel={t('flightops:validators.add')}
    />
  );
}

// ---- the pilots -------------------------------------------------------------------------------------

/** «Pilots»: a VID, and the pilot's page (note 2026-09-24-le-pagine-delle-persone §2). */
export function PilotLookupPage() {
  const { t } = useTranslation();
  const { bootstrap } = useStaff();
  const navigate = useNavigate();

  return (
    <PageShell
      title={t('flightops:pilots.title')}
      description={t('flightops:pilots.description')}
      breadcrumb={[{ label: t('flightops:nav.section') }, { label: t('flightops:pilots.title') }]}
    >
      <SchemaForm
        schema={pilotLookupSchema}
        defaults={{}}
        locales={bootstrap.division.locales}
        labels="flightops:pilots"
        onSubmit={async (values) => {
          if (values.vid !== undefined) {
            await navigate({ href: pilotHref(values.vid) });
          }
        }}
        submitLabel={t('flightops:pilots.open')}
      />
    </PageShell>
  );
}

export function PilotPage() {
  const { t } = useTranslation();
  // `$id`, like every other screen of the module: it is the pilot's VID.
  const vid = String(useParams({ strict: false }).id ?? '');
  const year = useYear();
  const known = /^\d+$/.test(vid);
  const pilot = useQuery({ ...pilotQuery(Number(vid), year.year), enabled: known });

  if (known && pilot.isPending) {
    return <p className="text-muted-foreground text-sm">{t('common.loading')}</p>;
  }

  return pilot.data === undefined || pilot.data === null ? (
    <NotFound />
  ) : (
    <PilotScreen page={pilot.data} year={year} />
  );
}

function PilotScreen({ page, year }: { page: PilotPageDto; year: ReturnType<typeof useYear> }) {
  const { t } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();
  const vid = page.pilot.vid;

  return (
    <PageShell
      title={memberName(page.pilot, t)}
      breadcrumb={[
        { label: t('flightops:nav.section') },
        { label: t('flightops:pilots.title'), to: PILOTS },
        { label: String(vid) },
      ]}
      actions={
        <div className="flex flex-wrap items-end gap-3">
          <YearFilter {...year} />
          {page.canBan ? (
            <Button asChild variant="outline">
              <RouterAnchor href={`${BANS}/new?vid=${vid}`}>{t('flightops:pilots.ban')}</RouterAnchor>
            </Button>
          ) : null}
        </div>
      }
    >
      <div className="flex flex-col gap-8">
        <Section title={t('flightops:pilots.sections.tours')}>
          {page.tours.length === 0 ? (
            <p className="text-muted-foreground text-sm">{t('flightops:pilots.noTours')}</p>
          ) : (
            <ul className="flex flex-col divide-y">
              {page.tours.map((tour) => (
                <li key={tour.tourId} className="flex flex-col gap-2 py-3">
                  <span className="font-semibold">
                    {read(tour.title)}
                    <span className="text-muted-foreground ml-2 text-sm font-normal">
                      {t('flightops:pilots.startedOn', { date: moment(tour.startedAt, { time: false }) })}
                    </span>
                  </span>
                  <ProgressLine tour={tour} />
                </li>
              ))}
            </ul>
          )}
        </Section>

        <Section title={t('flightops:pilots.sections.errors')}>
          <Table
            head={[
              t('flightops:pilots.fields.category'),
              t('flightops:pilots.fields.inYear', { year: page.year }),
              t('flightops:pilots.fields.ever'),
            ]}
            empty={t('flightops:pilots.noErrors')}
            rows={page.categories.map((category) => [
              <CategoryBadge key="category" category={category.category} />,
              <Count key="inYear" value={category.inYear} />,
              <Count key="ever" value={category.ever} />,
            ])}
          />
          {page.errors.length === 0 ? null : (
            <Table
              head={[
                t('flightops:pilots.fields.error'),
                t('flightops:pilots.fields.category'),
                t('flightops:pilots.fields.inYear', { year: page.year }),
                t('flightops:pilots.fields.ever'),
              ]}
              empty=""
              rows={page.errors.map((error) => [
                read(error.name) || `#${error.errorId}`,
                <CategoryBadge key="category" category={error.category} />,
                <Count key="inYear" value={error.inYear} />,
                <Count key="ever" value={error.ever} />,
              ])}
            />
          )}
        </Section>

        <Section title={t('flightops:pilots.sections.flights')}>
          <Table
            head={[
              t('flightops:pilots.fields.tour'),
              t('flightops:pilots.fields.leg'),
              t('flightops:pilots.fields.takeoffAt'),
              t('flightops:pilots.fields.status'),
              t('flightops:pilots.fields.decidedBy'),
            ]}
            empty={t('flightops:pilots.noFlights')}
            rows={page.flights.map((flight) => [
              read(flight.tourTitle),
              <RouterAnchor key="leg" href={`/staff/tours/review/${flight.pirepId}`} className="underline">
                {flight.legNumber === null
                  ? `${flight.departureIcao} → ${flight.arrivalIcao}`
                  : `${flight.legNumber} · ${flight.departureIcao} → ${flight.arrivalIcao}`}
              </RouterAnchor>,
              <span key="takeoff" className="tabular-nums">
                {moment(flight.takeoffAt)}
              </span>,
              <span key="status" className="flex flex-wrap items-center gap-2">
                <Badge
                  variant="flat"
                  color={REPORT_STATUS_COLOURS[flight.status]}
                  text={t(`flightops:review.options.status.${flight.status}`)}
                />
                {flight.disputeStatus === null ? null : (
                  <span className="text-muted-foreground text-xs">
                    {t(`flightops:pilots.dispute.${flight.disputeStatus}`)}
                  </span>
                )}
              </span>,
              flight.decidedBy === null ? '—' : memberName(flight.decidedBy, t),
            ])}
          />
        </Section>

        <Section title={t('flightops:pilots.sections.threads')}>
          <p className="text-sm">
            {t('flightops:pilots.disputes', {
              open: page.disputes.open,
              upheld: page.disputes.upheld,
              dismissed: page.disputes.dismissed,
            })}
          </p>
          {page.threads.length === 0 ? (
            <p className="text-muted-foreground text-sm">{t('flightops:pilots.noThreads')}</p>
          ) : (
            <ul className="flex flex-col gap-1 text-sm">
              {page.threads.map((thread) => (
                <li key={thread.id}>
                  <RouterAnchor
                    href={`/staff/${thread.department.toLowerCase()}/contacts/${thread.id}`}
                    className="underline"
                  >
                    {thread.subject}
                  </RouterAnchor>{' '}
                  <span className="text-muted-foreground">
                    {t(`flightops:pilots.threadKind.${thread.kind}`, { defaultValue: thread.kind })} ·{' '}
                    {moment(thread.createdAt, { time: false })}
                  </span>
                </li>
              ))}
            </ul>
          )}
        </Section>

        <Section title={t('flightops:pilots.sections.bans')}>
          <BanLines bans={page.bans} canBan={page.canBan} />
          <p className="text-sm">
            <RouterAnchor href={`${BANS}?vid=${vid}`} className="underline">
              {t('flightops:pilots.allBans')}
            </RouterAnchor>
          </p>
        </Section>
      </div>
    </PageShell>
  );
}

function CategoryBadge({ category }: { category: PilotPageDto['categories'][number]['category'] }) {
  const { t } = useTranslation();

  return (
    <Badge
      variant="flat"
      color={category === 'Dangerous' ? 'red' : category === 'Warning' ? 'orange' : 'gray'}
      text={t(`flightops:tourErrors.options.category.${category}`)}
    />
  );
}

/**
 * The pilot's bans, newest first as the server orders them, each with its reach and why — a link to move its end for whoever
 * may write one, words for whoever only reads (a validator).
 */
function BanLines({ bans, canBan }: { bans: readonly BanDto[]; canBan: boolean }) {
  const { t } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();

  if (bans.length === 0) {
    return <p className="text-muted-foreground text-sm">{t('flightops:review.noBans')}</p>;
  }

  const reach = (ban: BanDto) =>
    ban.tourTitle === null ? t('flightops:validators.allTours') : read(ban.tourTitle);

  return (
    <ul className="flex flex-col gap-1 text-sm">
      {bans.map((ban) => (
        <li key={ban.id}>
          {canBan ? (
            <RouterAnchor href={`${BANS}/${ban.id}`} className="underline">
              {reach(ban)}
            </RouterAnchor>
          ) : (
            reach(ban)
          )}
          {': '}
          {t(ban.active ? 'flightops:review.banInForce' : 'flightops:review.banPast', {
            from: moment(ban.startsAt, { time: false }),
            to: ban.endsAt === null ? t('flightops:bans.forGood') : moment(ban.endsAt, { time: false }),
            reason: ban.reason,
          })}
        </li>
      ))}
    </ul>
  );
}

// ---- the bans ---------------------------------------------------------------------------------------

const banColumns: readonly ColumnSpec<BanRow>[] = [
  col.text('pilotName'),
  col.localized('tourTitle'),
  col.badge('reach', 'flightops:bans'),
  col.date('startsAt', { sortable: true }),
  col.date('endsAt'),
  col.boolean('active'),
  col.text('reason'),
];

export function BansPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useStaff();
  const navigate = useNavigate();
  const search = bansSearchSchema.parse(useSearch({ strict: false }));
  const bans = holdsPermissionAnywhere(bootstrap, TOURS_BAN);

  const onSearchChange = (patch: Partial<typeof search>) =>
    void navigate({
      search: ((previous: typeof search) => ({ ...previous, ...patch })) as never,
      to: '.',
    });

  const create = bans ? (
    <Button asChild>
      <RouterAnchor href={`${BANS}/new${search.vid === undefined ? '' : `?vid=${search.vid}`}`}>
        {t('flightops:bans.create')}
      </RouterAnchor>
    </Button>
  ) : null;

  return (
    <PageShell
      title={t('flightops:bans.title')}
      description={t('flightops:bans.description')}
      breadcrumb={[{ label: t('flightops:nav.section') }, { label: t('flightops:bans.title') }]}
      actions={create ?? undefined}
    >
      <DataList
        columns={banColumns}
        query={bansListQuery(search, search.vid === undefined ? {} : { vid: search.vid })}
        labels="flightops:bans"
        locale={i18n.language}
        defaultLocale={bootstrap.division.defaultLocale}
        timezone={bootstrap.division.timezone}
        search={search}
        onSearchChange={onSearchChange}
        actions={(row) => (
          <span className="flex gap-1">
            <Button asChild variant="ghost" size="sm">
              <RouterAnchor href={pilotHref(row.pilot.vid)}>{t('flightops:bans.toPilot')}</RouterAnchor>
            </Button>
            {bans ? (
              <Button asChild variant="ghost" size="sm">
                <RouterAnchor href={`${BANS}/${row.id}`}>{t('common.edit')}</RouterAnchor>
              </Button>
            ) : null}
          </span>
        )}
        {...(create === null ? {} : { emptyAction: create })}
      />
    </PageShell>
  );
}

/**
 * A ban, new or moved (§3.9): never deleted — lifting one early is moving its end. Opened from a pilot's page, it has the
 * pilot written already and goes back there when saved.
 */
export function BanForm() {
  const { t } = useTranslation();
  const { bootstrap } = useStaff();
  const navigate = useNavigate();
  const id = String(useParams({ strict: false }).id ?? 'new');
  const { vid } = banFormSearchSchema.parse(useSearch({ strict: false }));
  const isNew = id === 'new';
  const ban = useQuery({ ...banQuery(Number(id)), enabled: !isNew });
  const save = useSaveBan(isNew ? null : Number(id));
  const tours = useFirstLevelTours();
  const writes = holdsPermissionAnywhere(bootstrap, TOURS_BAN);

  if (!isNew && ban.isPending) {
    return <p className="text-muted-foreground text-sm">{t('common.loading')}</p>;
  }

  if ((!isNew && ban.data === undefined) || !writes) {
    return <NotFound />;
  }

  const back = (pilot: number) => void navigate({ href: vid === undefined ? BANS : pilotHref(pilot) });
  const title = isNew ? t('flightops:bans.create') : t('flightops:bans.edit');

  return (
    <PageShell
      title={title}
      breadcrumb={[
        { label: t('flightops:nav.section') },
        { label: t('flightops:bans.title'), to: BANS },
        { label: title },
      ]}
    >
      <SchemaForm<BanFormValues>
        key={`${ban.data?.rowVersion ?? 'new'}-${tours.length}`}
        schema={banSchema(tours)}
        defaults={ban.data === undefined ? emptyBan(new Date(), vid) : banToFormValues(ban.data)}
        locales={bootstrap.division.locales}
        labels="flightops:bans"
        division={{
          defaultLocale: bootstrap.division.defaultLocale,
          timezone: bootstrap.division.timezone,
        }}
        onSubmit={async (values) => {
          const saved = await save.mutateAsync(values);
          back(saved.pilot.vid);
        }}
        submitLabel={t('common.save')}
        secondaryAction={
          <Button asChild variant="ghost">
            <RouterAnchor href={vid === undefined ? BANS : pilotHref(vid)}>{t('common.cancel')}</RouterAnchor>
          </Button>
        }
      />
    </PageShell>
  );
}
