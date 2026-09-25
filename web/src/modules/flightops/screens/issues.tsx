import { Button } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate, useParams, useSearch } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { writableDepartments } from '../../../shared/api/bootstrap';
import { SchemaForm } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { useMoment } from '../../../shared/i18n/useMoment';
import { DataList, ListFilter, col, type ColumnSpec } from '../../../shared/list';
import { NotFound, PageShell } from '../../../shared/ui';
import {
  legIssueQuery,
  legIssueToFormValues,
  legIssuesListQuery,
  memberName,
  useSaveLegIssue,
  type LegIssueDto,
  type LegIssueRow,
} from '../api';
import { TOURS_EDIT } from '../permissions';
import { legIssueSchema, legIssuesSearchSchema, type LegIssueFormValues } from '../schemas';

import { useStaff } from './hooks';

/**
 * The issues the pilots report on the legs (design M2 §3.11; note 2026-09-23-contestazioni-chiarimenti-segnalazioni §3.3): the
 * open ones first, the closed ones on asking; one issue with what the pilot wrote, and the form that closes it with a note for
 * the rest of the staff. List and form generated; `Tours.View` reads, `Tours.Edit` closes.
 */

export const ISSUES = '/staff/tours/issues';

const columns: readonly ColumnSpec<LegIssueRow>[] = [
  col.localized('tourTitle'),
  col.text('leg'),
  col.text('pilotName'),
  col.text('body'),
  col.badge('status', 'flightops:issues'),
  col.date('createdAt', { sortable: true }),
];

export function LegIssuesPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useStaff();
  const navigate = useNavigate();
  const search = legIssuesSearchSchema.parse(useSearch({ strict: false }));

  const onSearchChange = (patch: Partial<typeof search>) =>
    void navigate({
      search: ((previous: typeof search) => ({ ...previous, ...patch })) as never,
      to: '.',
    });

  return (
    <PageShell
      title={t('flightops:issues.title')}
      description={t('flightops:issues.description')}
      breadcrumb={[{ label: t('flightops:nav.section') }, { label: t('flightops:issues.title') }]}
    >
      <DataList
        columns={columns}
        query={legIssuesListQuery(search, { open: search.all !== true })}
        labels="flightops:issues"
        locale={i18n.language}
        defaultLocale={bootstrap.division.defaultLocale}
        timezone={bootstrap.division.timezone}
        search={search}
        onSearchChange={onSearchChange}
        toolbar={
          <ListFilter
            id="issues-shown"
            label={t('flightops:issues.filters.shown')}
            none={t('flightops:issues.filters.open')}
            value={search.all === true ? 'all' : undefined}
            onChange={(value) => onSearchChange({ all: value === 'all' ? true : undefined, page: 1 })}
            items={[{ value: 'all', label: t('flightops:issues.filters.all') }]}
          />
        }
        actions={(row) => (
          <Button asChild variant="ghost" size="sm">
            <RouterAnchor href={`${ISSUES}/${row.id}`}>{t('flightops:issues.open')}</RouterAnchor>
          </Button>
        )}
      />
    </PageShell>
  );
}

export function LegIssueForm() {
  const { t } = useTranslation();
  const { id = '' } = useParams({ strict: false });
  const issue = useQuery({ ...legIssueQuery(Number(id)), enabled: Number.isInteger(Number(id)) });

  if (issue.isPending) {
    return <p className="text-muted-foreground text-sm">{t('common.loading')}</p>;
  }

  return issue.data === undefined ? (
    <NotFound />
  ) : (
    <LegIssueScreen key={issue.data.rowVersion} issue={issue.data} />
  );
}

function LegIssueScreen({ issue }: { issue: LegIssueDto }) {
  const { t } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();
  const navigate = useNavigate();
  const { bootstrap } = useStaff();
  const save = useSaveLegIssue(issue);
  const writes = writableDepartments(bootstrap, TOURS_EDIT).length > 0;
  const back = () => void navigate({ href: ISSUES });
  const title = t('flightops:issues.one', { tour: read(issue.tourTitle) });

  return (
    <PageShell
      title={title}
      breadcrumb={[
        { label: t('flightops:nav.section') },
        { label: t('flightops:issues.title'), to: ISSUES },
        { label: `#${issue.id}` },
      ]}
    >
      <div className="flex flex-col gap-6">
        <dl className="grid grid-cols-1 gap-x-6 gap-y-2 text-sm sm:grid-cols-2">
          <div className="flex flex-col">
            <dt className="text-muted-foreground">{t('flightops:issues.fields.leg')}</dt>
            <dd>
              {issue.legNumber === null ? issue.route : `${issue.legNumber} · ${issue.route ?? ''}`}{' '}
              <RouterAnchor href={`/staff/tours/${issue.tourId}`} className="underline">
                {t('flightops:issues.toTour')}
              </RouterAnchor>
            </dd>
          </div>
          <div className="flex flex-col">
            <dt className="text-muted-foreground">{t('flightops:issues.fields.pilotName')}</dt>
            <dd>{memberName(issue.pilot, t)}</dd>
          </div>
          <div className="flex flex-col">
            <dt className="text-muted-foreground">{t('flightops:issues.fields.createdAt')}</dt>
            <dd>{moment(issue.createdAt)}</dd>
          </div>
        </dl>

        <p className="whitespace-pre-line">{issue.body}</p>

        {writes ? (
          <SchemaForm<LegIssueFormValues>
            schema={legIssueSchema}
            defaults={legIssueToFormValues(issue)}
            locales={[]}
            labels="flightops:issues"
            onSubmit={async (values) => {
              await save.mutateAsync(values);
              back();
            }}
            submitLabel={t('common.save')}
            secondaryAction={
              <Button asChild variant="ghost">
                <RouterAnchor href={ISSUES}>{t('common.cancel')}</RouterAnchor>
              </Button>
            }
          />
        ) : null}
      </div>
    </PageShell>
  );
}
