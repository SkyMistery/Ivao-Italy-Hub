import { Button } from '@ivao/atmosphere-react';
import { Link, createFileRoute, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import {
  emptyCategory,
  toFormValues,
  useCreateCategory,
  useDeleteCategory,
  useUpdateCategory,
} from '../../features/categories/mutations';
import { categoryQuery, type CategoryDetailDto } from '../../features/categories/queries';
import { categorySchema, type CategoryFormValues } from '../../features/categories/schema';
import { deptParam } from '../../shared/api/department';
import { SchemaForm } from '../../shared/forms';
import { ConfirmDialog, PageShell } from '../../shared/ui';

/**
 * The form of one shelf, and `new` for one that does not exist yet.
 *
 * There is no field in this file: `SchemaForm` reads `features/categories/schema.ts` and draws
 * every one of them, the language tabs of the label included (design M0 §7.5).
 */
export const Route = createFileRoute('/_staff/staff/$dept/categories/$id')({
  loader: async ({ context, params }): Promise<CategoryDetailDto | null> =>
    params.id === 'new' ? null : context.queryClient.ensureQueryData(categoryQuery(Number(params.id))),
  component: CategoryForm,
});

function CategoryForm() {
  const { t } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const { dept, id } = Route.useParams();
  const navigate = useNavigate();

  const isNew = id === 'new';
  const locales = bootstrap.division.locales;
  const category = Route.useLoaderData();

  const create = useCreateCategory();
  const update = useUpdateCategory(Number(id));
  const remove = useDeleteCategory();

  const backToList = () => void navigate({ to: '/staff/$dept/categories', params: { dept } });

  const submit = async (values: CategoryFormValues) => {
    if (isNew) {
      await create.mutateAsync(values);
    } else {
      await update.mutateAsync(values);
    }
    backToList();
  };

  return (
    <PageShell
      title={isNew ? t('categories.create') : t('categories.edit')}
      breadcrumb={[
        { label: dept },
        { label: t('categories.title'), to: `/staff/${deptParam.format(dept)}/categories` },
        { label: isNew ? t('categories.create') : t('categories.edit') },
      ]}
      actions={
        isNew ? undefined : (
          <ConfirmDialog
            triggerText={t('common.delete')}
            title={t('categories.delete.title')}
            description={t('categories.delete.description')}
            confirmText={t('common.delete')}
            disabled={remove.isPending}
            onConfirm={() => remove.mutate(Number(id), { onSuccess: backToList })}
          />
        )
      }
    >
      <SchemaForm
        schema={categorySchema}
        defaults={category === null ? emptyCategory(dept, locales) : toFormValues(category, locales)}
        locales={locales}
        labels="categories"
        onSubmit={submit}
        submitLabel={t('common.save')}
        secondaryAction={
          <Button asChild variant="ghost">
            <Link to="/staff/$dept/categories" params={{ dept }}>
              {t('common.cancel')}
            </Link>
          </Button>
        }
      />
    </PageShell>
  );
}
