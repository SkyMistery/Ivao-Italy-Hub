import { Button } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { Link, createFileRoute, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import {
  emptyMenuItem,
  toFormValues,
  useCreateMenuItem,
  useDeleteMenuItem,
  useUpdateMenuItem,
} from '../../features/menu/mutations';
import { menuItemQuery, menuParentsQuery, type MenuItemDetailDto } from '../../features/menu/queries';
import { menuItemSchema, type MenuItemFormValues } from '../../features/menu/schema';
import { deptParam } from '../../shared/api/department';
import { SchemaForm, type ChoiceOption } from '../../shared/forms';
import { useLocalized } from '../../shared/i18n/useLocalized';
import { ConfirmDialog, PageShell } from '../../shared/ui';

/**
 * One entry of the site menu, and `new` for one that does not exist yet.
 *
 * There is no field in this file: `SchemaForm` reads `features/menu/schema.ts` and draws every one
 * of them, the language tabs of the label included (design M0 §7.5). What this file adds is the one
 * thing a schema cannot hold — the entries this one may hang under, which are rows.
 */
export const Route = createFileRoute('/_staff/staff/$dept/menu/$id')({
  loader: async ({ context, params }): Promise<MenuItemDetailDto | null> =>
    params.id === 'new' ? null : context.queryClient.ensureQueryData(menuItemQuery(Number(params.id))),
  component: MenuItemForm,
});

function MenuItemForm() {
  const { t } = useTranslation();
  const read = useLocalized();
  const { bootstrap } = Route.useRouteContext();
  const { dept, id } = Route.useParams();
  const navigate = useNavigate();

  const isNew = id === 'new';
  const locales = bootstrap.division.locales;
  const item = Route.useLoaderData();

  const defaults = item === null ? emptyMenuItem(locales) : toFormValues(item, locales);

  // The entries this one may hang under: the top level of the menu it is in, and not itself.
  //
  // ⚠️ Read from the menu the row is in *now*, not from the one the select is showing: the
  // generator draws a schema and does not report a value while somebody is typing, and teaching it
  // to would be an extension of the form generator for one screen. Somebody who switches menu and
  // then picks a parent of the other one is refused by the server, on the field, with the message
  // `MenuItemWriteDtoValidator` gives — which is the same answer, one round trip later.
  const siblings = useQuery(menuParentsQuery(defaults.scope));

  const parents: ChoiceOption[] = (siblings.data?.items ?? [])
    // Depth is one: only a top level entry may be a parent, and an entry is never its own.
    .filter((row) => row.parentId === null && String(row.id) !== id)
    .map((row) => ({ value: String(row.id), label: read(row.label) || row.path }));

  const create = useCreateMenuItem();
  const update = useUpdateMenuItem(Number(id));
  const remove = useDeleteMenuItem();

  const backToList = () => void navigate({ to: '/staff/$dept/menu', params: { dept } });

  const submit = async (values: MenuItemFormValues) => {
    if (isNew) {
      await create.mutateAsync(values);
    } else {
      await update.mutateAsync(values);
    }
    backToList();
  };

  return (
    <PageShell
      title={isNew ? t('menu.create') : t('menu.edit')}
      breadcrumb={[
        { label: dept },
        { label: t('menu.title'), to: `/staff/${deptParam.format(dept)}/menu` },
        { label: isNew ? t('menu.create') : t('menu.edit') },
      ]}
      actions={
        isNew ? undefined : (
          <ConfirmDialog
            triggerText={t('common.delete')}
            title={t('menu.delete.title')}
            description={t('menu.delete.description')}
            confirmText={t('common.delete')}
            disabled={remove.isPending}
            onConfirm={() => remove.mutate(Number(id), { onSuccess: backToList })}
          />
        )
      }
    >
      <SchemaForm
        schema={menuItemSchema(parents)}
        defaults={defaults}
        locales={locales}
        labels="menu"
        onSubmit={submit}
        submitLabel={t('common.save')}
        secondaryAction={
          <Button asChild variant="ghost">
            <Link to="/staff/$dept/menu" params={{ dept }}>
              {t('common.cancel')}
            </Link>
          </Button>
        }
      />
    </PageShell>
  );
}
