import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { bootstrapQuery } from '../../../features/me/queries';
import type { BlockComponentProps } from '../../../shared/modules';
import type { PublicSessionDto } from '../api';
import { UpcomingSessionList } from '../screens/public';

/**
 * `training.upcomingSessions` (design M3 §4.1, §4.3; note il-training-in-pubblico): the sessions still to be held, the soonest first,
 * on a page of the site or on a dashboard — the list `/training` draws, from the same component: a block that drew its own would be
 * the same screen written twice. Who is in a session only when the server said it, to a signed in reader. Always live; its other
 * half is `UpcomingSessionsProvider`, which reads what `/api/training/sessions` reads.
 */

/** What `UpcomingSessionsProvider` answers with: the sessions, and whether the reader is signed in. */
export interface UpcomingSessionsData {
  signedIn?: boolean;
  items?: PublicSessionDto[];
}

export function UpcomingSessionsBlock({ data }: BlockComponentProps) {
  const { t } = useTranslation();
  const { data: bootstrap } = useQuery(bootstrapQuery);
  const answer = data as UpcomingSessionsData | null | undefined;
  const items = answer?.items;

  if (items === undefined || bootstrap === undefined) {
    return <p className="text-muted-foreground text-sm">{t('common.loading')}</p>;
  }

  if (items.length === 0) {
    return <p className="text-muted-foreground text-sm">{t('training:public.none')}</p>;
  }

  return (
    <UpcomingSessionList
      sessions={items}
      timezone={bootstrap.division.timezone}
      signedIn={answer?.signedIn === true}
    />
  );
}
