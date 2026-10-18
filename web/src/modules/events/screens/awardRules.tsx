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
  awardRuleChoicesQuery,
  awardRuleQuery,
  awardRuleToFormValues,
  awardRulesQuery,
  eventQuery,
  useDeleteAwardRule,
  useSaveAwardRule,
  type AwardRuleRow,
  type EventDetailDto,
} from '../api';
import {
  awardCriterionSchema,
  awardRuleSchema,
  emptyAwardRule,
  type AwardCriterionValue,
  type AwardRuleChoices,
} from '../schemas';

import { awardRuleParameters } from './awardRuleWords';

/**
 * The rules of award of an event (design M4 §1.9, §5.4, §7.2, E14b): a tab of the event's page with the generated list — the award, the
 * criterion, its parameters in words the page writes — and each rule in a generated form on a page of its own, as a route is. Whoever writes
 * the event writes its rules (`Events.Edit`). Accepting a report evaluates them; a person assigns the award from the core's queue.
 */

const EVENTS = '/staff/events';

const columns: readonly ColumnSpec<AwardRuleRow>[] = [
  col.localized('awardName'),
  col.badge('criterion', 'events:awardRules'),
  col.text('parameters'),
];

export function AwardRulesTab({ event, editable }: { event: EventDetailDto; editable: boolean }) {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const [search, setSearch] = useState<ListSearch>(() => listSearchSchema.parse({ pageSize: 100 }));
  const base = `${EVENTS}/${event.id}`;

  const create = editable ? (
    <Button asChild>
      <RouterAnchor href={`${base}/award-rules/new`}>
        <Plus aria-hidden className="mr-2 size-4" />
        {t('events:awardRules.create')}
      </RouterAnchor>
    </Button>
  ) : null;

  return (
    <DataList
      columns={columns}
      query={awardRulesQuery(event.id, search, i18n.language, (row) =>
        awardRuleParameters(row, t, i18n.language),
      )}
      labels="events:awardRules"
      locale={i18n.language}
      defaultLocale={bootstrap.division.defaultLocale}
      timezone={bootstrap.division.timezone}
      search={search}
      onSearchChange={(patch) => setSearch((current) => ({ ...current, ...patch }))}
      {...(create === null ? {} : { toolbar: create })}
      // To whoever writes the rules and whoever only reads them alike: what the tab will hold.
      emptyDescription={t('events:awardRules.empty')}
      actions={(row) =>
        editable ? (
          <Button asChild variant="ghost" size="sm">
            <RouterAnchor href={`${base}/award-rules/${row.id}`}>{t('common.edit')}</RouterAnchor>
          </Button>
        ) : null
      }
    />
  );
}

/**
 * One rule of award of an event: the criterion chosen above, with what it asks of a report, and the form of its award and its parameters
 * below — drawn again when the criterion changes —; «delete» for one that exists, and back to the tab.
 */
export function AwardRuleForm() {
  const { t, i18n } = useTranslation();
  const read = useLocalized();
  const navigate = useNavigate();
  // The routes of a module are registered from its manifest, so their parameters are not in the router's typed tree.
  const params: Readonly<Record<string, string | undefined>> = useParams({ strict: false });
  const eventId = Number(params.id);
  const id = (params.ruleId ?? 'new') === 'new' ? null : Number(params.ruleId);

  const event = useQuery(eventQuery(eventId)).data;
  const rule = useQuery({ ...awardRuleQuery(id ?? 0), enabled: id !== null }).data ?? null;
  const offered = useQuery(awardRuleChoicesQuery()).data;
  const save = useSaveAwardRule(id);
  const remove = useDeleteAwardRule();
  const [picked, setPicked] = useState<AwardCriterionValue | null>(null);

  if (event === undefined || offered === undefined || (id !== null && rule === null)) {
    return null;
  }

  const criterion: AwardCriterionValue = picked ?? rule?.criterion ?? 'AnyFlight';
  const choices: AwardRuleChoices = {
    awards: offered.awards.map((award) => ({ value: String(award.id), label: read(award.name) })),
    positionTypes: offered.positionTypes.map((type) => ({ value: type, label: type })),
  };
  const back = `${EVENTS}/${eventId}?tab=awards`;
  const title = t(id === null ? 'events:awardRules.create' : 'events:awardRules.edit');
  const refusal = describeProblem(remove.error, t, i18n.language);

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
        rule === null ? undefined : (
          <ConfirmDialog
            triggerText={t('common.delete')}
            title={t('events:awardRules.delete.title')}
            description={t('events:awardRules.delete.description')}
            confirmText={t('common.delete')}
            disabled={remove.isPending}
            onConfirm={() => remove.mutate(rule.id, { onSuccess: () => void navigate({ href: back }) })}
          />
        )
      }
    >
      <div className="flex flex-col gap-6">
        {refusal === null ? null : <Notice tone="error" title={refusal} />}
        <div className="flex flex-col gap-2">
          <SchemaForm
            schema={awardCriterionSchema}
            defaults={{ criterion }}
            locales={[]}
            labels="events:awardRules"
            onChange={(values) => setPicked(values.criterion)}
          />
          <p className="text-sm">{t(`events:awardRules.explain.${criterion}`)}</p>
        </div>
        <SchemaForm
          key={criterion}
          schema={awardRuleSchema(criterion, choices)}
          defaults={rule === null ? emptyAwardRule(eventId) : awardRuleToFormValues(rule)}
          locales={[]}
          labels="events:awardRules"
          onSubmit={async (values) => {
            await save.mutateAsync({ criterion, values });
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
