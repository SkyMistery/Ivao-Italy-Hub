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
  eventQuery,
  routeQuery,
  routeToFormValues,
  routesQuery,
  useDeleteRoute,
  useSaveRoute,
  type EventDetailDto,
  type EventRouteDto,
} from '../api';
import { emptyRoute, routeSchema } from '../schemas';

/**
 * The routes of an event (design M4 §1.4, §7.2, E4): a tab of the event's page with the generated list, each route opened in a
 * generated form on a page of its own, as the airports are. The flight operations write them, from a grant on the events
 * department that gives them this area and not the event (§6.2): whether they may is the server's to say, and whether an airport
 * exists too.
 */

const EVENTS = '/staff/events';

const columns: readonly ColumnSpec<EventRouteDto>[] = [
  col.text('departureIcao', { sortable: true }),
  col.text('arrivalIcao', { sortable: true }),
  col.text('route'),
  col.localized('remarks'),
];

/**
 * The routes of an event, in the order they were written — the order of its page —, or by an airport when a column is sorted: the
 * page and the search are the tab's, the address is the event's.
 */
export function RoutesTab({ event, editable }: { event: EventDetailDto; editable: boolean }) {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const [search, setSearch] = useState<ListSearch>(() => listSearchSchema.parse({ pageSize: 100 }));
  const base = `${EVENTS}/${event.id}`;

  const create = editable ? (
    <Button asChild>
      <RouterAnchor href={`${base}/routes/new`}>
        <Plus aria-hidden className="mr-2 size-4" />
        {t('events:routes.create')}
      </RouterAnchor>
    </Button>
  ) : null;

  return (
    <DataList
      columns={columns}
      query={routesQuery(event.id, search)}
      labels="events:routes"
      locale={i18n.language}
      defaultLocale={bootstrap.division.defaultLocale}
      timezone={bootstrap.division.timezone}
      search={search}
      onSearchChange={(patch) => setSearch((current) => ({ ...current, ...patch }))}
      {...(create === null ? {} : { toolbar: create })}
      actions={(row) =>
        editable ? (
          <Button asChild variant="ghost" size="sm">
            <RouterAnchor href={`${base}/routes/${row.id}`}>{t('common.edit')}</RouterAnchor>
          </Button>
        ) : null
      }
    />
  );
}

/** One route of an event: the event in the breadcrumb, the generated form, "delete" for one that exists, and back to the tab. */
export function RouteForm() {
  const { t, i18n } = useTranslation();
  const read = useLocalized();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const navigate = useNavigate();
  // The routes of a module are registered from its manifest, so their parameters are not in the router's typed tree.
  const params: Readonly<Record<string, string | undefined>> = useParams({ strict: false });
  const eventId = Number(params.id);
  const id = (params.routeId ?? 'new') === 'new' ? null : Number(params.routeId);
  const locales = bootstrap.division.locales;

  const event = useQuery(eventQuery(eventId)).data;
  const route = useQuery({ ...routeQuery(id ?? 0), enabled: id !== null }).data ?? null;
  const save = useSaveRoute(id);
  const remove = useDeleteRoute();

  if (event === undefined || (id !== null && route === null)) {
    return null;
  }

  const back = `${EVENTS}/${eventId}?tab=routes`;
  const title = t(id === null ? 'events:routes.create' : 'events:routes.edit');
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
        route === null ? undefined : (
          <ConfirmDialog
            triggerText={t('common.delete')}
            title={t('events:routes.delete.title')}
            description={t('events:routes.delete.description')}
            confirmText={t('common.delete')}
            disabled={remove.isPending}
            onConfirm={() => remove.mutate(route.id, { onSuccess: () => void navigate({ href: back }) })}
          />
        )
      }
    >
      <div className="flex flex-col gap-6">
        {refusal === null ? null : <Notice tone="error" title={refusal} />}
        <SchemaForm
          schema={routeSchema}
          defaults={route === null ? emptyRoute(eventId, locales) : routeToFormValues(route, locales)}
          locales={locales}
          labels="events:routes"
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
