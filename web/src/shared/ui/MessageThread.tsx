import { Badge, Subtle } from '@ivao/atmosphere-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import type { components } from '../api/schema';
import { SchemaForm } from '../forms';

export type MessageThreadValue = components['schemas']['ContactThreadDto'];
type Reply = components['schemas']['ContactReplyDto'];

/** One answer at most as long as a message (`ContactSubmitDtoValidator.MaxBodyLength`); the server says so too. */
const replySchema = z.object({ body: z.string().meta({ multiline: true }) });

/**
 * A conversation with a department: the message, what it is about, the answers in order and, when the reader may
 * write, the box to answer (M2, T14a, note 2026-09-15-contatti-con-risposte §3.1). The twenty-third of the closed list,
 * decided in that note: it is mounted by the back office of a department and by `/me/contacts` alike.
 *
 * It draws what the server sent and hides nothing itself: for the member who wrote, the server has already taken the
 * name and the VID off every answer of the department's side, so "the department" is all there is to draw (design M2
 * §3.5). Plain text as it was written, never markdown — somebody's asterisks are not somebody else's formatting.
 */
export function MessageThread({
  thread,
  timezone,
  onReply,
}: {
  thread: MessageThreadValue;
  /** The division's zone: a conversation is read in the time of the division, like every date of the hub. */
  timezone: string;
  /** Rejecting with an `ApiError` is how the server's refusal reaches the box. Absent, the thread is read only. */
  onReply?: (body: string) => Promise<unknown>;
}) {
  const { t, i18n } = useTranslation();
  // A new key after each answer: the box comes back empty rather than holding what was just sent.
  const [round, setRound] = useState(0);

  const when = (value: string) =>
    new Intl.DateTimeFormat(i18n.language, {
      dateStyle: 'medium',
      timeStyle: 'short',
      timeZone: timezone,
    }).format(new Date(value));

  const department = t('contacts.thread.department', { department: t(`departments.${thread.department}`) });

  const author = (reply: Reply) => {
    if (reply.side === 'Sender') {
      return thread.readerIsSender
        ? t('contacts.thread.you')
        : t('contacts.thread.sender', { vid: thread.senderVid });
    }

    if (thread.readerIsSender) {
      return department;
    }

    const who = reply.authorName ?? (reply.authorVid === null ? '' : `VID ${String(reply.authorVid)}`);
    return `${who} · ${reply.side === 'Participant' ? t('contacts.thread.participant') : department}`;
  };

  return (
    <div className="flex flex-col gap-4">
      <div className="flex flex-wrap items-center gap-2">
        <Badge
          variant="flat"
          color="indigo"
          text={t(`contacts.options.kind.${thread.kind}`, { defaultValue: thread.kind })}
        />
        <Badge variant="flat" color="gray" text={t(`contacts.options.status.${thread.status}`)} />
        {thread.participants.length > 0 ? (
          <Subtle>{t('contacts.thread.participants', { vids: thread.participants.join(', ') })}</Subtle>
        ) : null}
      </div>

      {thread.references.length > 0 ? (
        <div className="flex flex-wrap items-baseline gap-2 text-sm">
          <span className="font-medium">{t('contacts.thread.about')}</span>
          {thread.references.map((reference) =>
            reference.url ? (
              <a
                key={`${reference.sourceModule}:${reference.sourceId}`}
                href={reference.url}
                className="text-primary underline"
              >
                {reference.label}
              </a>
            ) : (
              <span key={`${reference.sourceModule}:${reference.sourceId}`}>{reference.label}</span>
            ),
          )}
        </div>
      ) : null}

      <ol className="flex flex-col gap-3">
        <Entry
          mine={thread.readerIsSender}
          author={
            thread.readerIsSender
              ? t('contacts.thread.you')
              : t('contacts.thread.sender', { vid: thread.senderVid })
          }
          at={when(thread.createdAt)}
          body={thread.body}
        />
        {thread.replies.map((reply) => (
          <Entry
            key={reply.id}
            mine={(reply.side === 'Sender') === thread.readerIsSender}
            author={author(reply)}
            at={when(reply.createdAt)}
            body={reply.body}
          />
        ))}
      </ol>

      {thread.replies.length === 0 ? <Subtle>{t('contacts.thread.noReplies')}</Subtle> : null}

      {onReply ? (
        <SchemaForm
          key={round}
          schema={replySchema}
          defaults={{ body: '' }}
          // An answer is written in one language, the writer's: no language tabs.
          locales={[]}
          labels="contacts.thread"
          onSubmit={async (values) => {
            await onReply(values.body.trim());
            setRound((value) => value + 1);
          }}
          submitLabel={t('contacts.thread.send')}
        />
      ) : null}
    </div>
  );
}

/** One message of the conversation. The reader's side on the right, the other side on the left. */
function Entry({ mine, author, at, body }: { mine: boolean; author: string; at: string; body: string }) {
  return (
    <li
      className={`border-border max-w-[85%] rounded-md border p-4 ${mine ? 'bg-muted self-end' : 'bg-card self-start'}`}
    >
      <Subtle>
        {author} · {at}
      </Subtle>
      <p className="mt-2 text-sm whitespace-pre-wrap">{body}</p>
    </li>
  );
}
