import { Button, Tabs } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useBlocker, useNavigate, useParams, useRouteContext, useSearch } from '@tanstack/react-router';
import { Plus } from 'lucide-react';
import { useEffect, useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { readBody } from '../../../blocks';
import { BodyEditor } from '../../../features/content/BodyEditor';
import { mediaPickerQuery } from '../../../features/media/queries';
import {
  holdsPermission,
  holdsPermissionAnywhere,
  writableDepartments,
  type Department,
} from '../../../shared/api/bootstrap';
import { SchemaForm, describeProblem } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import { DataList, ListFilter, col, type ColumnSpec } from '../../../shared/list';
import { ConfirmDialog, Notice, PageShell } from '../../../shared/ui';
import {
  eventQuery,
  eventToFormValues,
  eventsListQuery,
  kindPresetsQuery,
  useCancelEvent,
  useDeleteEvent,
  useSaveEvent,
  type EventDetailDto,
  type EventListDto,
} from '../api';
import { EVENTS_DELETE, EVENTS_EDIT } from '../permissions';
import {
  EVENT_VIEWS,
  cancelSchema,
  emptyEvent,
  eventEditorSearchSchema,
  eventSchema,
  eventsSearchSchema,
  kindChoices,
  presetSwitches,
  type EventEditorTab,
  type EventFormValues,
  type EventView,
  type EventsSearch,
  type KindPreset,
} from '../schemas';

import { AirportsTab } from './airports';

/**
 * The events in the staff's back office (design M4 §7.2, E3a): the list of every event with the views of its state — drafts,
 * upcoming, in progress, ended, cancelled —; the page of one, with its settings as a generated form, its description written
 * with the editor of the content as a tour's briefing is, and its airports with their capacity; and what happens to it without
 * its form — cancelling it with a note, deleting one nobody took part in.
 *
 * The switches of an event are preset by its kind (§1.12): choosing a kind in the form sets them to what the division's
 * settings say of that kind, and the staff changes them before saving or afterwards. Everything else is the server's answer:
 * a refusal comes back field by field.
 */

export const EVENTS = '/staff/events';

const columns: readonly ColumnSpec<EventListDto>[] = [
  col.localized('title'),
  col.localized('kindLabel'),
  col.badge('state', 'events:events'),
  col.date('startsAtUtc', { sortable: true }),
  col.date('endsAtUtc', { sortable: true }),
  col.date('updatedAt', { sortable: true }),
];

function NewEventButton() {
  const { t } = useTranslation();

  return (
    <Button asChild>
      <RouterAnchor href={`${EVENTS}/new`}>
        <Plus aria-hidden className="mr-2 size-4" />
        {t('events:events.create')}
      </RouterAnchor>
    </Button>
  );
}

export function EventsPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const navigate = useNavigate();
  const search = eventsSearchSchema.parse(useSearch({ strict: false }));

  const onSearchChange = (patch: Partial<EventsSearch>) =>
    void navigate({
      search: ((previous: EventsSearch) => ({ ...previous, ...patch })) as never,
      to: '.',
    });

  const create = writableDepartments(bootstrap, EVENTS_EDIT).length > 0 ? <NewEventButton /> : null;

  return (
    <PageShell
      title={t('events:events.title')}
      description={t('events:events.description')}
      breadcrumb={[{ label: t('events:nav.section') }, { label: t('events:events.title') }]}
      actions={create ?? undefined}
    >
      <DataList
        columns={columns}
        query={eventsListQuery(search)}
        labels="events:events"
        locale={i18n.language}
        defaultLocale={bootstrap.division.defaultLocale}
        timezone={bootstrap.division.timezone}
        search={search}
        onSearchChange={onSearchChange}
        toolbar={
          <ListFilter
            id="events-view"
            label={t('events:events.filters.view')}
            none={t('events:events.filters.anyView')}
            value={search.view}
            onChange={(view) => onSearchChange({ view: view as EventView | undefined, page: 1 })}
            items={EVENT_VIEWS.map((view) => ({ value: view, label: t(`events:events.views.${view}`) }))}
          />
        }
        actions={(row) => (
          <Button asChild variant="ghost" size="sm">
            <RouterAnchor href={`${EVENTS}/${row.id}`}>{t('events:events.open')}</RouterAnchor>
          </Button>
        )}
        {...(create === null ? {} : { emptyAction: create })}
      />
    </PageShell>
  );
}

/**
 * The settings of an event as a generated form. Choosing a kind presets the switches (§1.12): the form follows what is written
 * as it is written, and when the kind changes it is drawn again with the same values and the switches the kind presets — the
 * generator is handed defaults, and draws them once.
 */
function EventForm({
  event,
  presets,
  department,
  editable,
  onSaved,
}: {
  event: EventDetailDto | null;
  presets: readonly KindPreset[];
  department: Department;
  editable: boolean;
  onSaved: (saved: EventDetailDto) => void;
}) {
  const { t } = useTranslation();
  const read = useLocalized();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const locales = bootstrap.division.locales;
  const save = useSaveEvent(event?.id ?? null);

  const [drawn, setDrawn] = useState(() => ({
    key: 0,
    values: event === null ? emptyEvent(locales) : eventToFormValues(event, locales),
  }));
  // What the form holds now: the kind it was last drawn or written with is the one a change is measured against.
  const written = useRef<EventFormValues>(drawn.values);

  const calendar = bootstrap.calendarKinds.map((kind) => ({
    value: kind.key,
    label: read(kind.label) || kind.key,
  }));

  return (
    <SchemaForm
      key={drawn.key}
      schema={eventSchema({ kinds: kindChoices(calendar, event === null ? [] : [event]) })}
      defaults={drawn.values}
      locales={locales}
      labels="events:events"
      // The banner comes from the library of the department, where it was uploaded.
      mediaLibrary={mediaPickerQuery(department)}
      division={{ defaultLocale: bootstrap.division.defaultLocale, timezone: bootstrap.division.timezone }}
      {...(editable
        ? {
            onChange: (values: EventFormValues) => {
              const before = written.current.kind;
              written.current = values;

              if (values.kind !== before) {
                const preset = { ...values, ...presetSwitches(presets, values.kind) };
                written.current = preset;
                setDrawn((current) => ({ key: current.key + 1, values: preset }));
              }
            },
            onSubmit: async (values: EventFormValues) => {
              onSaved(await save.mutateAsync({ values }));
            },
            submitLabel: t('common.save'),
            secondaryAction: (
              <Button asChild variant="ghost">
                <RouterAnchor href={EVENTS}>{t('common.cancel')}</RouterAnchor>
              </Button>
            ),
          }
        : {})}
    />
  );
}

/**
 * The description of an event (design M4 §1.2): the editor of the content's body, saved with the event's own `PUT` — as a
 * tour's briefing is —, by a press and not by itself, because the event's other fields travel in the same request.
 */
function EventDescription({ event }: { event: EventDetailDto }) {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const locales = bootstrap.division.locales;
  const save = useSaveEvent(event.id);

  const [initial] = useState(() => readBody(event.body));
  const [body, setBody] = useState(initial);
  // What was last stored, as one string: the description is dirty while the one on screen says something else.
  const [stored, setStored] = useState(() => JSON.stringify(initial));
  const [saved, setSaved] = useState(false);
  const dirty = JSON.stringify(body) !== stored;

  // On the way out — another tab, another screen, the browser's tab closed — a description not saved is asked about, in the
  // words the editor of the content uses.
  const leaving = useRef(dirty);
  useEffect(() => {
    leaving.current = dirty;
  });
  useBlocker({
    shouldBlockFn: () => leaving.current && !window.confirm(t('content.editor.autosave.leave')),
    enableBeforeUnload: () => leaving.current,
  });

  const refusal = describeProblem(save.error, t, i18n.language);

  return (
    <BodyEditor
      initial={initial}
      onChange={(next) => {
        setBody(next);
        setSaved(false);
      }}
      locales={locales}
      division={{ defaultLocale: bootstrap.division.defaultLocale, timezone: bootstrap.division.timezone }}
      // The pictures come from the library, like the banner.
      mediaLibrary={mediaPickerQuery(event.ownerDepartment)}
      // ⚠️ No block that asks for a permission belongs in a description: the only one that does, the interactive block,
      // runs in a frame the server builds for a row of the content alone.
      holds={() => false}
      // An event is not published through the content's publication, the only thing that captures a data block: every one
      // stays live here, as in a tour's briefing.
      captures={false}
      toolbar={(tools) => (
        <div className="flex flex-wrap items-center gap-2">
          <Button
            type="button"
            disabled={save.isPending || !dirty}
            onClick={() => {
              const sent = body;
              save.mutate(
                // The event as it stands, with this description: the row's version is the latest the cache has.
                { values: eventToFormValues(event, locales), body: sent },
                {
                  onSuccess: () => {
                    setStored(JSON.stringify(sent));
                    setSaved(true);
                  },
                },
              );
            }}
          >
            {t('events:events.body.save')}
          </Button>
          {tools}
        </div>
      )}
      header={
        refusal !== null ? (
          <Notice tone="error" title={refusal} />
        ) : saved && !dirty ? (
          <Notice tone="success" title={t('events:events.body.saved')} />
        ) : dirty ? (
          <p role="status" className="text-muted-foreground text-sm">
            {t('events:events.body.unsaved')}
          </p>
        ) : null
      }
    />
  );
}

/** The bar of an event that exists: what happens to it without its form — cancelled with a note, or deleted. */
function EventActions({ event }: { event: EventDetailDto }) {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const navigate = useNavigate();
  const remove = useDeleteEvent();

  // An event cancelled stays cancelled, and one that is over happened: the server refuses both, so neither is offered.
  const cancellable =
    holdsPermission(bootstrap, EVENTS_EDIT, event.ownerDepartment) &&
    event.state !== 'Cancelled' &&
    event.state !== 'Ended';
  // Deleting asks for Events.Delete on the event, which only the base department's heads hold (§6.3).
  const deletable = holdsPermission(bootstrap, EVENTS_DELETE, event.ownerDepartment);
  const refusal = describeProblem(remove.error, t, i18n.language);

  if (!cancellable && !deletable) {
    return null;
  }

  return (
    <div className="flex flex-col items-end gap-2">
      <div className="flex flex-wrap justify-end gap-2">
        {cancellable ? (
          <Button asChild variant="outline">
            <RouterAnchor href={`${EVENTS}/${event.id}/cancel`}>
              {t('events:events.actions.cancel')}
            </RouterAnchor>
          </Button>
        ) : null}
        {deletable ? (
          <ConfirmDialog
            triggerText={t('common.delete')}
            title={t('events:events.delete.title')}
            description={t('events:events.delete.description')}
            confirmText={t('common.delete')}
            disabled={remove.isPending}
            onConfirm={() => remove.mutate(event.id, { onSuccess: () => void navigate({ href: EVENTS }) })}
          />
        ) : null}
      </div>
      {refusal === null ? null : <Notice tone="error" title={refusal} />}
    </div>
  );
}

export function EventEditor() {
  const { t } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const navigate = useNavigate();
  const id = String(useParams({ strict: false }).id ?? 'new');
  const isNew = id === 'new';
  const tab: EventEditorTab = eventEditorSearchSchema.parse(useSearch({ strict: false })).tab ?? 'settings';
  const [saved, setSaved] = useState(false);

  const event = useQuery({ ...eventQuery(Number(id)), enabled: !isNew }).data ?? null;
  // The presets are read by whoever writes events, the only ones whose form follows a change of kind.
  const writer = holdsPermissionAnywhere(bootstrap, EVENTS_EDIT);
  const presets = useQuery({ ...kindPresetsQuery(), enabled: writer }).data;

  const department = event?.ownerDepartment ?? writableDepartments(bootstrap, EVENTS_EDIT)[0];

  if ((!isNew && event === null) || department === undefined || (writer && presets === undefined)) {
    return null;
  }

  const editable = event === null || holdsPermission(bootstrap, EVENTS_EDIT, event.ownerDepartment);
  const title = event === null ? t('events:events.create') : read(event.title) || t('events:events.edit');

  const settings = (
    <div className="flex flex-col gap-6">
      {saved ? <Notice tone="success" title={t('events:events.saved')} /> : null}
      {editable ? null : <Notice tone="info" title={t('events:events.readOnly')} />}
      <EventForm
        // A new form when the row changes underneath, so it draws what the server now holds.
        key={event?.rowVersion ?? 'new'}
        event={event}
        presets={presets ?? []}
        department={department}
        editable={editable}
        onSaved={(row) => {
          if (event === null) {
            void navigate({ href: `${EVENTS}/${row.id}` });
          } else {
            setSaved(true);
          }
        }}
      />
    </div>
  );

  return (
    <PageShell
      title={title}
      {...(event === null ? {} : { description: t(`events:events.options.state.${event.state}`) })}
      breadcrumb={[
        { label: t('events:nav.section') },
        { label: t('events:events.title'), to: EVENTS },
        { label: title },
      ]}
      actions={event === null ? undefined : <EventActions event={event} />}
    >
      <div className="flex flex-col gap-6">
        {event === null || event.cancelledAt === null ? null : (
          <Notice
            tone="warning"
            title={t('events:events.cancelled', { date: moment(event.cancelledAt, { time: false }) })}
            description={read(event.cancellationNote ?? {})}
          />
        )}
        {event === null ? (
          settings
        ) : (
          // The open tab is in the address, so a reload or a link lands on it; only the open tab is mounted.
          <Tabs
            // ⚠️ `w-full` is not decoration: Atmosphere pins `Tabs` to `w-[400px]`.
            className="w-full"
            value={tab}
            onValueChange={(next) => {
              setSaved(false);
              void navigate({
                search: ((previous: Record<string, unknown>) => ({ ...previous, tab: next })) as never,
                to: '.',
              });
            }}
            tabs={{
              settings: {
                trigger: t('events:events.tabs.settings'),
                content: <div className="pt-4">{settings}</div>,
              },
              // The description is written by whoever writes the event; the page of the event shows it to the others (E4).
              ...(editable
                ? {
                    description: {
                      trigger: t('events:events.tabs.description'),
                      content: (
                        <div className="pt-4">
                          <EventDescription event={event} />
                        </div>
                      ),
                    },
                  }
                : {}),
              // An event about the whole division has no airports of its own (§1.2).
              ...(event.wholeDivision
                ? {}
                : {
                    airports: {
                      trigger: t('events:events.tabs.airports'),
                      content: (
                        <div className="pt-4">
                          <AirportsTab event={event} editable={editable} />
                        </div>
                      ),
                    },
                  }),
            }}
          />
        )}
      </div>
    </PageShell>
  );
}

/** "Cancel" (§2.3): the note, in every language of the division, and back to the event. */
export function EventCancelPage() {
  const { t } = useTranslation();
  const read = useLocalized();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const navigate = useNavigate();
  const id = Number(useParams({ strict: false }).id);
  const locales = bootstrap.division.locales;

  const event = useQuery(eventQuery(id)).data;
  const cancel = useCancelEvent(id);

  if (event === undefined) {
    return null;
  }

  const title = t('events:cancel.title');
  const back = `${EVENTS}/${event.id}`;

  return (
    <PageShell
      title={title}
      description={t('events:cancel.description')}
      breadcrumb={[
        { label: t('events:nav.section') },
        { label: t('events:events.title'), to: EVENTS },
        { label: read(event.title) || t('events:events.edit'), to: back },
        { label: title },
      ]}
    >
      <SchemaForm
        schema={cancelSchema}
        defaults={{
          note: Object.fromEntries(locales.map((locale) => [locale, ''])),
          rowVersion: event.rowVersion,
        }}
        locales={locales}
        labels="events:cancel"
        onSubmit={async (values) => {
          await cancel.mutateAsync(values);
          void navigate({ href: back });
        }}
        submitLabel={t('events:cancel.submit')}
        secondaryAction={
          <Button asChild variant="ghost">
            <RouterAnchor href={back}>{t('common.cancel')}</RouterAnchor>
          </Button>
        }
      />
    </PageShell>
  );
}
