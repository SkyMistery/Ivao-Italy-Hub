import { Button } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate, useParams, useRouteContext, useSearch } from '@tanstack/react-router';
import { Plus } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { holdsPermissionAnywhere } from '../../../shared/api/bootstrap';
import { SchemaForm, describeProblem, type Suggestion } from '../../../shared/forms';
import { DataList, ListFilter, col, type ColumnSpec } from '../../../shared/list';
import { ConfirmDialog, NotFound, Notice, PageShell } from '../../../shared/ui';
import {
  examChoicesQuery,
  examQuery,
  examsListQuery,
  memberLabel,
  useDeleteExam,
  useSaveExam,
  type ExamRow,
} from '../api';
import { TRAINING_MANAGE_EXAMS } from '../permissions';
import { emptyExam, examSchema, examToFormValues, examsSearchSchema, type ExamsSearch } from '../schemas';

import { positionLabel, ratingOptions } from './ratings';

/**
 * The exams in the calendar (design M3 §1.5, §2.8, §4.2), over the server of A10c: the generated list and the generated form. Whoever
 * does training reads them; whoever examines — the coordinator, the assistant and the advisors of the department, and the direction —
 * enters them. An advisor changes and takes off the calendar only the exams assigned to them, and whoever edits the area every one:
 * each row says whether it is the reader's and whether they may change it (`mine`, `mayEdit`), which is the server's answer, so the
 * page offers no step the server would refuse. Of the candidate and of the examiner, the VID and nothing else.
 */

export const EXAMS = '/staff/training/exams';

const columns: readonly ColumnSpec<ExamRow>[] = [
  col.date('startsAtUtc', { sortable: true }),
  col.text('ratingShortName'),
  col.text('position'),
  col.text('candidate'),
  col.text('examiner'),
  col.badge('whose', 'training:exams'),
];

function NewExamButton() {
  const { t } = useTranslation();

  return (
    <Button asChild>
      <RouterAnchor href={`${EXAMS}/new`}>
        <Plus aria-hidden className="mr-2 size-4" />
        {t('training:exams.create')}
      </RouterAnchor>
    </Button>
  );
}

export function ExamsPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const navigate = useNavigate();
  const search = examsSearchSchema.parse(useSearch({ strict: false }));
  const create = holdsPermissionAnywhere(bootstrap, TRAINING_MANAGE_EXAMS) ? <NewExamButton /> : null;

  const onSearchChange = (patch: Partial<ExamsSearch>) =>
    void navigate({
      search: ((previous: ExamsSearch) => ({ ...previous, ...patch })) as never,
      to: '.',
    });

  return (
    <PageShell
      title={t('training:exams.title')}
      description={t('training:exams.description')}
      breadcrumb={[{ label: t('training:nav.section') }, { label: t('training:exams.title') }]}
      actions={create ?? undefined}
    >
      <DataList
        columns={columns}
        query={examsListQuery(search, bootstrap.user?.vid)}
        labels="training:exams"
        locale={i18n.language}
        defaultLocale={bootstrap.division.defaultLocale}
        timezone={bootstrap.division.timezone}
        search={search}
        onSearchChange={onSearchChange}
        toolbar={
          <ListFilter
            id="exams-examiner"
            label={t('training:exams.filters.examiner')}
            none={t('training:exams.filters.anyExaminer')}
            value={search.mine === true ? 'mine' : undefined}
            onChange={(value) => onSearchChange({ mine: value === 'mine' ? true : undefined, page: 1 })}
            items={[{ value: 'mine', label: t('training:exams.filters.mine') }]}
          />
        }
        // A step is offered only on an exam the server says the reader may change: an advisor's own, or every one for whoever
        // edits the area.
        actions={(row) =>
          row.mayEdit ? (
            <Button asChild variant="ghost" size="sm">
              <RouterAnchor href={`${EXAMS}/${String(row.id)}`}>{t('common.edit')}</RouterAnchor>
            </Button>
          ) : null
        }
        {...(create === null ? {} : { emptyAction: create })}
      />
    </PageShell>
  );
}

/**
 * A new exam, or one to change (§2.8): the rating — any of the path, as the server offers them, the ones nobody trains for too
 * (#178) —, the position of an exam on one, when, the candidate by VID, and the examiner among the ones the server offers — an advisor
 * only themselves, whoever edits the area every examiner the hub knows. Taking an exam off the calendar is asked first; the exam itself
 * stays the network's.
 */
export function ExamForm() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const navigate = useNavigate();
  const id = String(useParams({ strict: false }).id ?? 'new');
  const isNew = id === 'new';
  const writes = holdsPermissionAnywhere(bootstrap, TRAINING_MANAGE_EXAMS);

  const exam = useQuery({ ...examQuery(Number(id)), enabled: writes && !isNew }).data ?? null;
  const choices = useQuery({ ...examChoicesQuery(), enabled: writes }).data;
  const save = useSaveExam(isNew ? null : Number(id));
  const remove = useDeleteExam();

  if (!writes) {
    return <NotFound />;
  }

  if ((!isNew && exam === null) || choices === undefined) {
    return null;
  }

  const reader = bootstrap.user?.vid;
  const title = isNew ? t('training:exams.create') : t('training:exams.edit');
  const refusal = describeProblem(remove.error, t, i18n.language);

  return (
    <PageShell
      title={title}
      description={t('training:exams.formDescription')}
      breadcrumb={[
        { label: t('training:nav.section') },
        { label: t('training:exams.title'), to: EXAMS },
        { label: title },
      ]}
      actions={
        exam === null ? undefined : (
          <ConfirmDialog
            triggerText={t('common.delete')}
            title={t('training:exams.delete.title')}
            description={t('training:exams.delete.description')}
            confirmText={t('common.delete')}
            disabled={remove.isPending}
            onConfirm={() => remove.mutate(exam.id, { onSuccess: () => void navigate({ href: EXAMS }) })}
          />
        )
      }
    >
      <div className="flex flex-col gap-6">
        {refusal === null ? null : <Notice tone="error" title={refusal} />}
        <SchemaForm
          // Keyed on the version: an exam somebody else saved since the cache was filled is drawn again when the row arrives,
          // rather than sent back stale and answered 409.
          key={exam?.rowVersion ?? 'new'}
          schema={examSchema({
            ratings: ratingOptions(choices.ratings, t),
            positions: choices.positions.map((position): Suggestion => ({
              value: position.callsign,
              label: positionLabel(position, t),
              group: position.ratingShortName,
            })),
            examiners: choices.examiners.map((member) => ({
              value: String(member.vid),
              label: memberLabel(member),
            })),
          })}
          defaults={
            exam === null
              ? emptyExam(choices.examiners.some((member) => member.vid === reader) ? reader : undefined)
              : examToFormValues(exam)
          }
          locales={bootstrap.division.locales}
          labels="training:exams"
          division={{
            defaultLocale: bootstrap.division.defaultLocale,
            timezone: bootstrap.division.timezone,
          }}
          onSubmit={async (values) => {
            await save.mutateAsync(values);
            await navigate({ href: EXAMS });
          }}
          submitLabel={t('common.save')}
          secondaryAction={
            <Button asChild variant="ghost">
              <RouterAnchor href={EXAMS}>{t('common.cancel')}</RouterAnchor>
            </Button>
          }
        />
      </div>
    </PageShell>
  );
}
