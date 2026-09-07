import { useQuery } from '@tanstack/react-query';
import { Button } from '@ivao/atmosphere-react';
import { Link, createFileRoute, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import {
  emptyCalendarEntry,
  toFormValues,
  useCreateCalendarEntry,
  useDeleteCalendarEntry,
  useUpdateCalendarEntry,
} from '../../features/calendar/mutations';
import { calendarEntryQuery, type CalendarDetailDto } from '../../features/calendar/queries';
import { calendarSchema, type CalendarFormValues } from '../../features/calendar/schema';
import { deptParam } from '../../shared/api/department';
import { ProblemAlert, SchemaForm } from '../../shared/forms';
import { ConfirmDialog, PageShell } from '../../shared/ui';

/**
 * The form of one calendar entry, and `new` for one that does not exist yet.
 *
 * ⚠️ A **projected** entry is read only, and this screen says so instead of drawing a form that
 * cannot be saved: the row belongs to the module that owns the thing it mirrors, and editing it
 * would be undone at that module's next save. The engine refuses the write on the same answer this
 * screen draws — `isProjection`, decided once on the entity (design M1 §4).
 */
export const Route = createFileRoute('/_staff/staff/$dept/calendar/$id')({
  loader: async ({ context, params }): Promise<CalendarDetailDto | null> =>
    params.id === 'new' ? null : context.queryClient.ensureQueryData(calendarEntryQuery(Number(params.id))),
  component: CalendarEntryForm,
});

function CalendarEntryForm() {
  const { t } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const { dept, id } = Route.useParams();
  const navigate = useNavigate();

  const isNew = id === 'new';
  const locales = bootstrap.division.locales;
  // The row as it stands now. ⚠️ Not `Route.useLoaderData()`: a loader runs on navigation and
  // never again, so after one save the screen still held the `rowVersion` from when the page
  // opened, and the second save was answered 409 — blaming somebody who does not exist. The loader
  // above is the *preload*; what the screen reads is the query it filled (design M0 §7.3).
  const entry = useQuery({ ...calendarEntryQuery(Number(id)), enabled: id !== 'new' }).data ?? null;

  const create = useCreateCalendarEntry();
  const update = useUpdateCalendarEntry(Number(id));
  const remove = useDeleteCalendarEntry();

  const backToList = () => void navigate({ to: '/staff/$dept/calendar', params: { dept } });

  const submit = async (values: CalendarFormValues) => {
    if (isNew) {
      await create.mutateAsync(values);
    } else {
      await update.mutateAsync(values);
    }
    backToList();
  };

  const title = isNew ? t('calendar.create') : t('calendar.edit');
  const readOnly = entry?.isProjection === true;

  return (
    <PageShell
      title={title}
      breadcrumb={[
        { label: dept },
        { label: t('calendar.title'), to: `/staff/${deptParam.format(dept)}/calendar` },
        { label: title },
      ]}
      actions={
        isNew || readOnly ? undefined : (
          <ConfirmDialog
            triggerText={t('common.delete')}
            title={t('calendar.delete.title')}
            description={t('calendar.delete.description')}
            confirmText={t('common.delete')}
            disabled={remove.isPending}
            onConfirm={() => remove.mutate(Number(id), { onSuccess: backToList })}
          />
        )
      }
    >
      {readOnly ? (
        <div className="flex flex-col gap-6">
          {/* Said in a sentence, with the module named, rather than shown as a form whose save
              button answers 403: a disabled control without an explanation produces tickets. */}
          <ProblemAlert summary={t('calendar.projectedExplained', { module: entry.sourceModule })} />

          <Button asChild variant="ghost" className="self-start">
            <Link to="/staff/$dept/calendar" params={{ dept }}>
              {t('common.cancel')}
            </Link>
          </Button>
        </div>
      ) : (
        <SchemaForm
          schema={calendarSchema}
          defaults={
            entry === null ? emptyCalendarEntry(dept, locales, new Date()) : toFormValues(entry, locales)
          }
          locales={locales}
          labels="calendar"
          division={{
            defaultLocale: bootstrap.division.defaultLocale,
            timezone: bootstrap.division.timezone,
          }}
          onSubmit={submit}
          submitLabel={t('common.save')}
          secondaryAction={
            <Button asChild variant="ghost">
              <Link to="/staff/$dept/calendar" params={{ dept }}>
                {t('common.cancel')}
              </Link>
            </Button>
          }
        />
      )}
    </PageShell>
  );
}
