import { useQuery } from '@tanstack/react-query';
import { Button, Subtle } from '@ivao/atmosphere-react';
import { Link, createFileRoute, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { useReplyContact, useUpdateContactStatus } from '../../features/contacts/mutations';
import { contactQuery, contactThreadQuery, type ContactDetailDto } from '../../features/contacts/queries';
import { contactStatusSchema, type ContactStatusFormValues } from '../../features/contacts/schema';
import { deptParam } from '../../shared/api/department';
import { SchemaForm } from '../../shared/forms';
import { MessageThread, PageShell } from '../../shared/ui';

/**
 * One message, and the one thing the department may write about it.
 *
 * What somebody sent is shown and not drawn as a form: there is no payload that could change it
 * (`ContactStatusWriteDto` carries the status alone), so a disabled input would be a promise the
 * server does not need to keep. The form below is the whole of what this screen can save.
 */
export const Route = createFileRoute('/_staff/staff/$dept/contacts/$id')({
  loader: async ({ context, params }): Promise<ContactDetailDto> => {
    // The thread alongside the row (M2, T14a): what was said, then what the department may change.
    await context.queryClient.ensureQueryData(contactThreadQuery(Number(params.id)));
    return context.queryClient.ensureQueryData(contactQuery(Number(params.id)));
  },
  component: ContactDetail,
});

function ContactDetail() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const { dept, id } = Route.useParams();
  const navigate = useNavigate();

  // The row as it stands now. ⚠️ Not `Route.useLoaderData()`: a loader runs on navigation and
  // never again, so after one save the screen still held the `rowVersion` from when the page
  // opened, and the second save was answered 409 — blaming somebody who does not exist. The loader
  // above is the *preload*; what the screen reads is the query it filled (design M0 §7.3).
  const { data: message, refetch: refetchMessage } = useQuery(contactQuery(Number(id)));
  const thread = useQuery(contactThreadQuery(Number(id))).data;

  const update = useUpdateContactStatus(Number(id));
  const reply = useReplyContact(Number(id));

  if (message === undefined || thread === undefined) {
    // The loader has already put it in the cache, so this is the compiler asking rather than a
    // state a reader reaches.
    return null;
  }

  const backToList = () => void navigate({ to: '/staff/$dept/contacts', params: { dept } });

  const submit = async (values: ContactStatusFormValues) => {
    await update.mutateAsync(values);
    backToList();
  };

  const sentAt = new Date(message.createdAt);
  const formatted = new Intl.DateTimeFormat(i18n.language, {
    dateStyle: 'medium',
    timeStyle: 'short',
    timeZone: bootstrap.division.timezone,
  }).format(sentAt);

  return (
    <PageShell
      title={message.subject}
      breadcrumb={[
        { label: dept },
        { label: t('contacts.title'), to: `/staff/${deptParam.format(dept)}/contacts` },
        { label: message.subject },
      ]}
    >
      <div className="flex max-w-3xl flex-col gap-6">
        <Subtle>{t('contacts.from', { vid: message.createdBy, at: formatted })}</Subtle>
        {/* The conversation, with the box to answer it: Contacts.View answers, and the answer moves the status by
            itself (note 2026-09-15-contatti-con-risposte §3.1). The form below is for moving it by hand. */}
        <MessageThread
          thread={thread}
          timezone={bootstrap.division.timezone}
          onReply={async (body) => {
            await reply.mutateAsync(body);
            // The row's version moved with the status: the form below has to hold the new one.
            await refetchMessage();
          }}
        />

        <SchemaForm
          schema={contactStatusSchema}
          defaults={{ status: message.status, rowVersion: message.rowVersion }}
          locales={bootstrap.division.locales}
          labels="contacts"
          onSubmit={submit}
          submitLabel={t('common.save')}
          secondaryAction={
            <Button asChild variant="ghost">
              <Link to="/staff/$dept/contacts" params={{ dept }}>
                {t('common.cancel')}
              </Link>
            </Button>
          }
        />
      </div>
    </PageShell>
  );
}
