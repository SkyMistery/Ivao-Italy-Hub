import { Button } from '@ivao/atmosphere-react';
import { useRouteContext } from '@tanstack/react-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { SchemaForm } from '../../../shared/forms';
import { DataList, col, listSearchSchema, type ColumnSpec, type ListSearch } from '../../../shared/list';
import { Dialog, personName } from '../../../shared/ui';
import { eventReportsQuery, useDecideReport, type EventDetailDto, type ReportRow } from '../api';
import { decidableReportStates, reportDecisionSchema, type ReportDecisionFormValues } from '../schemas';

import { reportLines } from './reportWords';

/**
 * The tab «Reports of support» of an event over, in the staff's back office (design M4 §5.3, §7.2, E14a; note
 * 2026-10-17-il-pirep-di-supporto, reading 9): the generated list of its reports — the member, the kind, where it stands, what the check
 * made of it, when it was sent, each item with what the check found of it, who decided and their note — with «Decide» where the server says
 * the reader may (`decidable`, the one handler on the report: never their own, the super administrator included). The server sends data;
 * the page writes the items' lines in the language on screen.
 */

const columns: readonly ColumnSpec<ReportRow>[] = [
  col.person('member'),
  col.badge('kind', 'events:reports'),
  col.badge('state', 'events:reports'),
  col.badge('verification', 'events:reports'),
  col.date('submittedAt'),
  col.list('lines'),
  col.person('decidedBy'),
  col.text('decisionNote'),
];

export function ReportsTab({ event }: { event: EventDetailDto }) {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const [search, setSearch] = useState<ListSearch>(() => listSearchSchema.parse({}));

  return (
    <div className="flex flex-col gap-4">
      <DataList
        columns={columns}
        query={eventReportsQuery(event.id, search, i18n.language, (row) =>
          reportLines(row.items, row.kind, t, i18n.language),
        )}
        labels="events:reports"
        locale={i18n.language}
        defaultLocale={bootstrap.division.defaultLocale}
        timezone={bootstrap.division.timezone}
        search={search}
        onSearchChange={(patch) => setSearch((current) => ({ ...current, ...patch }))}
        emptyDescription={t('events:reports.empty')}
        actions={(row) => (row.decidable ? <Decide row={row} /> : null)}
      />
    </div>
  );
}

/** «Decide»: accepted or rejected — the other one, once decided —, with a note the member reads in the mail, required to reject. */
function Decide({ row }: { row: ReportRow }) {
  const { t } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const decide = useDecideReport();
  const [open, setOpen] = useState(false);
  const allowed = decidableReportStates(row.state);
  const name = personName(row.member, t);

  return (
    <Dialog
      open={open}
      onOpenChange={(next) => {
        setOpen(next);
        decide.reset();
      }}
      trigger={
        <Button variant="ghost" size="sm">
          {t(row.state === 'Submitted' ? 'events:reports.decide' : 'events:reports.change')}
        </Button>
      }
      title={t(`events:reports.decideTitle.${row.kind}`, { name })}
      description={t('events:reports.decideDescription')}
    >
      <SchemaForm<ReportDecisionFormValues>
        schema={reportDecisionSchema(allowed)}
        defaults={{ state: allowed[0]!, note: '' }}
        locales={[]}
        labels="events:reports.decision"
        division={{ defaultLocale: bootstrap.division.defaultLocale, timezone: bootstrap.division.timezone }}
        onSubmit={async (values) => {
          await decide.mutateAsync({ id: row.id, state: values.state, note: values.note });
          setOpen(false);
        }}
        submitLabel={t('events:reports.decide')}
      />
    </Dialog>
  );
}
