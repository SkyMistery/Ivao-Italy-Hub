import { useQuery } from '@tanstack/react-query';
import { Button } from '@ivao/atmosphere-react';
import { Link, createFileRoute, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import {
  emptyGrant,
  toFormValues,
  useCreateGrant,
  useDeleteGrant,
  useUpdateGrant,
} from '../../features/admin/grants/mutations';
import { grantQuery, type GrantDetailDto } from '../../features/admin/grants/queries';
import { grantSchema, type GrantFormValues } from '../../features/admin/grants/schema';
import { SchemaForm } from '../../shared/forms';
import { ConfirmDialog, PageShell } from '../../shared/ui';

/**
 * One grant, and `new` for one that does not exist yet. No field is written here: the schema is
 * built from the bootstrap — the permissions on offer are the ones this installation was built
 * with — and `SchemaForm` draws it (design M0 §7.5).
 */
export const Route = createFileRoute('/_staff/staff/admin/permissions/$id')({
  loader: async ({ context, params }): Promise<GrantDetailDto | null> =>
    params.id === 'new' ? null : context.queryClient.ensureQueryData(grantQuery(Number(params.id))),
  component: GrantForm,
});

function GrantForm() {
  const { t } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const { id } = Route.useParams();
  const navigate = useNavigate();

  const isNew = id === 'new';
  // The row as it stands now. ⚠️ Not `Route.useLoaderData()`: a loader runs on navigation and
  // never again, so after one save the screen still held the `rowVersion` from when the page
  // opened, and the second save was answered 409 — blaming somebody who does not exist. The loader
  // above is the *preload*; what the screen reads is the query it filled (design M0 §7.3).
  const grant = useQuery({ ...grantQuery(Number(id)), enabled: id !== 'new' }).data ?? null;

  const create = useCreateGrant();
  const update = useUpdateGrant(Number(id));
  const remove = useDeleteGrant();

  const backToList = () => void navigate({ to: '/staff/admin/permissions' });

  const submit = async (values: GrantFormValues) => {
    if (isNew) {
      await create.mutateAsync(values);
    } else {
      await update.mutateAsync(values);
    }
    backToList();
  };

  return (
    <PageShell
      title={isNew ? t('grants.create') : t('grants.edit')}
      description={t('grants.formHint')}
      breadcrumb={[
        { label: t('admin.title') },
        { label: t('grants.title'), to: '/staff/admin/permissions' },
        { label: isNew ? t('grants.create') : t('grants.edit') },
      ]}
      actions={
        isNew ? undefined : (
          <ConfirmDialog
            triggerText={t('common.delete')}
            title={t('grants.delete.title')}
            description={t('grants.delete.description')}
            confirmText={t('common.delete')}
            disabled={remove.isPending}
            onConfirm={() => remove.mutate(Number(id), { onSuccess: backToList })}
          />
        )
      }
    >
      <SchemaForm
        schema={grantSchema(bootstrap)}
        division={{
          defaultLocale: bootstrap.division.defaultLocale,
          timezone: bootstrap.division.timezone,
        }}
        defaults={grant === null ? emptyGrant() : toFormValues(grant)}
        locales={bootstrap.division.locales}
        labels="grants"
        onSubmit={submit}
        submitLabel={t('common.save')}
        secondaryAction={
          <Button asChild variant="ghost">
            <Link to="/staff/admin/permissions">{t('common.cancel')}</Link>
          </Button>
        }
      />
    </PageShell>
  );
}
