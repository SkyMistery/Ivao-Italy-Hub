import { useQuery } from '@tanstack/react-query';
import { useRouteContext } from '@tanstack/react-router';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { SchemaForm, describeProblem } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { DataList, col, listSearchSchema, type ColumnSpec, type ListSearch } from '../../../shared/list';
import { ConfirmDialog, Notice } from '../../../shared/ui';
import {
  questionsQuery,
  staffRegistrationsQuery,
  useRemoveRegistration,
  type EventDetailDto,
  type EventRegistrationDto,
  type RegistrationRow,
} from '../api';
import { bookingRemovalSchema, type BookingRemovalValues } from '../schemas';

import { answerLines, useAnswerWords } from './answers';

/**
 * The registrations of an event in person in the back office (design M4 §4-bis.2, §7.2, E16): a tab of the event's page with the
 * generated list — the member, named with the core's helper (`col.person`: an erased one is said so, never as a number), their answers
 * in words of the language on screen, one line per question (the server sends them as given), when they registered and when they last
 * changed them —, the first registered first unless sorted; and «take away» with the reason the member reads in the mail. Whoever reads the bookings of the event has the tab, whoever writes
 * them «take away»; the sums are in the tab of the questions.
 */

const columns: readonly ColumnSpec<RegistrationRow>[] = [
  col.person('member'),
  col.list('lines'),
  col.date('createdAt', { sortable: true }),
  col.date('updatedAt', { sortable: true }),
];

export function RegistrationsTab({ event, editable }: { event: EventDetailDto; editable: boolean }) {
  const { t, i18n } = useTranslation();
  const read = useLocalized();
  const words = useAnswerWords();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const [search, setSearch] = useState<ListSearch>(() => listSearchSchema.parse({ pageSize: 100 }));
  // The event's questions, which the answers are written in words with, in the language on screen (Carmine's answer 6 on #255).
  const questionsRead = useQuery(questionsQuery(event.id));
  const questions = questionsRead.data?.items;

  if (questionsRead.isError) {
    // Said, as on the event's page, rather than a tab with nothing in it (the second reading of #255, small 2).
    return (
      <Notice
        tone="error"
        title={describeProblem(questionsRead.error, t, i18n.language) ?? t('errors.unknown')}
      />
    );
  }

  if (questions === undefined) {
    return null;
  }

  return (
    <DataList
      columns={columns}
      query={staffRegistrationsQuery(event.id, search, i18n.language, (registration) =>
        answerLines(questions, registration.answers, read, words),
      )}
      labels="events:registrations"
      locale={i18n.language}
      defaultLocale={bootstrap.division.defaultLocale}
      timezone={bootstrap.division.timezone}
      search={search}
      onSearchChange={(patch) => setSearch((current) => ({ ...current, ...patch }))}
      // The registrations are the event's, not a department's: an empty tab says what will be there (#224, E4d).
      emptyDescription={t('events:registrations.empty')}
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
