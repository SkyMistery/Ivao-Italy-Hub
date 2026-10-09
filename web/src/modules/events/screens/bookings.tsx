import { useRouteContext } from '@tanstack/react-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { SchemaForm, describeProblem } from '../../../shared/forms';
import { DataList, col, listSearchSchema, type ColumnSpec, type ListSearch } from '../../../shared/list';
import { ConfirmDialog, Notice } from '../../../shared/ui';
import { staffBookingsQuery, useRemoveBooking, type EventBookingDto, type EventDetailDto } from '../api';
import { bookingRemovalSchema, type BookingRemovalValues } from '../schemas';

/**
 * The bookings of an event in the back office (design M4 §7.2, E6b): a tab of the event's page with the generated list — the pilot,
 * named with the core's helper (`col.person`: a person whose data was erased is said so, never as a number), the flight of the slot
 * with the aircraft they chose, when they booked and when the reminder left —, by the time of the flights unless sorted; and «take
 * away» with the reason the pilot reads in the mail (§3.6). Whoever reads the bookings of the event has the tab, whoever writes them
 * «take away».
 */

const columns: readonly ColumnSpec<EventBookingDto>[] = [
  col.person('pilot'),
  col.text('callsign'),
  col.text('aircraftIcao'),
  col.text('departureIcao'),
  col.date('offBlockUtc'),
  col.text('arrivalIcao'),
  col.date('onBlockUtc'),
  col.text('rotation'),
  col.date('createdAt', { sortable: true }),
  col.date('remindedAt'),
];

export function BookingsTab({ event, editable }: { event: EventDetailDto; editable: boolean }) {
  const { i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const [search, setSearch] = useState<ListSearch>(() => listSearchSchema.parse({ pageSize: 100 }));

  return (
    <DataList
      columns={columns}
      query={staffBookingsQuery(event.id, search)}
      labels="events:bookings"
      locale={i18n.language}
      defaultLocale={bootstrap.division.defaultLocale}
      timezone={bootstrap.division.timezone}
      search={search}
      onSearchChange={(patch) => setSearch((current) => ({ ...current, ...patch }))}
      actions={(row) => (editable ? <RemoveBooking booking={row} /> : null)}
    />
  );
}

/**
 * «Take away» (§3.6): asked once more, with the reason — required, in the staff's own words, sent to the pilot and kept nowhere else
 * (the audit says who and when). The slot is free again.
 */
function RemoveBooking({ booking }: { booking: EventBookingDto }) {
  const { t, i18n } = useTranslation();
  const remove = useRemoveBooking();
  const [reason, setReason] = useState('');
  const refusal = describeProblem(remove.error, t, i18n.language);

  return (
    <div className="flex flex-col items-end gap-1">
      <ConfirmDialog
        triggerText={t('events:bookings.remove.trigger')}
        title={t('events:bookings.remove.title', { callsign: booking.callsign ?? '' })}
        description={t('events:bookings.remove.description')}
        confirmText={t('events:bookings.remove.confirm')}
        disabled={remove.isPending}
        confirmDisabled={reason.trim() === ''}
        onOpenChange={(open) => {
          if (open) {
            remove.reset();
            setReason('');
          }
        }}
        onConfirm={() => remove.mutate({ id: booking.id, reason: reason.trim() })}
      >
        <SchemaForm<BookingRemovalValues>
          schema={bookingRemovalSchema}
          defaults={{ reason }}
          locales={[]}
          labels="events:bookings.remove"
          onChange={(values) => setReason(values.reason)}
        />
      </ConfirmDialog>
      {refusal === null ? null : <Notice tone="error" title={refusal} />}
    </div>
  );
}
