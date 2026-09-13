import { useQuery } from '@tanstack/react-query';
import { Button } from '@ivao/atmosphere-react';
import { Link, createFileRoute, redirect, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import {
  emptyLink,
  toFormValues,
  useCreateLink,
  useDeleteLink,
  useUpdateLink,
} from '../../features/links/mutations';
import { linkQuery, type LinkDetailDto } from '../../features/links/queries';
import { linkSchema, type LinkFormValues } from '../../features/links/schema';
import { writableDepartments } from '../../shared/api/bootstrap';
import { DEPARTMENTS } from '../../shared/api/department';
import { SchemaForm } from '../../shared/forms';
import { ConfirmDialog, PageShell } from '../../shared/ui';

/** What writing a link asks for, on the department of the row. */
const LINKS_EDIT = 'Links.Edit';

/**
 * The form of one link, and `new` for one that does not exist yet.
 *
 * There is no field in this file. `SchemaForm` reads `features/links/schema.ts` and draws every
 * one of them, including the two language tabs of the title; the server's refusal reaches the right
 * field through `useProblemDetails`, which the generator wires up on its own (design M0 §7.5).
 */
export const Route = createFileRoute('/_staff/staff/links/$id')({
  // Which department a new link goes to, chosen on the list. An existing link says so itself.
  validateSearch: z.object({ department: z.enum(DEPARTMENTS).optional() }),
  beforeLoad: ({ context, params, search }) => {
    const writable = writableDepartments(context.bootstrap, LINKS_EDIT);
    if (
      params.id === 'new' &&
      (search.department === undefined ? writable.length === 0 : !writable.includes(search.department))
    ) {
      throw redirect({ to: '/forbidden' });
    }
  },
  loader: async ({ context, params }): Promise<LinkDetailDto | null> =>
    params.id === 'new' ? null : context.queryClient.ensureQueryData(linkQuery(Number(params.id))),
  component: LinkForm,
});

function LinkForm() {
  const { t } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const { id } = Route.useParams();
  const search = Route.useSearch();
  const navigate = useNavigate();

  const isNew = id === 'new';
  const locales = bootstrap.division.locales;

  // The row as it stands now. ⚠️ Not `Route.useLoaderData()`: a loader runs on navigation and
  // never again, so after one save the screen still held the `rowVersion` from when the page
  // opened, and the second save was answered 409 — blaming somebody who does not exist. The loader
  // above is the *preload*; what the screen reads is the query it filled (design M0 §7.3).
  const link = useQuery({ ...linkQuery(Number(id)), enabled: id !== 'new' }).data ?? null;

  const create = useCreateLink();
  const update = useUpdateLink(Number(id));
  const remove = useDeleteLink();

  const department =
    link?.ownerDepartment ?? search.department ?? writableDepartments(bootstrap, LINKS_EDIT)[0];

  const backToList = () =>
    void navigate({ to: '/staff/links', search: department === undefined ? {} : { department } });

  const submit = async (values: LinkFormValues) => {
    if (isNew) {
      await create.mutateAsync(values);
    } else {
      await update.mutateAsync(values);
    }
    backToList();
  };

  if (!isNew && link === null) {
    // Still loading, which the loader makes a state nobody sees.
    return null;
  }

  if (department === undefined) {
    return null;
  }

  const defaults = link === null ? emptyLink(department, locales) : toFormValues(link, locales);

  return (
    <PageShell
      title={isNew ? t('links.create') : t('links.edit')}
      breadcrumb={[
        { label: t(`departments.${department}`) },
        { label: t('links.title'), to: `/staff/links?department=${department}` },
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
            <Link to="/staff/links" search={{ department }}>
              {t('common.cancel')}
            </Link>
          </Button>
        }
      />
    </PageShell>
  );
}
