import { useQuery } from '@tanstack/react-query';
import { Radio } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { blockDataQuery } from '../../blocks/data';
import { useMoment } from '../i18n/useMoment';

/**
 * Who is on the network right now, in one line across the top of the public site (design M1 §6.2).
 *
 * ⚠️ **Polling, not a websocket.** A strip that refreshes once a minute is more than enough for a
 * number that changes by ones, and the Plesk proxy in front of this application is not the place for
 * a persistent connection per reader (plan §16, §14). The refresh interval matches the cache the
 * server keeps on the same reading, so a reader arriving on the minute costs nothing at all.
 *
 * ⚠️ **No endpoint of its own.** It asks `/api/blocks/data/networkStats` — the provider of G4, which
 * is anonymous, always live and already the answer to this exact question. A strip with a route of
 * its own would be a second way of asking one thing (CLAUDE.md §2).
 *
 * What it does when the network cannot be reached is the whole of its honesty: **it draws nothing**.
 * React Query keeps the last answer across a failed refresh, so a blip leaves the previous numbers
 * on screen; a network that is down from the first request leaves no strip rather than four zeroes,
 * which would be the page saying "nobody is flying" when it means "I could not ask".
 */

/** What the strip asks for: the two figures about this division, and no list of positions. */
const QUESTION = {
  figures: [{ figure: 'divisionAtc' }, { figure: 'divisionPilots' }],
  showPositions: false,
};

/** How often it asks again. The same minute the server caches the reading for. */
const EVERY_MINUTE = 60_000;

/** What `NetworkStatsProvider` answers with, read defensively: it is JSON off the wire. */
export interface LiveNetworkStatus {
  updatedAt?: string | null;
  figures?: { figure?: string; value?: number }[];
}

/**
 * <paramref name="status"/> is the seam the gallery mounts it with, and it is the one blocks already
 * have: `/staff/admin/ui-kit` draws a data block with its `exampleData` rather than with an answer
 * from the server, for the reason written there — a gallery that called the API would show whatever
 * this installation happens to hold today, or nothing at all on a fresh one. Given a status, the
 * strip draws it and asks nobody; that is the only thing the prop does.
 */
export function LiveStatusStrip({ status: sample }: { status?: LiveNetworkStatus } = {}) {
  const { t, i18n } = useTranslation();
  const moment = useMoment();

  const { data } = useQuery({
    ...blockDataQuery('networkStats', QUESTION),
    refetchInterval: EVERY_MINUTE,
    // The strip is decoration on somebody else's page: a network that is down must cost one attempt
    // a minute, not three in a row every minute.
    retry: false,
    // Never asked when the caller brought the answer. The hook still runs, because a hook that runs
    // sometimes is a hook that breaks the next render.
    enabled: sample === undefined,
  });

  const status = sample ?? (data as LiveNetworkStatus | null | undefined);

  // Nothing to say, so nothing said. This covers the first paint, a network that has never
  // answered, and the reading the server marks as "I could not ask" — all three are the same
  // sentence, and none of them is worth a line across the top of the site.
  if (!status?.updatedAt) {
    return null;
  }

  const value = (figure: string) => status.figures?.find((entry) => entry.figure === figure)?.value ?? 0;
  const count = (n: number) => new Intl.NumberFormat(i18n.language).format(n);

  return (
    <div className="border-border bg-muted/40 border-b">
      <div className="text-muted-foreground mx-auto flex w-full max-w-6xl flex-wrap items-center gap-x-4 gap-y-1 px-4 py-2 text-sm">
        <span className="text-foreground flex items-center gap-2 font-medium">
          <Radio aria-hidden className="size-4" />
          {t('liveStatus.title')}
        </span>

        {/* The words are the block's own: the strip and `networkStats` count the same two things,
            and one set of figures deserves one set of words (CLAUDE.md §2). Written as a number and
            a caption rather than as a sentence with a plural, because a plural key is one
            `pnpm i18n:check` cannot see — it looks for `_one` and `_other`, not for the key in the
            source. */}
        <span className="tabular-nums">
          <span className="text-foreground font-semibold">{count(value('divisionAtc'))}</span>{' '}
          {t('blocks.networkStats.captions.divisionAtc')}
        </span>
        <span className="tabular-nums">
          <span className="text-foreground font-semibold">{count(value('divisionPilots'))}</span>{' '}
          {t('blocks.networkStats.captions.divisionPilots')}
        </span>

        {/* When the network counted, and never when the hub asked: a reader who sees a number wants
            to know how old it is, and the two are not the same minute. */}
        <span className="ml-auto text-xs">{t('liveStatus.updatedAt', { at: moment(status.updatedAt) })}</span>
      </div>
    </div>
  );
}
