import { useQuery } from '@tanstack/react-query';
import { createFileRoute } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { useReplyContact } from '../../features/contacts/mutations';
import { contactThreadQuery, type ContactThreadDto } from '../../features/contacts/queries';
import { MessageThread, PageShell } from '../../shared/ui';

/**
 * One of the member's threads, and the box to answer it (M2, T14a). The link of every mail about a thread lands here:
 * there is no answering by mail (note 2026-09-15-contatti-con-risposte §3.1).
 */
export const Route = createFileRoute('/_member/me_/contacts/$id')({
  loader: ({ context, params }): Promise<ContactThreadDto> =>
    context.queryClient.ensureQueryData(contactThreadQuery(Number(params.id))),
  component: MyContactPage,
});

function MyContactPage() {
  const { t } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const { id } = Route.useParams();
  // The query and not the loader's data: after an answer the thread on screen is the one the server sent back.
  const thread = useQuery(contactThreadQuery(Number(id))).data;
  const reply = useReplyContact(Number(id));

  if (thread === undefined) {
    return null;
  }

  return (
    <PageShell
      title={thread.subject}
      breadcrumb={[{ label: t('contacts.mine.title'), to: '/me/contacts' }, { label: thread.subject }]}
    >
      <div className="max-w-3xl">
        <MessageThread
          thread={thread}
          timezone={bootstrap.division.timezone}
          onReply={(body) => reply.mutateAsync(body)}
        />
      </div>
    </PageShell>
  );
}
