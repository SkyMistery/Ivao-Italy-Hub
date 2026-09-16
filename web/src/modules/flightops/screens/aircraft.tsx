import { Button } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useNavigate, useParams } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { RouterAnchor } from '../../../app/layouts/RouterAnchor';
import { writableDepartments } from '../../../shared/api/bootstrap';
import { SchemaForm } from '../../../shared/forms';
import { useLocalized } from '../../../shared/i18n/useLocalized';
import { DataList, col, type ColumnSpec } from '../../../shared/list';
import { ConfirmDialog, PageShell } from '../../../shared/ui';
import {
  emptyGroup,
  emptyProfile,
  groupQuery,
  groupToFormValues,
  groupsListQuery,
  profileQuery,
  profileToFormValues,
  profilesListQuery,
  useDeleteGroup,
  useDeleteProfile,
  useSaveGroup,
  useSaveProfile,
  type AircraftGroupDto,
  type AircraftProfileDto,
} from '../api';
import { TOURS_MANAGE_AIRCRAFT } from '../permissions';
import { aircraftGroupSchema, aircraftProfileSchema } from '../schemas';

import { NewButton } from './NewButton';
import { keepingCurrent, useListSearch, useStaff, useTypeSuggestions } from './hooks';

/**
 * The aircraft data of the tours (design M2 §1.5, §8.7): profiles — how fast a type flies — and groups of
 * types. Lists and forms generated, like every back office screen of the hub; the routes are the module's,
 * registered from its manifest under the staff layout.
 */

const PROFILES = '/staff/tours/aircraft-profiles';
const GROUPS = '/staff/tours/aircraft-groups';

const profileColumns: readonly ColumnSpec<AircraftProfileDto>[] = [
  col.text('icaoType', { sortable: true }),
  col.number('cruiseTasKt', { sortable: true }),
  col.text('note'),
  col.date('updatedAt', { sortable: true }),
];

const groupColumns: readonly ColumnSpec<AircraftGroupDto>[] = [
  col.localized('name'),
  col.list('icaoTypes'),
  col.date('updatedAt', { sortable: true }),
];

export function AircraftProfilesPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useStaff();
  const { search, onSearchChange } = useListSearch();
  const writes = writableDepartments(bootstrap, TOURS_MANAGE_AIRCRAFT).length > 0;
  const create = writes ? (
    <NewButton href={`${PROFILES}/new`} label={t('flightops:aircraftProfiles.create')} />
  ) : null;

  return (
    <PageShell
      title={t('flightops:aircraftProfiles.title')}
      description={t('flightops:aircraftProfiles.description')}
      breadcrumb={[{ label: t('flightops:nav.section') }, { label: t('flightops:aircraftProfiles.title') }]}
      actions={create ?? undefined}
    >
      <DataList
        columns={profileColumns}
        query={profilesListQuery(search)}
        labels="flightops:aircraftProfiles"
        locale={i18n.language}
        defaultLocale={bootstrap.division.defaultLocale}
        timezone={bootstrap.division.timezone}
        search={search}
        onSearchChange={onSearchChange}
        actions={(row) =>
          writes ? (
            <Button asChild variant="ghost" size="sm">
              <RouterAnchor href={`${PROFILES}/${row.id}`}>{t('common.edit')}</RouterAnchor>
            </Button>
          ) : null
        }
        {...(create === null ? {} : { emptyAction: create })}
      />
    </PageShell>
  );
}

export function AircraftProfileForm() {
  const { t } = useTranslation();
  const { bootstrap } = useStaff();
  const id = String(useParams({ strict: false }).id ?? 'new');
  const navigate = useNavigate();
  const isNew = id === 'new';

  const profile = useQuery({ ...profileQuery(Number(id)), enabled: !isNew }).data ?? null;
  const save = useSaveProfile(isNew ? null : Number(id));
  const remove = useDeleteProfile();
  const { suggestions, onSuggestSearch } = useTypeSuggestions();
  const department = writableDepartments(bootstrap, TOURS_MANAGE_AIRCRAFT)[0];

  const back = () => void navigate({ href: PROFILES });

  if ((!isNew && profile === null) || department === undefined) {
    return null;
  }

  const title = isNew ? t('flightops:aircraftProfiles.create') : t('flightops:aircraftProfiles.edit');
  // The type being edited is offered too, or a closed field would put back the value it opened with.
  const offered = keepingCurrent(suggestions, profile === null ? [] : [profile.icaoType]);

  return (
    <PageShell
      title={title}
      breadcrumb={[
        { label: t('flightops:nav.section') },
        { label: t('flightops:aircraftProfiles.title'), to: PROFILES },
        { label: title },
      ]}
      actions={
        isNew ? undefined : (
          <ConfirmDialog
            triggerText={t('common.delete')}
            title={t('flightops:aircraftProfiles.delete.title')}
            description={t('flightops:aircraftProfiles.delete.description')}
            confirmText={t('common.delete')}
            disabled={remove.isPending}
            onConfirm={() => remove.mutate(Number(id), { onSuccess: back })}
          />
        )
      }
    >
      <SchemaForm
        schema={aircraftProfileSchema(offered)}
        defaults={profile === null ? emptyProfile(department) : profileToFormValues(profile)}
        locales={bootstrap.division.locales}
        labels="flightops:aircraftProfiles"
        onSuggestSearch={onSuggestSearch}
        onSubmit={async (values) => {
          await save.mutateAsync(values);
          back();
        }}
        submitLabel={t('common.save')}
        secondaryAction={
          <Button asChild variant="ghost">
            <RouterAnchor href={PROFILES}>{t('common.cancel')}</RouterAnchor>
          </Button>
        }
      />
    </PageShell>
  );
}

export function AircraftGroupsPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = useStaff();
  const { search, onSearchChange } = useListSearch();
  const writes = writableDepartments(bootstrap, TOURS_MANAGE_AIRCRAFT).length > 0;
  const create = writes ? (
    <NewButton href={`${GROUPS}/new`} label={t('flightops:aircraftGroups.create')} />
  ) : null;

  return (
    <PageShell
      title={t('flightops:aircraftGroups.title')}
      description={t('flightops:aircraftGroups.description')}
      breadcrumb={[{ label: t('flightops:nav.section') }, { label: t('flightops:aircraftGroups.title') }]}
      actions={create ?? undefined}
    >
      <DataList
        columns={groupColumns}
        query={groupsListQuery(search)}
        labels="flightops:aircraftGroups"
        locale={i18n.language}
        defaultLocale={bootstrap.division.defaultLocale}
        timezone={bootstrap.division.timezone}
        search={search}
        onSearchChange={onSearchChange}
        actions={(row) =>
          writes ? (
            <Button asChild variant="ghost" size="sm">
              <RouterAnchor href={`${GROUPS}/${row.id}`}>{t('common.edit')}</RouterAnchor>
            </Button>
          ) : null
        }
        {...(create === null ? {} : { emptyAction: create })}
      />
    </PageShell>
  );
}

export function AircraftGroupForm() {
  const { t } = useTranslation();
  const read = useLocalized();
  const { bootstrap } = useStaff();
  const id = String(useParams({ strict: false }).id ?? 'new');
  const navigate = useNavigate();
  const isNew = id === 'new';
  const locales = bootstrap.division.locales;

  const group = useQuery({ ...groupQuery(Number(id)), enabled: !isNew }).data ?? null;
  const save = useSaveGroup(isNew ? null : Number(id));
  const remove = useDeleteGroup();
  const { suggestions, onSuggestSearch } = useTypeSuggestions();
  const department = writableDepartments(bootstrap, TOURS_MANAGE_AIRCRAFT)[0];

  const back = () => void navigate({ href: GROUPS });

  if ((!isNew && group === null) || department === undefined) {
    return null;
  }

  const title = isNew
    ? t('flightops:aircraftGroups.create')
    : read(group?.name ?? {}) || t('flightops:aircraftGroups.edit');
  const offered = keepingCurrent(suggestions, group?.icaoTypes ?? []);

  return (
    <PageShell
      title={title}
      breadcrumb={[
        { label: t('flightops:nav.section') },
        { label: t('flightops:aircraftGroups.title'), to: GROUPS },
        { label: title },
      ]}
      actions={
        isNew ? undefined : (
          <ConfirmDialog
            triggerText={t('common.delete')}
            title={t('flightops:aircraftGroups.delete.title')}
            description={t('flightops:aircraftGroups.delete.description')}
            confirmText={t('common.delete')}
            disabled={remove.isPending}
            onConfirm={() => remove.mutate(Number(id), { onSuccess: back })}
          />
        )
      }
    >
      <SchemaForm
        schema={aircraftGroupSchema(offered)}
        defaults={group === null ? emptyGroup(department, locales) : groupToFormValues(group, locales)}
        locales={locales}
        labels="flightops:aircraftGroups"
        onSuggestSearch={onSuggestSearch}
        onSubmit={async (values) => {
          await save.mutateAsync(values);
          back();
        }}
        submitLabel={t('common.save')}
        secondaryAction={
          <Button asChild variant="ghost">
            <RouterAnchor href={GROUPS}>{t('common.cancel')}</RouterAnchor>
          </Button>
        }
      />
    </PageShell>
  );
}
