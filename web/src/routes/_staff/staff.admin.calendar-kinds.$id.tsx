import { Button } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { Link, createFileRoute, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import {
  emptyCalendarKind,
  toFormValues,
  useCreateCalendarKind,
  useDeleteCalendarKind,
  useUpdateCalendarKind,
} from '../../features/admin/calendarKinds/mutations';
import { calendarKindQuery, type CalendarKindDetailDto } from '../../features/admin/calendarKinds/queries';
import { calendarKindSchema, type CalendarKindFormValues } from '../../features/admin/calendarKinds/schema';
import { SchemaForm } from '../../shared/forms';
import { ConfirmDialog, Notice, PageShell } from '../../shared/ui';

/**
 * One word of the division's calendar vocabulary, and `new` for one that does not exist yet. No
 * field is written here: `SchemaForm` draws the schema (design M0 §7.5).
 *
 * ⚠️ Deleting one is the thing worth being careful about, and the screen says so: the entries
 * already written with that key keep it, and nothing rewrites them — there is no foreign key, on
 * purpose. Retiring a word with `isActive` is almost always what somebody means, and it leaves the
 * entries reading the way they were written.
 */
export const Route = createFileRoute('/_staff/staff/admin/calendar-kinds/$id')({
  loader: async ({ context, params }): Promise<CalendarKindDetailDto | null> =>
    params.id === 'new' ? null : context.queryClient.ensureQueryData(calendarKindQuery(Number(params.id))),
  component: CalendarKindForm,
});

function CalendarKindForm() {
  const { t } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const { id } = Route.useParams();
  const navigate = useNavigate();

  const isNew = id === 'new';
  const locales = bootstrap.division.locales;

  // The row as it stands now, and not `Route.useLoaderData()`: a loader runs on navigation and
  // never again, so the screen would keep answering with the `rowVersion` it opened with and the
  // second save of a page load would be answered 409 (design M0 §7.3).
  const kind = useQuery({ ...calendarKindQuery(Number(id)), enabled: !isNew }).data ?? null;

  const create = useCreateCalendarKind();
  const update = useUpdateCalendarKind(Number(id));
  const remove = useDeleteCalendarKind();

  const backToList = () => void navigate({ to: '/staff/admin/calendar-kinds' });

  const submit = async (values: CalendarKindFormValues) => {
    if (isNew) {
      await create.mutateAsync(values);
    } else {
      await update.mutateAsync(values);
    }
    backToList();
  };

  const title = isNew ? t('calendarKinds.create') : t('calendarKinds.edit');

  return (
    <PageShell
      title={title}
      breadcrumb={[
        { label: t('admin.title') },
        { label: t('calendarKinds.title'), to: '/staff/admin/calendar-kinds' },
        { label: title },
      ]}
      actions={
        isNew ? undefined : (
          <ConfirmDialog
            triggerText={t('common.delete')}
            title={t('calendarKinds.delete.title')}
            description={t('calendarKinds.delete.description')}
            confirmText={t('common.delete')}
            disabled={remove.isPending}
            onConfirm={() => remove.mutate(Number(id), { onSuccess: backToList })}
          />
        )
      }
    >
      <div className="flex flex-col gap-6">
        <Notice tone="info" title={t('calendarKinds.formHint')} />

        <SchemaForm
          schema={calendarKindSchema}
          defaults={kind === null ? emptyCalendarKind(locales) : toFormValues(kind, locales)}
          locales={locales}
          labels="calendarKinds"
          division={{
            defaultLocale: bootstrap.division.defaultLocale,
            timezone: bootstrap.division.timezone,
          }}
          onSubmit={submit}
          submitLabel={t('common.save')}
          secondaryAction={
            <Button asChild variant="ghost">
              <Link to="/staff/admin/calendar-kinds">{t('common.cancel')}</Link>
            </Button>
          }
        />
      </div>
    </PageShell>
  );
}
