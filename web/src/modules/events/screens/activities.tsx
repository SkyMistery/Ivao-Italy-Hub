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
  activitiesQuery,
  activityQuery,
  activityToFormValues,
  eventQuery,
  staffSeatsQuery,
  useDeleteActivity,
  useSaveActivity,
  type EventActivityDto,
  type EventDetailDto,
  type EventSeatDto,
} from '../api';
import { activitySchema, emptyActivity } from '../schemas';

/**
 * The parallel activities of an event in person in the back office (design M4 §4-bis, §7.2, E17): a tab of the event's page with the
 * generated list — by the hour they start, each with its window, its turns and its seats, and how many seats the members took —, each
 * activity opened in a generated form on a page of its own, as the questions are; and a tab with the seats themselves — the member, named
 * with the core's helper (`col.person`), the activity, the turn —, read only: a seat goes with its registration when the staff take that
 * away. Both the bookings' area: whoever reads the bookings has the tabs, whoever writes them the activities.
 */

const EVENTS = '/staff/events';

const activityColumns: readonly ColumnSpec<EventActivityDto>[] = [
  col.localized('name'),
  col.date('fromUtc', { sortable: true }),
  col.date('toUtc'),
  col.number('slotMinutes'),
  col.number('seatsPerSlot'),
  col.number('turns'),
  col.number('seatsTaken'),
];

export function ActivitiesTab({ event, editable }: { event: EventDetailDto; editable: boolean }) {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const [search, setSearch] = useState<ListSearch>(() => listSearchSchema.parse({ pageSize: 100 }));
  const base = `${EVENTS}/${event.id}`;

  const create = editable ? (
    <Button asChild>
      <RouterAnchor href={`${base}/activities/new`}>
        <Plus aria-hidden className="mr-2 size-4" />
        {t('events:activities.create')}
      </RouterAnchor>
    </Button>
  ) : null;

  return (
    <DataList
      columns={activityColumns}
      query={activitiesQuery(event.id, search)}
      labels="events:activities"
      locale={i18n.language}
      defaultLocale={bootstrap.division.defaultLocale}
      timezone={bootstrap.division.timezone}
      search={search}
      onSearchChange={(patch) => setSearch((current) => ({ ...current, ...patch }))}
      emptyDescription={t('events:activities.empty')}
      {...(create === null ? {} : { toolbar: create })}
      actions={(row) =>
        editable ? (
          <Button asChild variant="ghost" size="sm">
            <RouterAnchor href={`${base}/activities/${row.id}`}>{t('common.edit')}</RouterAnchor>
          </Button>
        ) : null
      }
    />
  );
}

const seatColumns: readonly ColumnSpec<EventSeatDto>[] = [
  col.person('member'),
  col.localized('activity'),
  col.date('startsAtUtc', { sortable: true }),
  col.date('endsAtUtc'),
  col.date('createdAt', { sortable: true }),
];

export function SeatsTab({ event }: { event: EventDetailDto }) {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const [search, setSearch] = useState<ListSearch>(() => listSearchSchema.parse({ pageSize: 100 }));

  return (
    <DataList
      columns={seatColumns}
      query={staffSeatsQuery(event.id, search)}
      labels="events:seats"
      locale={i18n.language}
      defaultLocale={bootstrap.division.defaultLocale}
      timezone={bootstrap.division.timezone}
      search={search}
      onSearchChange={(patch) => setSearch((current) => ({ ...current, ...patch }))}
      emptyDescription={t('events:seats.empty')}
    />
  );
}

/** One activity of an event: the event in the breadcrumb, the generated form, "delete" for one that exists, and back to the tab. */
export function ActivityForm() {
  const { t, i18n } = useTranslation();
  const read = useLocalized();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const navigate = useNavigate();
  // The routes of a module are registered from its manifest, so their parameters are not in the router's typed tree.
  const params: Readonly<Record<string, string | undefined>> = useParams({ strict: false });
  const eventId = Number(params.id);
  const id = (params.activityId ?? 'new') === 'new' ? null : Number(params.activityId);
  const locales = bootstrap.division.locales;

  const event = useQuery(eventQuery(eventId)).data;
  const activity = useQuery({ ...activityQuery(id ?? 0), enabled: id !== null }).data ?? null;
  const save = useSaveActivity(id);
  const remove = useDeleteActivity();

  if (event === undefined || (id !== null && activity === null)) {
    return null;
  }

  const back = `${EVENTS}/${eventId}?tab=activities`;
  const title = t(id === null ? 'events:activities.create' : 'events:activities.edit');
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
        activity === null ? undefined : (
          <ConfirmDialog
            triggerText={t('common.delete')}
            title={t('events:activities.delete.title')}
            description={t('events:activities.delete.description')}
            confirmText={t('common.delete')}
            disabled={remove.isPending}
            onConfirm={() => remove.mutate(activity.id, { onSuccess: () => void navigate({ href: back }) })}
          />
        )
      }
    >
      <div className="flex flex-col gap-6">
        {refusal === null ? null : <Notice tone="error" title={refusal} />}
        <SchemaForm
          schema={activitySchema}
          defaults={
            activity === null
              ? emptyActivity(eventId, event.startsAtUtc ?? '', event.endsAtUtc ?? '', locales)
              : activityToFormValues(activity, locales)
          }
          locales={locales}
          labels="events:activities"
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
