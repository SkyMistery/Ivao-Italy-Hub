import { Button, Label, Select } from '@ivao/atmosphere-react';
import { Link, createFileRoute } from '@tanstack/react-router';
import { Upload } from 'lucide-react';
import { useRef, useState } from 'react';
import { useTranslation } from 'react-i18next';

import { mediaColumns } from '../../features/media/list';
import { useUploadMedia } from '../../features/media/mutations';
import { mediaListQuery, type MediaListDto } from '../../features/media/queries';
import {
  holdsPermission,
  reachableDepartments,
  writableDepartments,
  type Department,
} from '../../shared/api/bootstrap';
import { ProblemAlert, describeProblem } from '../../shared/forms';
import { DataList, ListFilter, col, departmentListSearchSchema } from '../../shared/list';
import { PageShell, useNotice } from '../../shared/ui';

/** What changing a file asks for, on the department of the row. */
const MEDIA_EDIT = 'Media.Edit';

/**
 * The library: every file this person may use — their departments' and the public ones of the others
 * (note 2026-09-13-contenuti-centralizzati, 3.4) — with the department as a filter. The same list
 * engine as every other screen, with one control the
 * generator has no notion of — a file input, because uploading is the one thing in this hub that is
 * not a JSON payload.
 *
 * A file arrives with no alternative text, and the screen goes straight to its metadata form for
 * that reason: a picture nobody can hear is not finished.
 */
export const Route = createFileRoute('/_staff/staff/media/')({
  validateSearch: departmentListSearchSchema,
  loaderDeps: ({ search }) => search,
  loader: ({ context, deps }) => context.queryClient.ensureQueryData(mediaListQuery(deps.department, deps)),
  component: MediaLibraryPage,
});

function MediaLibraryPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();

  const division = bootstrap.division;
  const reachable = reachableDepartments(bootstrap);
  const writable = writableDepartments(bootstrap, MEDIA_EDIT);

  // Where an upload goes: the department of the filter when this person writes in it, the only one
  // they write in, or the one they choose beside the button.
  const [chosen, setChosen] = useState<Department | undefined>(undefined);
  const fixed =
    search.department !== undefined && writable.includes(search.department) ? search.department : undefined;
  const target = fixed ?? (writable.length === 1 ? writable[0] : (chosen ?? writable[0]));
  const chooser = useRef<HTMLInputElement>(null);

  const upload = useUploadMedia();
  const notice = useNotice();
  const refusal = describeProblem(upload.error, t, i18n.language);

  const choose = (file: File | undefined) => {
    if (file === undefined || target === undefined) {
      return;
    }

    upload.mutate(
      { file, ownerDepartment: target },
      {
        onSuccess: ({ media, alreadyHere }) => {
          // The same bytes were already here: the screen goes to the row that exists and says so,
          // because "I uploaded a file and it opened somebody else's alt text" needs a sentence.
          if (alreadyHere) {
            notice({ tone: 'info', title: t('media.alreadyHere') });
          }

          void navigate({ to: '/staff/media/$id', params: { id: String(media.id) } });
        },
      },
    );
  };

  return (
    <PageShell
      title={t('media.title')}
      description={t('media.description')}
      breadcrumb={[{ label: t('backOffice.content') }, { label: t('media.title') }]}
      actions={
        target === undefined ? undefined : (
          <div className="flex flex-wrap items-end gap-2">
            {fixed === undefined && writable.length > 1 ? (
              <div className="flex flex-col gap-1">
                <Label htmlFor="createIn">{t('backOffice.createIn')}</Label>
                <Select
                  id="createIn"
                  value={target}
                  onValueChange={(value) => setChosen(value as Department)}
                  items={writable.map((code) => ({ value: code, label: t(`departments.${code}`) }))}
                />
              </div>
            ) : null}
            <Button disabled={upload.isPending} onClick={() => chooser.current?.click()}>
              <Upload aria-hidden className="mr-2 size-4" />
              {t('media.upload')}
            </Button>
          </div>
        )
      }
    >
      <input
        ref={chooser}
        type="file"
        className="hidden"
        aria-label={t('media.upload')}
        onChange={(event) => {
          choose(event.target.files?.[0]);
          // Cleared so that choosing the same file twice in a row is still a change event.
          event.target.value = '';
        }}
      />

      {refusal === null ? null : (
        <div className="mb-4">
          <ProblemAlert summary={refusal} />
        </div>
      )}

      <DataList
        columns={
          reachable.length > 1 && search.department === undefined
            ? [
                ...mediaColumns.slice(0, 1),
                col.department<MediaListDto>('ownerDepartment'),
                ...mediaColumns.slice(1),
              ]
            : mediaColumns
        }
        query={mediaListQuery(search.department, search)}
        labels="media"
        locale={i18n.language}
        defaultLocale={division.defaultLocale}
        timezone={division.timezone}
        search={search}
        onSearchChange={(patch) => void navigate({ search: (previous) => ({ ...previous, ...patch }) })}
        toolbar={
          reachable.length > 1 ? (
            <ListFilter
              id="mediaDepartment"
              label={t('backOffice.filters.department')}
              none={t('backOffice.filters.allDepartments')}
              value={search.department}
              onChange={(value) =>
                void navigate({
                  search: (previous) => ({
                    ...previous,
                    department: value as Department | undefined,
                    page: 1,
                  }),
                })
              }
              items={reachable.map((code) => ({ value: code, label: t(`departments.${code}`) }))}
            />
          ) : undefined
        }
        // A public file of another department is here to be used, not changed: no "edit" on it.
        actions={(row) =>
          holdsPermission(bootstrap, MEDIA_EDIT, row.ownerDepartment) ? (
            <Button asChild variant="ghost" size="sm">
              <Link to="/staff/media/$id" params={{ id: String(row.id) }}>
                {t('common.edit')}
              </Link>
            </Button>
          ) : null
        }
      />
    </PageShell>
  );
}
