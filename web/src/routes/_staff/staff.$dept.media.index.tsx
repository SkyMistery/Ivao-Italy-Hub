import { Button } from '@ivao/atmosphere-react';
import { Link, createFileRoute } from '@tanstack/react-router';
import { Upload } from 'lucide-react';
import { useRef } from 'react';
import { useTranslation } from 'react-i18next';

import { mediaColumns } from '../../features/media/list';
import { useUploadMedia } from '../../features/media/mutations';
import { mediaListQuery } from '../../features/media/queries';
import { ProblemAlert, describeProblem } from '../../shared/forms';
import { DataList, listSearchSchema } from '../../shared/list';
import { PageShell } from '../../shared/ui';

/**
 * The library of one department: the same list engine as every other screen, with one control the
 * generator has no notion of — a file input, because uploading is the one thing in this hub that is
 * not a JSON payload.
 *
 * A file arrives with no alternative text, and the screen goes straight to its metadata form for
 * that reason: a picture nobody can hear is not finished.
 */
export const Route = createFileRoute('/_staff/staff/$dept/media/')({
  validateSearch: listSearchSchema,
  loaderDeps: ({ search }) => search,
  loader: ({ context, deps, params }) =>
    context.queryClient.ensureQueryData(mediaListQuery(params.dept, deps)),
  component: MediaLibraryPage,
});

function MediaLibraryPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const { dept } = Route.useParams();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();

  const division = bootstrap.division;
  const chooser = useRef<HTMLInputElement>(null);

  const upload = useUploadMedia();
  const refusal = describeProblem(upload.error, t, i18n.language);

  const choose = (file: File | undefined) => {
    if (file === undefined) {
      return;
    }

    upload.mutate(
      { file, ownerDepartment: dept },
      {
        onSuccess: (media) =>
          void navigate({ to: '/staff/$dept/media/$id', params: { dept, id: String(media.id) } }),
      },
    );
  };

  return (
    <PageShell
      title={t('media.title')}
      description={t('media.description')}
      breadcrumb={[{ label: dept }, { label: t('media.title') }]}
      actions={
        <Button disabled={upload.isPending} onClick={() => chooser.current?.click()}>
          <Upload aria-hidden className="mr-2 size-4" />
          {t('media.upload')}
        </Button>
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
        columns={mediaColumns}
        query={mediaListQuery(dept, search)}
        labels="media"
        locale={i18n.language}
        defaultLocale={division.defaultLocale}
        timezone={division.timezone}
        search={search}
        onSearchChange={(patch) => void navigate({ search: (previous) => ({ ...previous, ...patch }) })}
        actions={(row) => (
          <Button asChild variant="ghost" size="sm">
            <Link to="/staff/$dept/media/$id" params={{ dept, id: String(row.id) }}>
              {t('common.edit')}
            </Link>
          </Button>
        )}
      />
    </PageShell>
  );
}
