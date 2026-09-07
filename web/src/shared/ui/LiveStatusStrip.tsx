import { useQuery } from '@tanstack/react-query';
import { Plane, TowerControl } from 'lucide-react';
import type { ComponentType } from 'react';
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
    <div className="border-border bg-muted/30 border-b">
      <div className="mx-auto flex w-full max-w-6xl flex-wrap items-center gap-x-6 gap-y-2 px-4 py-2.5">
        {/* ⚠️ The words of the title are a direct child of this span, and the dot is its sibling
            rather than its wrapper. `web/e2e/live-status.spec.ts` finds the band by going two
            levels up from the title, so a wrapper here would have it measure the reading column
            instead of the band — and the measurement is the only thing that says where the strip
            is. */}
        <span className="text-foreground flex items-center gap-2 text-xs font-semibold tracking-wide uppercase">
          <Pulse />
          {t('liveStatus.title')}
        </span>

        <span aria-hidden className="bg-border hidden h-6 w-px sm:block" />

        {/* The words are the block's own: the strip and `networkStats` count the same two things,
            and one set of figures deserves one set of words (CLAUDE.md §2). Written as a number and
            a caption rather than as a sentence with a plural, because a plural key is one
            `pnpm i18n:check` cannot see — it looks for `_one` and `_other`, not for the key in the
            source. */}
        <Figure
          Icon={TowerControl}
          value={count(value('divisionAtc'))}
          caption={t('blocks.networkStats.captions.divisionAtc')}
        />
        <Figure
          Icon={Plane}
          value={count(value('divisionPilots'))}
          caption={t('blocks.networkStats.captions.divisionPilots')}
        />

        {/* When the network counted, and never when the hub asked: a reader who sees a number wants
            to know how old it is, and the two are not the same minute. */}
        <span className="text-muted-foreground ml-auto text-xs">
          {t('liveStatus.updatedAt', { at: moment(status.updatedAt) })}
        </span>
      </div>
    </div>
  );
}

/**
 * One figure: the number first and large, the word after it and small.
 *
 * ⚠️ This is the whole of what Carmine asked for after the demo — "too flat, it does not make the
 * information stand out". The strip used to draw the number at the same size as everything else, in
 * a line of small grey text, so the two things it exists to say weighed exactly as much as the
 * label beside them and as the timestamp at the end. Nothing has been added: what changed is that
 * the number is now the loudest thing in the band, the words around it the quietest, and the icon
 * lets a reader find "who is controlling" without reading at all.
 */
function Figure({
  Icon,
  value,
  caption,
}: {
  Icon: ComponentType<{ className?: string; 'aria-hidden'?: boolean }>;
  value: string;
  caption: string;
}) {
  return (
    <span className="flex items-center gap-2">
      <Icon aria-hidden className="text-muted-foreground size-4 shrink-0" />
      <span className="text-foreground text-lg leading-none font-semibold tabular-nums">{value}</span>
      <span className="text-muted-foreground text-xs">{caption}</span>
    </span>
  );
}

/**
 * The dot that says these numbers are of this minute. It breathes, because a still dot beside the
 * word "live" says nothing a full stop would not — and it stops breathing for a reader who has
 * asked their system for less motion, which is what `motion-safe` is.
 */
function Pulse() {
  return (
    <span aria-hidden className="relative flex size-2 shrink-0">
      <span className="motion-safe:animate-ping absolute inline-flex size-full rounded-full bg-green-500 opacity-60" />
      <span className="relative inline-flex size-2 rounded-full bg-green-600" />
    </span>
  );
}
