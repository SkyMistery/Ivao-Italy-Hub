import { Button } from '@ivao/atmosphere-react';
import { Link, createFileRoute, redirect } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';
import { z } from 'zod';

import { awardSignalColumns } from '../../features/awards/list';
import { useMoveSignal } from '../../features/awards/mutations';
import { AWARD_SIGNAL_STATUSES, awardSignalsListQuery } from '../../features/awards/queries';
import { holdsPermissionAnywhere } from '../../shared/api/bootstrap';
import { DataList, ListFilter, listSearchSchema } from '../../shared/list';
import { PageShell } from '../../shared/ui';

/** Global: the queue belongs to whoever assigns, whatever their department. */
const AWARDS_ASSIGN = 'Awards.Assign';

/**
 * The queue of awards (M2, T4b): the members the modules point out — a tour completed — each with the
 * award its row proposes. Nothing here assigns anything by itself (plan §9.1): "assign" opens the form
 * of an assignment filled from the line, and saving it handles the line in the same write; "dismiss"
 * takes a line out, and a dismissed line can be put back.
 *
 * The waiting lines are what the server lists unless the status is named, so the filter left empty
 * means the queue as it should be worked.
 */
export const Route = createFileRoute('/_staff/staff/awards/queue')({
  validateSearch: listSearchSchema.extend({ status: z.enum(AWARD_SIGNAL_STATUSES).optional() }),
  beforeLoad: ({ context }) => {
    if (!holdsPermissionAnywhere(context.bootstrap, AWARDS_ASSIGN)) {
      throw redirect({ to: '/forbidden' });
    }
  },
  loaderDeps: ({ search }) => search,
  loader: ({ context, deps }) =>
    context.queryClient.ensureQueryData(awardSignalsListQuery(deps.status, deps)),
  component: AwardQueuePage,
});

function AwardQueuePage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();
  const move = useMoveSignal();

  const division = bootstrap.division;

  return (
    <PageShell
      title={t('awardSignals.title')}
      description={t('awardSignals.description')}
      breadcrumb={[{ label: t('awards.section') }, { label: t('awardSignals.title') }]}
    >
      <DataList
        columns={awardSignalColumns}
        query={awardSignalsListQuery(search.status, search)}
        labels="awardSignals"
        locale={i18n.language}
        defaultLocale={division.defaultLocale}
        timezone={division.timezone}
        search={search}
        onSearchChange={(patch) => void navigate({ search: (previous) => ({ ...previous, ...patch }) })}
        toolbar={
          <ListFilter
            id="awardSignalsStatus"
            label={t('awardSignals.fields.status')}
            none={t('awardSignals.options.status.Pending')}
            value={search.status}
            onChange={(value) =>
              void navigate({
                search: (previous) => ({
                  ...previous,
                  status: value as (typeof AWARD_SIGNAL_STATUSES)[number] | undefined,
                  page: 1,
                }),
              })
            }
            items={AWARD_SIGNAL_STATUSES.filter((status) => status !== 'Pending').map((status) => ({
              value: status,
              label: t(`awardSignals.options.status.${status}`),
            }))}
          />
        }
        actions={(row) =>
          row.status === 'Pending' ? (
            <div className="flex gap-2">
              <Button asChild variant="ghost" size="sm">
                <Link to="/staff/awards/assignments/$id" params={{ id: 'new' }} search={{ signal: row.id }}>
                  {t('awardSignals.assign')}
                </Link>
              </Button>
              <Button
                variant="ghost"
                size="sm"
                disabled={move.isPending}
                onClick={() => move.mutate({ id: row.id, status: 'Dismissed' })}
              >
                {t('awardSignals.dismiss')}
              </Button>
            </div>
          ) : row.status === 'Dismissed' ? (
            <Button
              variant="ghost"
              size="sm"
              disabled={move.isPending}
              onClick={() => move.mutate({ id: row.id, status: 'Pending' })}
            >
              {t('awardSignals.restore')}
            </Button>
          ) : null
        }
      />
    </PageShell>
  );
}
