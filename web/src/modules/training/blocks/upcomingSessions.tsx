import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { bootstrapQuery } from '../../../features/me/queries';
import type { BlockComponentProps } from '../../../shared/modules';
import type { PublicExamDto, PublicSessionDto } from '../api';
import { UpcomingSessionList } from '../screens/public';

/**
 * `training.upcomingSessions` (design M3 §4.1, §4.3; note il-training-in-pubblico): the sessions still to be held and the exams still
 * to come (A10c), the soonest first, on a page of the site or on a dashboard — the list `/training` draws, from the same component: a
 * block that drew its own would be the same screen written twice. At most as many as its property asks, of the two together. Who is in
 * them only when the server said it, to a signed in reader. Always live; its other half is `UpcomingSessionsProvider`, which reads what
 * `/api/training/sessions` and `/api/training/sessions/exams` read.
 */

/** What `UpcomingSessionsProvider` answers with: the sessions, the exams, and whether the reader is signed in. */
export interface UpcomingSessionsData {
  signedIn?: boolean;
  items?: PublicSessionDto[];
  exams?: PublicExamDto[];
}

export function UpcomingSessionsBlock({ props, data }: BlockComponentProps) {
  const { t } = useTranslation();
  const { data: bootstrap } = useQuery(bootstrapQuery);
  const answer = data as UpcomingSessionsData | null | undefined;
  const items = answer?.items;
  const exams = answer?.exams ?? [];

  if (items === undefined || bootstrap === undefined) {
    return <p className="text-muted-foreground text-sm">{t('common.loading')}</p>;
  }

  if (items.length === 0 && exams.length === 0) {
    return <p className="text-muted-foreground text-sm">{t('training:public.none')}</p>;
  }

  return (
    <UpcomingSessionList
      sessions={items}
      exams={exams}
      {...(typeof props.limit === 'number' ? { limit: props.limit } : {})}
      timezone={bootstrap.division.timezone}
      signedIn={answer?.signedIn === true}
    />
  );
}
