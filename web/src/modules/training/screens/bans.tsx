import { Button } from '@ivao/atmosphere-react';
import { useNavigate, useParams, useRouteContext, useSearch } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { holdsPermissionAnywhere } from '../../../shared/api/bootstrap';
import { SchemaForm, describeProblem } from '../../../shared/forms';
import { DataList, col, type ColumnSpec } from '../../../shared/list';
import { ConfirmDialog, NotFound, PageShell, useNotice } from '../../../shared/ui';
import { bansListQuery, useGiveBan, useLiftBan, type BanRow, type TraineeBanDto } from '../api';
import { TRAINING_BAN } from '../permissions';
import {
  banFormSearchSchema,
  banSchema,
  bansSearchSchema,
  emptyBan,
  type BanFormValues,
  type BansSearch,
} from '../schemas';

import { BANS, banFormHref, traineeHref } from './path';

/**
 * The bans of the trainees (design M3 §2.9), over the server of A10a: the generated list — who, how the ban stands, since when and
 * until when, why, who gave it —, the generated form of a new one, and «lift the ban». A ban is never changed nor deleted: to change
 * one, it is lifted and another is given. Whoever does training reads them; whoever holds `Training.Ban` gives and lifts them, never
 * on themselves — the server says so on every write.
 *
 * The member and who gave the ban are the list's `person` columns: «Deleted person» for somebody whose data was erased, whose ban
 * stays in the history without them (A12b) — and with no path to open.
 */

const columns: readonly ColumnSpec<BanRow>[] = [
  col.person('trainee'),
  col.badge('status', 'training:bans'),
  col.date('createdAt', { sortable: true }),
  col.date('endsAt', { sortable: true }),
  col.text('reason'),
  col.person('givenBy'),
];

export function BansPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const navigate = useNavigate();
  const search = bansSearchSchema.parse(useSearch({ strict: false }));
  const bans = holdsPermissionAnywhere(bootstrap, TRAINING_BAN);
  const reader = bootstrap.user?.vid;

  const onSearchChange = (patch: Partial<BansSearch>) =>
    void navigate({
      search: ((previous: BansSearch) => ({ ...previous, ...patch })) as never,
      to: '.',
    });

  const create = bans ? (
    <Button asChild>
      <RouterAnchor href={banFormHref(search.vid)}>{t('training:bans.create')}</RouterAnchor>
    </Button>
  ) : null;

  return (
    <PageShell
      title={t('training:bans.title')}
      description={t('training:bans.description')}
      breadcrumb={[{ label: t('training:nav.section') }, { label: t('training:bans.title') }]}
      actions={create ?? undefined}
    >
      <DataList
        columns={columns}
        query={bansListQuery(search)}
        labels="training:bans"
        locale={i18n.language}
        defaultLocale={bootstrap.division.defaultLocale}
        timezone={bootstrap.division.timezone}
        search={search}
        onSearchChange={onSearchChange}
        actions={(row) => {
          const path = traineeHref(row.trainee.vid);

          return (
            <span className="flex flex-wrap gap-1">
              {path === null ? null : (
                <Button asChild variant="ghost" size="sm">
                  <RouterAnchor href={path}>{t('training:bans.toTrainee')}</RouterAnchor>
                </Button>
              )}
              {/* Offered where it may go through; the server asks again, and never lets anybody lift a ban of their own. */}
              {bans && row.holds && row.trainee.vid !== reader ? <LiftBan ban={row} /> : null}
            </span>
          );
        }}
        {...(create === null ? {} : { emptyAction: create })}
      />
    </PageShell>
  );
}

/**
 * «Lift the ban» (§2.9), asked first: the member may ask for trainings again at once, and the list and the path record who lifted it
 * and when. A ban somebody lifted or moved meanwhile (409) is read again.
 */
export function LiftBan({ ban }: { ban: TraineeBanDto }) {
  const { t, i18n } = useTranslation();
  const lift = useLiftBan();
  const notice = useNotice();

  return (
    <ConfirmDialog
      triggerText={t('training:bans.lift')}
      triggerVariant="secondary"
      title={t('training:bans.liftTitle')}
      description={t('training:bans.liftDescription')}
      confirmText={t('training:bans.liftConfirm')}
      confirmVariant="primary"
      disabled={lift.isPending}
      onConfirm={() =>
        lift.mutate(ban, {
          onSuccess: () => notice({ tone: 'success', title: t('training:bans.lifted') }),
          onError: (error) =>
            notice({
              tone: 'error',
              title: describeProblem(error, t, i18n.language) ?? t('errors.unknown'),
            }),
        })
      }
    />
  );
}

/**
 * A new ban (§2.9): the member, why, and until when — nothing, until somebody lifts it. Opened from a trainee's path, it has the
 * member written already and goes back there once given. A ban given is not edited: its address answers only `new`.
 */
export function BanForm() {
  const { t } = useTranslation();
  const { bootstrap } = useRouteContext({ from: '/_staff' });
  const navigate = useNavigate();
  const notice = useNotice();
  const { id = '' } = useParams({ strict: false });
  const { vid } = banFormSearchSchema.parse(useSearch({ strict: false }));
  const give = useGiveBan();

  if (id !== 'new' || !holdsPermissionAnywhere(bootstrap, TRAINING_BAN)) {
    return <NotFound />;
  }

  const back = (vid === undefined ? null : traineeHref(vid)) ?? BANS;
  const title = t('training:bans.create');

  return (
    <PageShell
      title={title}
      description={t('training:bans.formDescription')}
      breadcrumb={[
        { label: t('training:nav.section') },
        { label: t('training:bans.title'), to: BANS },
        { label: title },
      ]}
    >
      <SchemaForm<BanFormValues>
        schema={banSchema}
        defaults={emptyBan(vid)}
        locales={bootstrap.division.locales}
        labels="training:bans"
        division={{
          defaultLocale: bootstrap.division.defaultLocale,
          timezone: bootstrap.division.timezone,
        }}
        onSubmit={async (values) => {
          const given = await give.mutateAsync(values);
          notice({ tone: 'success', title: t('training:bans.given') });
          await navigate({ href: (vid === undefined ? null : traineeHref(given.trainee.vid)) ?? BANS });
        }}
        submitLabel={t('training:bans.give')}
        secondaryAction={
          <Button asChild variant="ghost">
            <RouterAnchor href={back}>{t('common.cancel')}</RouterAnchor>
          </Button>
        }
      />
    </PageShell>
  );
}
