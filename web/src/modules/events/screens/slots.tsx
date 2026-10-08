import { Button, Input, Subtle } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate, useParams, useRouteContext } from '@tanstack/react-router';
import { Plus, Upload } from 'lucide-react';
import { useId, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { ApiError } from '../../../shared/api/problem';
import { SchemaForm, describeProblem } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { DataList, col, listSearchSchema, type ColumnSpec, type ListSearch } from '../../../shared/list';
import { ConfirmDialog, Notice, PageShell } from '../../../shared/ui';
import {
  eventQuery,
  slotQuery,
  slotToFormValues,
  slotsQuery,
  useDeleteFreeSlots,
  useDeleteSlot,
  useLoadSlots,
  useSaveSlot,
  type EventDetailDto,
  type EventSlotDto,
} from '../api';
import { SLOT_COLUMNS, emptySlot, slotLoadSchema, slotSchema, type SlotLoadFormValues } from '../schemas';

import { sheetProblems } from './slotList';

/**
 * The slots of an event in the back office (design M4 §1.5, §3.1, §7.2, E5): a tab of the event's page with the generated list, a
 * page that loads a table — pasted from a spreadsheet, or a CSV file — all or nothing, each slot opened in a generated form for the
 * corrections, and «delete the free ones». Whoever reads the bookings of the event has the tab, whoever writes them its buttons;
 * every rule — an airport of the event, a type the hub knows, a rotation that holds — is the server's to say.
 */

const EVENTS = '/staff/events';

const columns: readonly ColumnSpec<EventSlotDto>[] = [
  col.text('callsign', { sortable: true }),
  col.text('flightNumber'),
  col.list('aircraftTypes'),
  col.text('departureIcao'),
  col.date('offBlockUtc', { sortable: true }),
  col.text('arrivalIcao'),
  col.date('onBlockUtc', { sortable: true }),
  col.text('stand'),
  col.text('rotationCode', { sortable: true }),
  col.number('rotationLeg'),
];

/** The slots of an event, by their time at its airport: the page and the search are the tab's, the address is the event's. */
export function SlotsTab({ event, editable }: { event: EventDetailDto; editable: boolean }) {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const [search, setSearch] = useState<ListSearch>(() => listSearchSchema.parse({ pageSize: 100 }));
  const deleteFree = useDeleteFreeSlots(event.id);
  const base = `${EVENTS}/${event.id}`;
  const refusal = describeProblem(deleteFree.error, t, i18n.language);

  const tools = editable ? (
    <div className="flex flex-wrap items-center gap-2">
      <Button asChild>
        <RouterAnchor href={`${base}/slots/load`}>
          <Upload aria-hidden className="mr-2 size-4" />
          {t('events:slots.load.open')}
        </RouterAnchor>
      </Button>
      <Button asChild variant="outline">
        <RouterAnchor href={`${base}/slots/new`}>
          <Plus aria-hidden className="mr-2 size-4" />
          {t('events:slots.create')}
        </RouterAnchor>
      </Button>
      <ConfirmDialog
        triggerText={t('events:slots.deleteFree.trigger')}
        title={t('events:slots.deleteFree.title')}
        description={t('events:slots.deleteFree.description')}
        confirmText={t('events:slots.deleteFree.confirm')}
        disabled={deleteFree.isPending}
        onConfirm={() => deleteFree.mutate()}
      />
    </div>
  ) : null;

  return (
    <div className="flex flex-col gap-4">
      {deleteFree.isSuccess ? (
        <Notice tone="success" title={t('events:slots.deleteFree.done', { count: deleteFree.data })} />
      ) : null}
      {refusal === null ? null : <Notice tone="error" title={refusal} />}
      <DataList
        columns={columns}
        query={slotsQuery(event.id, search)}
        labels="events:slots"
        locale={i18n.language}
        defaultLocale={bootstrap.division.defaultLocale}
        timezone={bootstrap.division.timezone}
        search={search}
        onSearchChange={(patch) => setSearch((current) => ({ ...current, ...patch }))}
        {...(tools === null ? {} : { toolbar: tools })}
        actions={(row) =>
          editable && row.kind === 'Public' ? (
            <Button asChild variant="ghost" size="sm">
              <RouterAnchor href={`${base}/slots/${row.id}`}>{t('common.edit')}</RouterAnchor>
            </Button>
          ) : null
        }
      />
    </div>
  );
}

/** One slot of an event: the event in the breadcrumb, the generated form, "delete" for one that exists, and back to the tab. */
export function SlotForm() {
  const { t, i18n } = useTranslation();
  const read = useLocalized();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const navigate = useNavigate();
  // The routes of a module are registered from its manifest, so their parameters are not in the router's typed tree.
  const params: Readonly<Record<string, string | undefined>> = useParams({ strict: false });
  const eventId = Number(params.id);
  const id = (params.slotId ?? 'new') === 'new' ? null : Number(params.slotId);

  const event = useQuery(eventQuery(eventId)).data;
  const slot = useQuery({ ...slotQuery(id ?? 0), enabled: id !== null }).data ?? null;
  const save = useSaveSlot(id);
  const remove = useDeleteSlot();

  if (event === undefined || (id !== null && slot === null)) {
    return null;
  }

  const back = `${EVENTS}/${eventId}?tab=slots`;
  const title = t(id === null ? 'events:slots.create' : 'events:slots.edit');
  const refusal = describeProblem(remove.error, t, i18n.language);

  return (
    <PageShell
      title={title}
      breadcrumb={[
        { label: t('events:nav.section') },
        { label: t('events:events.title'), to: EVENTS },
        { label: read(event.title) || t('events:events.edit'), to: back },
        { label: title },
      ]}
      actions={
        slot === null ? undefined : (
          <ConfirmDialog
            triggerText={t('common.delete')}
            title={t('events:slots.delete.title')}
            description={t('events:slots.delete.description')}
            confirmText={t('common.delete')}
            disabled={remove.isPending}
            onConfirm={() => remove.mutate(slot.id, { onSuccess: () => void navigate({ href: back }) })}
          />
        )
      }
    >
      <div className="flex flex-col gap-6">
        {refusal === null ? null : <Notice tone="error" title={refusal} />}
        <SchemaForm
          schema={slotSchema}
          defaults={slot === null ? emptySlot(eventId) : slotToFormValues(slot)}
          locales={bootstrap.division.locales}
          labels="events:slots"
          division={{
            defaultLocale: bootstrap.division.defaultLocale,
            timezone: bootstrap.division.timezone,
          }}
          onSubmit={async (values) => {
            await save.mutateAsync(values);
            void navigate({ href: back });
          }}
          submitLabel={t('common.save')}
          secondaryAction={
            <Button asChild variant="ghost">
              <RouterAnchor href={back}>{t('common.cancel')}</RouterAnchor>
            </Button>
          }
        />
      </div>
    </PageShell>
  );
}

/** The refusals of a load, as the server keyed them, or null for any other answer: the table is listed row by row. */
function loadProblems(error: unknown): Readonly<Record<string, readonly string[]>> | null {
  if (!(error instanceof ApiError) || error.status !== 400) {
    return null;
  }

  const errors = error.problem?.errors ?? {};
  return Object.keys(errors).length === 0 ? null : errors;
}

/**
 * «Paste or load» (§3.1): the table copied from a spreadsheet, or the text of a CSV file read in the browser — the same box —, and
 * what to do with the public slots already there. All or nothing: what the server refuses is listed row by row, in the words of the
 * table — its rows, its columns —, and nothing is written until every row is right.
 */
export function SlotLoadPage() {
  const { t, i18n } = useTranslation();
  const read = useLocalized();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const navigate = useNavigate();
  const fileId = useId();
  const eventId = Number(useParams({ strict: false }).id);

  const event = useQuery(eventQuery(eventId)).data;
  const load = useLoadSlots(eventId);
  const [drawn, setDrawn] = useState<{ key: number; values: SlotLoadFormValues }>(() => ({
    key: 0,
    values: { text: '', mode: 'Add' },
  }));
  // What the form holds now, so that a file read keeps the mode chosen.
  const written = useRef<SlotLoadFormValues>(drawn.values);

  if (event === undefined) {
    return null;
  }

  const back = `${EVENTS}/${eventId}?tab=slots`;
  const title = t('events:slots.load.title');
  const problems = loadProblems(load.error);
  // Anything else — a 403, a 409 — is one sentence.
  const refusal = problems === null ? describeProblem(load.error, t, i18n.language) : null;

  const chooseFile = async (file: File | undefined) => {
    if (file === undefined) {
      return;
    }

    const text = await file.text();
    written.current = { ...written.current, text };
    setDrawn((current) => ({ key: current.key + 1, values: written.current }));
  };

  return (
    <PageShell
      title={title}
      description={t('events:slots.load.description')}
      breadcrumb={[
        { label: t('events:nav.section') },
        { label: t('events:events.title'), to: EVENTS },
        { label: read(event.title) || t('events:events.edit'), to: back },
        { label: title },
      ]}
    >
      <div className="flex flex-col gap-6">
        <div className="flex flex-col gap-2">
          <Subtle className="text-sm">{t('events:slots.load.help')}</Subtle>
          <code className="bg-muted overflow-x-auto rounded px-2 py-1 text-xs whitespace-nowrap">
            {SLOT_COLUMNS.join('\t')}
          </code>
          <Subtle className="text-sm">{t('events:slots.load.formats')}</Subtle>
        </div>

        <div className="flex flex-col gap-1">
          <label htmlFor={fileId} className="text-sm">
            {t('events:slots.load.file')}
          </label>
          <Input
            id={fileId}
            type="file"
            accept=".csv,.tsv,.txt,text/csv,text/plain"
            className="w-fit"
            disabled={load.isPending}
            onChange={(change) => void chooseFile(change.target.files?.[0])}
          />
        </div>

        {refusal === null ? null : <Notice tone="error" title={refusal} />}
        {problems === null ? null : <SheetProblems errors={problems} />}

        <SchemaForm
          key={drawn.key}
          schema={slotLoadSchema}
          defaults={drawn.values}
          locales={bootstrap.division.locales}
          labels="events:slots.load"
          onChange={(values) => {
            written.current = values;
          }}
          onSubmit={async (values) => {
            try {
              await load.mutateAsync(values);
            } catch {
              // The refusals are listed above the form, row by row: none is about a field of it.
              return;
            }

            // Loaded: the tab lists them.
            void navigate({ href: back });
          }}
          submitLabel={t('events:slots.load.submit')}
          secondaryAction={
            <Button asChild variant="ghost">
              <RouterAnchor href={back}>{t('common.cancel')}</RouterAnchor>
            </Button>
          }
        />
      </div>
    </PageShell>
  );
}

/** What the server refused of a table: what is about the whole of it, then row by row, each cell by the column of the header. */
function SheetProblems({ errors }: { errors: Readonly<Record<string, readonly string[]>> }) {
  const { t } = useTranslation();

  return (
    <Notice
      tone="error"
      title={t('events:slots.load.refused')}
      description={
        <ul className="list-disc pl-5">
          {sheetProblems(errors).map((problem) => {
            const said = problem.keys.map((key) => t(key)).join(' ');

            return (
              <li key={`${problem.row ?? 'table'}-${problem.column ?? ''}`}>
                {problem.row === null
                  ? said
                  : t('events:slots.load.cell', { row: problem.row, column: problem.column, said })}
              </li>
            );
          })}
        </ul>
      }
    />
  );
}
