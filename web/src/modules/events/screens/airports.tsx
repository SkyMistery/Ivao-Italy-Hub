import { Button } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate, useParams, useRouteContext } from '@tanstack/react-router';
import { Plus } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { SchemaForm, describeProblem } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { DataList, col, listSearchSchema, type ColumnSpec, type ListSearch } from '../../../shared/list';
import { ConfirmDialog, Notice, PageShell } from '../../../shared/ui';
import {
  airportQuery,
  airportToFormValues,
  airportsQuery,
  eventQuery,
  useDeleteAirport,
  useSaveAirport,
  type EventAirportDto,
  type EventDetailDto,
} from '../api';
import { airportSchema, emptyAirport } from '../schemas';

/**
 * The airports of an event and their capacity (design M4 §1.3, §7.2, E3a): a tab of the event's page with the generated list,
 * each airport opened in a generated form on a page of its own, as every back office screen of the hub (plan §16.6). Whether
 * an airport exists, and is in its event once, is the server's to say.
 */

const EVENTS = '/staff/events';

const columns: readonly ColumnSpec<EventAirportDto>[] = [
  col.number('ordinal', { sortable: true }),
  col.text('icao'),
  col.number('maxMovementsPerHour'),
  col.number('maxArrivalsPerHour'),
  col.number('maxDeparturesPerHour'),
];

/** The airports of an event, in their order: the page and the search are the tab's, the address is the event's. */
export function AirportsTab({ event, editable }: { event: EventDetailDto; editable: boolean }) {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const [search, setSearch] = useState<ListSearch>(() => listSearchSchema.parse({ pageSize: 100 }));
  const base = `${EVENTS}/${event.id}`;

  const create = editable ? (
    <Button asChild>
      <RouterAnchor href={`${base}/airports/new`}>
        <Plus aria-hidden className="mr-2 size-4" />
        {t('events:airports.create')}
      </RouterAnchor>
    </Button>
  ) : null;

  return (
    <DataList
      columns={columns}
      query={airportsQuery(event.id, search)}
      labels="events:airports"
      locale={i18n.language}
      defaultLocale={bootstrap.division.defaultLocale}
      timezone={bootstrap.division.timezone}
      search={search}
      onSearchChange={(patch) => setSearch((current) => ({ ...current, ...patch }))}
      {...(create === null ? {} : { toolbar: create, emptyAction: create })}
      actions={(row) =>
        editable ? (
          <Button asChild variant="ghost" size="sm">
            <RouterAnchor href={`${base}/airports/${row.id}`}>{t('common.edit')}</RouterAnchor>
          </Button>
        ) : null
      }
    />
  );
}

/** One airport of an event: the event in the breadcrumb, the generated form, "delete" for one that exists, and back to the tab. */
export function AirportForm() {
  const { t, i18n } = useTranslation();
  const read = useLocalized();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const navigate = useNavigate();
  // The routes of a module are registered from its manifest, so their parameters are not in the router's typed tree.
  const params: Readonly<Record<string, string | undefined>> = useParams({ strict: false });
  const eventId = Number(params.id);
  const id = (params.airportId ?? 'new') === 'new' ? null : Number(params.airportId);

  const event = useQuery(eventQuery(eventId)).data;
  const airport = useQuery({ ...airportQuery(id ?? 0), enabled: id !== null }).data ?? null;
  const airports = useQuery(airportsQuery(eventId)).data?.items;
  const save = useSaveAirport(id);
  const remove = useDeleteAirport();

  if (event === undefined || airports === undefined || (id !== null && airport === null)) {
    return null;
  }

  const back = `${EVENTS}/${eventId}?tab=airports`;
  const title = t(id === null ? 'events:airports.create' : 'events:airports.edit');
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
        airport === null ? undefined : (
          <ConfirmDialog
            triggerText={t('common.delete')}
            title={t('events:airports.delete.title')}
            description={t('events:airports.delete.description')}
            confirmText={t('common.delete')}
            disabled={remove.isPending}
            onConfirm={() => remove.mutate(airport.id, { onSuccess: () => void navigate({ href: back }) })}
          />
        )
      }
    >
      <div className="flex flex-col gap-6">
        {refusal === null ? null : <Notice tone="error" title={refusal} />}
        <SchemaForm
          schema={airportSchema}
          // A new airport goes after the last one.
          defaults={
            airport === null
              ? emptyAirport(eventId, Math.max(0, ...airports.map((row) => row.ordinal)) + 1)
              : airportToFormValues(airport)
          }
          locales={bootstrap.division.locales}
          labels="events:airports"
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
