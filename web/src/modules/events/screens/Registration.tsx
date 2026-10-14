import { H2, Subtle } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { loginHref } from '../../../shared/api/client';
import { SchemaForm, describeProblem } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { ConfirmDialog, Notice } from '../../../shared/ui';
import {
  myRegistrationsQuery,
  useRegister,
  useWithdrawRegistration,
  type MyRegistrationDto,
  type PublicEventDto,
} from '../api';

import { MY_EVENTS_PAGE } from './myBookings';
import { RegistrationAnswers } from './RegistrationAnswers';
import { registrationDefaults, registrationSchema, type RegistrationValues } from './registrationForm';

/**
 * The registration to an event in person on its page (design M4 §4-bis.2, §7.1, E16), under `#registration` — where `/events/mine`
 * sends a member to change their answers. A visitor is asked to sign in while the event takes registrations. A member not registered
 * answers the questions and registers; one registered reads that they are, changes their answers while the event takes registrations,
 * and withdraws until it starts. After the start, or on a cancelled event, it says the registrations are closed. Whether the answers
 * are right is the server's to say, field by field.
 */
export function RegistrationSection({ event, signedIn }: { event: PublicEventDto; signedIn: boolean }) {
  const { t, i18n } = useTranslation();
  // Read only for an event in person, by a member: no other page asks.
  const mine = useQuery({ ...myRegistrationsQuery(), enabled: signedIn && event.inPerson });
  // The registrations are open or closed from when the page was drawn, as the server's verbs will say again.
  const [drawnAt] = useState(() => Date.now());

  if (!event.inPerson) {
    return null;
  }

  const registration = mine.data?.find((row) => row.eventId === event.id);
  const open = event.unseen === null && event.cancelledAt === null && drawnAt < Date.parse(event.startsAtUtc);

  return (
    <section
      id="registration"
      className="flex flex-col gap-4"
      aria-label={t('events:public.registration.title')}
    >
      <H2>{t('events:public.registration.title')}</H2>
      {!signedIn ? (
        open ? (
          <p className="text-sm">
            <a href={loginHref(`${window.location.pathname}${window.location.search}`)} className="underline">
              {t('events:public.registration.signIn')}
            </a>
          </p>
        ) : (
          <Subtle className="text-sm">{t('events:public.registration.closed')}</Subtle>
        )
      ) : mine.isError ? (
        // The member's registrations could not be read: said, rather than a section with its title and nothing under it (the review of
        // #255, point 6).
        <Notice tone="error" title={describeProblem(mine.error, t, i18n.language) ?? t('errors.unknown')} />
      ) : mine.data === undefined ? null : registration === undefined ? (
        open ? (
          <RegistrationForm event={event} registration={undefined} />
        ) : (
          <Subtle className="text-sm">{t('events:public.registration.closed')}</Subtle>
        )
      ) : (
        <Registered event={event} registration={registration} />
      )}
    </section>
  );
}

/** A member registered: that they are, their answers — to change while the event takes registrations —, and «withdraw» until it starts. */
function Registered({ event, registration }: { event: PublicEventDto; registration: MyRegistrationDto }) {
  const { t, i18n } = useTranslation();
  const read = useLocalized();
  const withdraw = useWithdrawRegistration();
  const refusal = describeProblem(withdraw.error, t, i18n.language);

  return (
    <div className="flex flex-col gap-4">
      <Notice tone="success" title={t('events:public.registration.registered')} />
      {registration.changeable ? (
        <RegistrationForm event={event} registration={registration} />
      ) : (
        <RegistrationAnswers registration={registration} />
      )}
      {refusal === null ? null : <Notice tone="error" title={refusal} />}
      <div className="flex flex-wrap items-center gap-3">
        {registration.withdrawable ? (
          <ConfirmDialog
            triggerText={t('events:mine.withdraw')}
            triggerVariant="secondary"
            title={t('events:mine.withdrawRegistrationTitle', { title: read(event.title) || event.slug })}
            description={t('events:mine.withdrawRegistrationDescription')}
            confirmText={t('events:mine.withdraw')}
            confirmVariant="destructive"
            disabled={withdraw.isPending}
            onConfirm={() => withdraw.mutate(event.id)}
          />
        ) : null}
        <RouterAnchor href={MY_EVENTS_PAGE} className="text-sm underline">
          {t('events:mine.title')}
        </RouterAnchor>
      </div>
    </div>
  );
}

/**
 * The form built from the event's questions: «register» the first time, «save your answers» after. A refusal lands on the field of its
 * answer; one about the whole registration — closed meanwhile — above the form.
 */
function RegistrationForm({
  event,
  registration,
}: {
  event: PublicEventDto;
  registration: MyRegistrationDto | undefined;
}) {
  const { t } = useTranslation();
  const read = useLocalized();
  const register = useRegister(event.id);
  const [saved, setSaved] = useState(false);
  // An event with no question still registers: a form with nothing to answer but its button.
  const schema = event.questions.length === 0 ? z.object({}) : registrationSchema(event.questions, read);

  return (
    <div className="flex flex-col gap-4">
      {saved ? <Notice tone="success" title={t('events:public.registration.saved')} /> : null}
      <SchemaForm<RegistrationValues>
        schema={schema as unknown as z.ZodType<RegistrationValues, RegistrationValues>}
        defaults={registrationDefaults(event.questions, registration)}
        locales={[]}
        labels="events:public.registration"
        onSubmit={async (values) => {
          setSaved(false);
          await register.mutateAsync((values as Partial<RegistrationValues>).answers ?? {});
          setSaved(registration !== undefined);
        }}
        submitLabel={t(
          registration === undefined
            ? 'events:public.registration.register'
            : 'events:public.registration.save',
        )}
      />
    </div>
  );
}
