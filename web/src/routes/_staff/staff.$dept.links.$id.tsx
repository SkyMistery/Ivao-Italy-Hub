import { Button } from '@ivao/atmosphere-react';
import { Link, createFileRoute, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import {
  emptyLink,
  toFormValues,
  useCreateLink,
  useDeleteLink,
  useUpdateLink,
} from '../../features/links/mutations';
import { linkQuery, type LinkDetailDto } from '../../features/links/queries';
import { linkSchema, type LinkFormValues } from '../../features/links/schema';
import { deptParam } from '../../shared/api/department';
import { SchemaForm } from '../../shared/forms';
import { ConfirmDialog, PageShell } from '../../shared/ui';

/**
 * The form of one link, and `new` for one that does not exist yet.
 *
 * There is no field in this file. `SchemaForm` reads `features/links/schema.ts` and draws every
 * one of them, including the two language tabs of the title; the server's refusal reaches the right
 * field through `useProblemDetails`, which the generator wires up on its own (design M0 §7.5).
 */
export const Route = createFileRoute('/_staff/staff/$dept/links/$id')({
  // `$dept` is the parent route's parameter and is parsed there; this route only adds `$id`,
  // which is already the string the API addresses a row with.
  loader: async ({ context, params }): Promise<LinkDetailDto | null> =>
    params.id === 'new' ? null : context.queryClient.ensureQueryData(linkQuery(Number(params.id))),
  component: LinkForm,
});

function LinkForm() {
  const { t } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const { dept, id } = Route.useParams();
  const navigate = useNavigate();

  const isNew = id === 'new';
  const locales = bootstrap.division.locales;

  // The loader has already fetched it, so there is nothing to wait for and nothing to refetch.
  const link = Route.useLoaderData();

  const create = useCreateLink();
  const update = useUpdateLink(Number(id));
  const remove = useDeleteLink();

  const backToList = () => void navigate({ to: '/staff/$dept/links', params: { dept } });

  const submit = async (values: LinkFormValues) => {
    if (isNew) {
      await create.mutateAsync(values);
    } else {
      await update.mutateAsync(values);
    }
    backToList();
  };

  const defaults = link === null ? emptyLink(dept, locales) : toFormValues(link, locales);

  return (
    <PageShell
      title={isNew ? t('links.create') : t('links.edit')}
      breadcrumb={[
        { label: dept },
        { label: t('links.title'), to: `/staff/${deptParam.format(dept)}/links` },
        { label: isNew ? t('links.create') : t('links.edit') },
      ]}
      actions={
        isNew ? undefined : (
          <ConfirmDialog
            triggerText={t('common.delete')}
            title={t('links.delete.title')}
            description={t('links.delete.description')}
            confirmText={t('common.delete')}
            disabled={remove.isPending}
            onConfirm={() => remove.mutate(Number(id), { onSuccess: backToList })}
          />
        )
      }
    >
      <SchemaForm
        schema={linkSchema}
        defaults={defaults}
        locales={locales}
        labels="links"
        onSubmit={submit}
        submitLabel={t('common.save')}
        secondaryAction={
          <Button asChild variant="ghost">
            <Link to="/staff/$dept/links" params={{ dept }}>
              {t('common.cancel')}
            </Link>
          </Button>
        }
      />
    </PageShell>
  );
}
