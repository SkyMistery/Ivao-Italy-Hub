import { Button } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { Link, createFileRoute, redirect, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import {
  assignmentToFormValues,
  emptyAssignment,
  useCreateAssignment,
  useDeleteAssignment,
  useUpdateAssignment,
} from '../../features/awards/mutations';
import {
  activeAwardsQuery,
  awardAssignmentQuery,
  awardSignalQuery,
  type AwardAssignmentDetailDto,
} from '../../features/awards/queries';
import { awardAssignmentSchema, type AwardAssignmentFormValues } from '../../features/awards/schema';
import { holdsPermissionAnywhere } from '../../shared/api/bootstrap';
import { SchemaForm, type ChoiceOption } from '../../shared/forms';
import { useLocalized } from '../../shared/i18n/useLocalized';
import { ConfirmDialog, Notice, PageShell } from '../../shared/ui';

/** Global: the register belongs to whoever assigns. */
const AWARDS_ASSIGN = 'Awards.Assign';

/**
 * One assignment, and `new` for one that does not exist yet — filled from a line of the queue when the
 * address names one (`?signal=`). The form is `SchemaForm`; what is checked against other rows (the
 * award still active, the line still waiting and about the same member) is the server's, on the field.
 * Deleting revokes: the row goes and the audit log keeps it.
 */
export const Route = createFileRoute('/_staff/staff/awards/assignments/$id')({
  validateSearch: z.object({ signal: z.number().int().optional() }),
  beforeLoad: ({ context }) => {
    if (!holdsPermissionAnywhere(context.bootstrap, AWARDS_ASSIGN)) {
      throw redirect({ to: '/forbidden' });
    }
  },
  loaderDeps: ({ search }) => ({ signal: search.signal }),
  loader: async ({ context, params, deps }): Promise<AwardAssignmentDetailDto | null> => {
    await context.queryClient.ensureQueryData(activeAwardsQuery());
    if (params.id === 'new') {
      if (deps.signal !== undefined) {
        await context.queryClient.ensureQueryData(awardSignalQuery(deps.signal));
      }
      return null;
    }
    return context.queryClient.ensureQueryData(awardAssignmentQuery(Number(params.id)));
  },
  component: AwardAssignmentForm,
});

function AwardAssignmentForm() {
  const { t } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const { id } = Route.useParams();
  const search = Route.useSearch();
  const navigate = useNavigate();
  const read = useLocalized();

  const isNew = id === 'new';

  const assignment = useQuery({ ...awardAssignmentQuery(Number(id)), enabled: !isNew }).data ?? null;
  const signal =
    useQuery({ ...awardSignalQuery(search.signal ?? 0), enabled: isNew && search.signal !== undefined })
      .data ?? null;
  const active = useQuery(activeAwardsQuery()).data?.items ?? [];

  const awards: ChoiceOption[] = active.map((award) => ({
    value: String(award.id),
    label: read(award.name),
  }));

  const create = useCreateAssignment();
  const update = useUpdateAssignment(Number(id));
  const remove = useDeleteAssignment();

  // Back where the assignment came from: the queue when it answered a line, the register otherwise.
  const back = search.signal === undefined ? '/staff/awards/assignments' : '/staff/awards/queue';
  const goBack = () => void navigate({ to: back });

  const submit = async (values: AwardAssignmentFormValues) => {
    if (isNew) {
      await create.mutateAsync(values);
    } else {
      await update.mutateAsync(values);
    }
    goBack();
  };

  if ((!isNew && assignment === null) || (isNew && search.signal !== undefined && signal === null)) {
    return null;
  }

  const title = isNew ? t('awardAssignments.create') : t('awardAssignments.edit');

  return (
    <PageShell
      title={title}
      breadcrumb={[
        { label: t('awards.section') },
        {
          label: search.signal === undefined ? t('awardAssignments.title') : t('awardSignals.title'),
          to: back,
        },
        { label: title },
      ]}
      actions={
        isNew ? undefined : (
          <ConfirmDialog
            triggerText={t('awardAssignments.revoke')}
            title={t('awardAssignments.delete.title')}
            description={t('awardAssignments.delete.description')}
            confirmText={t('awardAssignments.revoke')}
            disabled={remove.isPending}
            onConfirm={() => remove.mutate(Number(id), { onSuccess: goBack })}
          />
        )
      }
    >
      <div className="flex flex-col gap-6">
        <Notice tone="info" title={t('awardAssignments.formHint')} />

        <SchemaForm
          schema={awardAssignmentSchema(awards)}
          defaults={assignment === null ? emptyAssignment(signal) : assignmentToFormValues(assignment)}
          locales={bootstrap.division.locales}
          labels="awardAssignments"
          onSubmit={submit}
          submitLabel={isNew ? t('awardAssignments.assign') : t('common.save')}
          secondaryAction={
            <Button asChild variant="ghost">
              <Link to={back}>{t('common.cancel')}</Link>
            </Button>
          }
        />
      </div>
    </PageShell>
  );
}
