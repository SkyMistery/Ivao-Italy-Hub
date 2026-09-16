import { Button } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { Link, createFileRoute, redirect, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import {
  awardToFormValues,
  emptyAward,
  useCreateAward,
  useDeleteAward,
  useUpdateAward,
} from '../../features/awards/mutations';
import { awardQuery, type AwardDetailDto } from '../../features/awards/queries';
import { awardSchema, type AwardFormValues } from '../../features/awards/schema';
import { useUploadMedia } from '../../features/media/mutations';
import { mediaPickerQuery } from '../../features/media/queries';
import { writableDepartments } from '../../shared/api/bootstrap';
import { DEPARTMENTS } from '../../shared/api/department';
import { SchemaForm } from '../../shared/forms';
import { ConfirmDialog, PageShell } from '../../shared/ui';

/** What writing an award asks for, on the department of the row. */
const AWARDS_EDIT = 'Awards.Edit';

/**
 * One award, and `new` for one that does not exist yet. No field is written here: `SchemaForm` draws
 * `features/awards/schema.ts` (design M0 §7.5). Deleting is for an award nobody holds; the server
 * refuses the rest on the field, and retiring it is what the form offers instead.
 */
export const Route = createFileRoute('/_staff/staff/awards/$id')({
  validateSearch: z.object({ department: z.enum(DEPARTMENTS).optional() }),
  beforeLoad: ({ context, params, search }) => {
    const writable = writableDepartments(context.bootstrap, AWARDS_EDIT);
    if (
      params.id === 'new' &&
      (search.department === undefined ? writable.length === 0 : !writable.includes(search.department))
    ) {
      throw redirect({ to: '/forbidden' });
    }
  },
  loader: async ({ context, params }): Promise<AwardDetailDto | null> =>
    params.id === 'new' ? null : context.queryClient.ensureQueryData(awardQuery(Number(params.id))),
  component: AwardForm,
});

function AwardForm() {
  const { t } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const { id } = Route.useParams();
  const search = Route.useSearch();
  const navigate = useNavigate();

  const isNew = id === 'new';
  const locales = bootstrap.division.locales;

  // The row as it stands now, not the loader's copy (design M0 §7.3).
  const award = useQuery({ ...awardQuery(Number(id)), enabled: !isNew }).data ?? null;

  const create = useCreateAward();
  const update = useUpdateAward(Number(id));
  const remove = useDeleteAward();
  const upload = useUploadMedia();

  const department =
    award?.ownerDepartment ?? search.department ?? writableDepartments(bootstrap, AWARDS_EDIT)[0];

  const backToList = () =>
    void navigate({ to: '/staff/awards', search: department === undefined ? {} : { department } });

  const submit = async (values: AwardFormValues) => {
    if (isNew) {
      await create.mutateAsync(values);
    } else {
      await update.mutateAsync(values);
    }
    backToList();
  };

  if ((!isNew && award === null) || department === undefined) {
    return null;
  }

  const title = isNew ? t('awards.create') : t('awards.edit');

  return (
    <PageShell
      title={title}
      breadcrumb={[
        { label: t('awards.section') },
        { label: t('awards.title'), to: `/staff/awards?department=${department}` },
        { label: title },
      ]}
      actions={
        isNew ? undefined : (
          <ConfirmDialog
            triggerText={t('common.delete')}
            title={t('awards.delete.title')}
            description={t('awards.delete.description')}
            confirmText={t('common.delete')}
            disabled={remove.isPending}
            onConfirm={() => remove.mutate(Number(id), { onSuccess: backToList })}
          />
        )
      }
    >
      <SchemaForm
        schema={awardSchema}
        defaults={award === null ? emptyAward(department, locales) : awardToFormValues(award, locales)}
        locales={locales}
        labels="awards"
        // The picture comes from the library of the award's department, and an upload lands there.
        mediaLibrary={mediaPickerQuery(department)}
        uploadMedia={async (file) =>
          (await upload.mutateAsync({ file, ownerDepartment: department })).media.id
        }
        division={{ defaultLocale: bootstrap.division.defaultLocale, timezone: bootstrap.division.timezone }}
        onSubmit={submit}
        submitLabel={t('common.save')}
        secondaryAction={
          <Button asChild variant="ghost">
            <Link to="/staff/awards" search={{ department }}>
              {t('common.cancel')}
            </Link>
          </Button>
        }
      />
    </PageShell>
  );
}
