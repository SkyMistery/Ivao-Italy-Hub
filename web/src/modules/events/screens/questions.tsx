import { Button } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate, useParams, useRouteContext } from '@tanstack/react-router';
import { Plus } from 'lucide-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { SchemaForm, describeProblem } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { DataList, col, listSearchSchema, type ColumnSpec, type ListSearch } from '../../../shared/list';
import { ConfirmDialog, Notice, PageShell } from '../../../shared/ui';
import {
  eventQuery,
  questionQuery,
  questionRowsQuery,
  questionToFormValues,
  questionsQuery,
  useDeleteQuestion,
  useSaveQuestion,
  type EventDetailDto,
  type QuestionRow,
} from '../api';
import { emptyQuestion, questionSchema } from '../schemas';

import { sumWords, useAnswerWords } from './answers';

/**
 * The questions of an event in person (design M4 §4-bis, §7.2, E16): a tab of the event's page with the generated list — in the order
 * of the member's form, with what the registrations answered so far, summed (the statistics of the event, read with the event: the
 * server counts, the page writes the words in the language on screen) —, each
 * question opened in a generated form on a page of its own, as the routes are. Whoever writes the event writes its questions; what a
 * question somebody answered still allows is the server's to say.
 */

const EVENTS = '/staff/events';

const columns: readonly ColumnSpec<QuestionRow>[] = [
  col.number('ordinal', { sortable: true }),
  col.localized('label'),
  col.badge('type', 'events:questions'),
  col.boolean('required'),
  col.list('sums'),
];

export function QuestionsTab({ event, editable }: { event: EventDetailDto; editable: boolean }) {
  const { t, i18n } = useTranslation();
  const read = useLocalized();
  const words = useAnswerWords();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const [search, setSearch] = useState<ListSearch>(() => listSearchSchema.parse({ pageSize: 100 }));
  const base = `${EVENTS}/${event.id}`;

  const create = editable ? (
    <Button asChild>
      <RouterAnchor href={`${base}/questions/new`}>
        <Plus aria-hidden className="mr-2 size-4" />
        {t('events:questions.create')}
      </RouterAnchor>
    </Button>
  ) : null;

  return (
    <DataList
      columns={columns}
      // The sums in words of the language on screen (Carmine's answer 6 on #255).
      query={questionRowsQuery(event.id, search, i18n.language, (question) =>
        sumWords(question, question.sum, read, words),
      )}
      labels="events:questions"
      locale={i18n.language}
      defaultLocale={bootstrap.division.defaultLocale}
      timezone={bootstrap.division.timezone}
      search={search}
      onSearchChange={(patch) => setSearch((current) => ({ ...current, ...patch }))}
      {...(create === null ? {} : { toolbar: create })}
      actions={(row) =>
        editable ? (
          <Button asChild variant="ghost" size="sm">
            <RouterAnchor href={`${base}/questions/${row.id}`}>{t('common.edit')}</RouterAnchor>
          </Button>
        ) : null
      }
    />
  );
}

/** One question of an event: the event in the breadcrumb, the generated form, "delete" for one that exists, and back to the tab. */
export function QuestionForm() {
  const { t, i18n } = useTranslation();
  const read = useLocalized();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const navigate = useNavigate();
  // The routes of a module are registered from its manifest, so their parameters are not in the router's typed tree.
  const params: Readonly<Record<string, string | undefined>> = useParams({ strict: false });
  const eventId = Number(params.id);
  const id = (params.questionId ?? 'new') === 'new' ? null : Number(params.questionId);
  const locales = bootstrap.division.locales;

  const event = useQuery(eventQuery(eventId)).data;
  // A new question goes after the others: the list says how many there are.
  const others = useQuery({ ...questionsQuery(eventId), enabled: id === null }).data;
  const question = useQuery({ ...questionQuery(id ?? 0), enabled: id !== null }).data ?? null;
  const save = useSaveQuestion(id);
  const remove = useDeleteQuestion();

  if (event === undefined || (id !== null && question === null) || (id === null && others === undefined)) {
    return null;
  }

  const back = `${EVENTS}/${eventId}?tab=questions`;
  const title = t(id === null ? 'events:questions.create' : 'events:questions.edit');
  const refusal = describeProblem(remove.error, t, i18n.language);
  const next = Math.max(0, ...(others?.items ?? []).map((row) => row.ordinal)) + 1;

  return (
    <PageShell
      title={title}
      breadcrumb={[
        { label: t('events:nav.section') },
        { label: t('events:events.title'), to: EVENTS },
        { label: read(event.title) || t('events:events.edit'), to: back },
        { label: title },
      ]}
      actions={
        question === null ? undefined : (
          <ConfirmDialog
            triggerText={t('common.delete')}
            title={t('events:questions.delete.title')}
            description={t('events:questions.delete.description')}
            confirmText={t('common.delete')}
            disabled={remove.isPending}
            onConfirm={() => remove.mutate(question.id, { onSuccess: () => void navigate({ href: back }) })}
          />
        )
      }
    >
      <div className="flex flex-col gap-6">
        {refusal === null ? null : <Notice tone="error" title={refusal} />}
        <SchemaForm
          schema={questionSchema}
          defaults={
            question === null
              ? emptyQuestion(eventId, next, locales)
              : questionToFormValues(question, locales)
          }
          locales={locales}
          labels="events:questions"
          onSubmit={async (values) => {
            await save.mutateAsync(values);
            void navigate({ href: back });
          }}
          submitLabel={t('common.save')}
          secondaryAction={
            <Button asChild variant="ghost">
              <RouterAnchor href={back}>{t('common.cancel')}</RouterAnchor>
            </Button>
          }
        />
      </div>
    </PageShell>
  );
}
