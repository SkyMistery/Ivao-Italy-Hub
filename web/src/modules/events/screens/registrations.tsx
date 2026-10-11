import { useRouteContext } from '@tanstack/react-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { SchemaForm, describeProblem } from '../../../shared/forms';
import { DataList, col, listSearchSchema, type ColumnSpec, type ListSearch } from '../../../shared/list';
import { ConfirmDialog, Notice } from '../../../shared/ui';
import {
  staffRegistrationsQuery,
  useRemoveRegistration,
  type EventDetailDto,
  type EventRegistrationDto,
} from '../api';
import { bookingRemovalSchema, type BookingRemovalValues } from '../schemas';

/**
 * The registrations of an event in person in the back office (design M4 §4-bis.2, §7.2, E16): a tab of the event's page with the
 * generated list — the member, named with the core's helper (`col.person`: an erased one is said so, never as a number), their answers
 * in words, one line per question, when they registered and when they last changed them —, the first registered first unless sorted;
 * and «take away» with the reason the member reads in the mail. Whoever reads the bookings of the event has the tab, whoever writes
 * them «take away»; the sums are in the tab of the questions.
 */

const columns: readonly ColumnSpec<EventRegistrationDto>[] = [
  col.person('member'),
  col.list('answers'),
  col.date('createdAt', { sortable: true }),
  col.date('updatedAt', { sortable: true }),
];

export function RegistrationsTab({ event, editable }: { event: EventDetailDto; editable: boolean }) {
  const { i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const [search, setSearch] = useState<ListSearch>(() => listSearchSchema.parse({ pageSize: 100 }));

  return (
    <DataList
      columns={columns}
      query={staffRegistrationsQuery(event.id, search)}
      labels="events:registrations"
      locale={i18n.language}
      defaultLocale={bootstrap.division.defaultLocale}
      timezone={bootstrap.division.timezone}
      search={search}
      onSearchChange={(patch) => setSearch((current) => ({ ...current, ...patch }))}
      actions={(row) => (editable ? <RemoveRegistration registration={row} /> : null)}
    />
  );
}

/** «Take away»: asked once more, with the reason — required, in the staff's own words, sent to the member and kept nowhere else. */
function RemoveRegistration({ registration }: { registration: EventRegistrationDto }) {
  const { t, i18n } = useTranslation();
  const remove = useRemoveRegistration();
  const [reason, setReason] = useState('');
  const refusal = describeProblem(remove.error, t, i18n.language);

  return (
    <div className="flex flex-col items-end gap-1">
      <ConfirmDialog
        triggerText={t('events:registrations.remove.trigger')}
        title={t('events:registrations.remove.title')}
        description={t('events:registrations.remove.description')}
        confirmText={t('events:registrations.remove.confirm')}
        disabled={remove.isPending}
        confirmDisabled={reason.trim() === ''}
        onOpenChange={(open) => {
          if (open) {
            remove.reset();
            setReason('');
          }
        }}
        onConfirm={() => remove.mutate({ id: registration.id, reason: reason.trim() })}
      >
        <SchemaForm<BookingRemovalValues>
          schema={bookingRemovalSchema}
          defaults={{ reason }}
          locales={[]}
          labels="events:registrations.remove"
          onChange={(values) => setReason(values.reason)}
        />
      </ConfirmDialog>
      {refusal === null ? null : <Notice tone="error" title={refusal} />}
    </div>
  );
}
