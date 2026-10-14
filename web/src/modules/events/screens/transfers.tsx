import { Button, H2 } from '@ivao/atmosphere-react';
import { useRouteContext } from '@tanstack/react-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { SchemaForm } from '../../../shared/forms';
import { DataList, col, listSearchSchema, type ColumnSpec, type ListSearch } from '../../../shared/list';
import { Dialog } from '../../../shared/ui';
import {
  transfersQuery,
  useApproveTransfer,
  useRefuseTransfer,
  type AtcEventDto,
  type TransferRow,
} from '../api';
import {
  refuseTransferSchema,
  transferSchema,
  type RefuseTransferFormValues,
  type TransferFormValues,
} from '../schemas';

import { transferLines } from './rosterWords';

/**
 * The requests to hand a shift over of an event, in the staff's back office (design M4 §4.4-bis, §7.2, E12; note
 * 2026-10-14-la-pubblicazione-del-roster-e-la-cessione, reading 7): a generated list under the roster — the shift, who hands it over and
 * to whom, the state, the note and the warnings on whoever would take it — with «Approve» and «Refuse» on one that waits, for whoever writes
 * the ATC of the event. The server answers who may decide which: nobody their own request, nobody giving a shift to themselves.
 */

const columns: readonly ColumnSpec<TransferRow>[] = [
  col.text('callsign'),
  col.date('fromUtc'),
  col.date('toUtc'),
  col.person('from'),
  col.person('to'),
  col.badge('status', 'events:atc.transfers'),
  col.text('note'),
  col.list('alerts'),
  col.text('decisionNote'),
];

export function TransfersSection({ event, editable }: { event: AtcEventDto; editable: boolean }) {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const [search, setSearch] = useState<ListSearch>(() => listSearchSchema.parse({}));

  if (event.rosterPublishedAt === null) {
    return null;
  }

  return (
    <section className="flex flex-col gap-4" aria-label={t('events:atc.transfers.title')}>
      <H2>{t('events:atc.transfers.title')}</H2>
      <DataList
        columns={columns}
        query={transfersQuery(event.id, search, i18n.language, (row) => transferLines(row, t))}
        labels="events:atc.transfers"
        locale={i18n.language}
        defaultLocale={bootstrap.division.defaultLocale}
        timezone={bootstrap.division.timezone}
        search={search}
        onSearchChange={(patch) => setSearch((current) => ({ ...current, ...patch }))}
        emptyDescription={t('events:atc.transfers.empty')}
        actions={(row) => (editable && row.status === 'Pending' ? <Decide row={row} /> : null)}
      />
    </section>
  );
}

/** «Approve» — with whoever was named, or another member the staff choose — and «Refuse» with a note, each in its generated form. */
function Decide({ row }: { row: TransferRow }) {
  const { t } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const approve = useApproveTransfer();
  const refuse = useRefuseTransfer();
  const [opened, setOpened] = useState<'approve' | 'refuse' | null>(null);
  const division = { defaultLocale: bootstrap.division.defaultLocale, timezone: bootstrap.division.timezone };

  return (
    <div className="flex gap-1">
      <Dialog
        open={opened === 'approve'}
        onOpenChange={(open) => {
          setOpened(open ? 'approve' : null);
          approve.reset();
        }}
        trigger={
          <Button variant="ghost" size="sm">
            {t('events:atc.transfers.approve')}
          </Button>
        }
        title={t('events:atc.transfers.approveTitle', { callsign: row.callsign })}
        description={t(
          row.to === null ? 'events:atc.transfers.approveNobody' : 'events:atc.transfers.approveDescription',
        )}
      >
        <SchemaForm<TransferFormValues>
          schema={transferSchema}
          defaults={{ ...(row.to === null ? {} : { toVid: row.to.vid }), note: '' }}
          locales={[]}
          labels="events:atc.transfers.decision"
          division={division}
          onSubmit={async (values) => {
            await approve.mutateAsync({ id: row.id, toVid: values.toVid ?? null, note: values.note });
            setOpened(null);
          }}
          submitLabel={t('events:atc.transfers.approve')}
        />
      </Dialog>
      <Dialog
        open={opened === 'refuse'}
        onOpenChange={(open) => {
          setOpened(open ? 'refuse' : null);
          refuse.reset();
        }}
        trigger={
          <Button variant="ghost" size="sm">
            {t('events:atc.transfers.refuse')}
          </Button>
        }
        title={t('events:atc.transfers.refuseTitle', { callsign: row.callsign })}
        description={t('events:atc.transfers.refuseDescription')}
      >
        <SchemaForm<RefuseTransferFormValues>
          schema={refuseTransferSchema}
          defaults={{ note: '' }}
          locales={[]}
          labels="events:atc.transfers.decision"
          division={division}
          onSubmit={async (values) => {
            await refuse.mutateAsync({ id: row.id, note: values.note });
            setOpened(null);
          }}
          submitLabel={t('events:atc.transfers.refuse')}
        />
      </Dialog>
    </div>
  );
}
