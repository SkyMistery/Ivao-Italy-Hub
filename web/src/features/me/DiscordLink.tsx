import { Button, H4, Subtle } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { ProblemAlert, describeProblem } from '../../shared/forms';
import { useMoment } from '../../shared/i18n/useMoment';

import { discordLinkQuery, useUnlinkDiscord } from './queries';

/** Why a round trip to Discord did not link anything; the server says it in the address it comes back to. */
export type DiscordRefusal = 'taken' | 'failed';

/**
 * The member's link with their Discord account (note 2026-11-05-il-collegamento-con-discord): a place of theirs, like the
 * preferences above it, and only they see it. An installation with no Discord application offers nothing; somebody linked
 * before it was taken away can still undo it.
 *
 * Linking is a full navigation, not a router link: `/auth/discord` is a Kestrel endpoint that leaves for Discord.
 */
export function DiscordLink({ refusal }: { refusal?: DiscordRefusal | undefined }) {
  const { t, i18n } = useTranslation();
  const moment = useMoment();
  const { data: link } = useQuery(discordLinkQuery);
  const unlink = useUnlinkDiscord();

  // With the link not read, the refusal still has something to say; an installation that offers none does not.
  if (link ? !link.available && !link.username : refusal === undefined) {
    return null;
  }

  return (
    <section className="flex flex-col gap-3">
      <H4>{t('discord.title')}</H4>

      {/* What the last round trip said, until the member has done something about it: the address keeps saying it. */}
      {refusal !== undefined && !unlink.isSuccess && (
        <ProblemAlert summary={t(`discord.refused.${refusal}`)} />
      )}

      {/* A limit reached, or the row changed by a sign in: the button comes back, and it must say why. */}
      <ProblemAlert summary={describeProblem(unlink.error, t, i18n.language)} />

      {!link ? null : link.username ? (
        <>
          <div>
            <p className="text-sm font-medium">{link.username}</p>
            {link.linkedAt && (
              <Subtle>{t('discord.linkedAt', { when: moment(link.linkedAt, { time: false }) })}</Subtle>
            )}
          </div>
          <Subtle>{t('discord.privacy')}</Subtle>
          <div className="flex flex-wrap gap-2">
            {link.available && (
              <Button asChild variant="secondary" size="sm">
                <a href="/auth/discord">{t('discord.refresh')}</a>
              </Button>
            )}
            <Button
              variant="secondary"
              size="sm"
              disabled={unlink.isPending}
              onClick={() => {
                unlink.mutate();
              }}
            >
              {t('discord.unlink')}
            </Button>
          </div>
        </>
      ) : (
        <>
          <Subtle>{t('discord.privacy')}</Subtle>
          <div>
            <Button asChild variant="secondary" size="sm">
              <a href="/auth/discord">{t('discord.link')}</a>
            </Button>
          </div>
        </>
      )}
    </section>
  );
}
