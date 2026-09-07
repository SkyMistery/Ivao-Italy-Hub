import { Button, Subtle } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { Link, createFileRoute, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { toFormValues, useDeleteMedia, useUpdateMedia } from '../../features/media/mutations';
import { mediaQuery, mediaUsageQuery, type MediaDetailDto } from '../../features/media/queries';
import { mediaSchema, type MediaFormValues } from '../../features/media/schema';
import { deptParam } from '../../shared/api/department';
import { SchemaForm } from '../../shared/forms';
import { resolveLocalized } from '../../shared/i18n/localized';
import { ConfirmDialog, PageShell } from '../../shared/ui';

/**
 * The metadata of one file. There is no `new` here and there is no create: a media is born from an
 * upload, with its bytes already on disk, so this screen only ever edits one that exists.
 *
 * There is no field in this file either — `SchemaForm` reads `features/media/schema.ts` — and the
 * two things that are not fields are the preview and the answer to "where is this used?", which is
 * what somebody about to delete a file needs to see first (design M1 §2).
 */
export const Route = createFileRoute('/_staff/staff/$dept/media/$id')({
  loader: ({ context, params }): Promise<MediaDetailDto> =>
    context.queryClient.ensureQueryData(mediaQuery(Number(params.id))),
  component: MediaForm,
});

function MediaForm() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const { dept, id } = Route.useParams();
  const navigate = useNavigate();

  const locales = bootstrap.division.locales;
  // The row as it stands now. ⚠️ Not `Route.useLoaderData()`: a loader runs on navigation and
  // never again, so after one save the screen still held the `rowVersion` from when the page
  // opened, and the second save was answered 409 — blaming somebody who does not exist. The loader
  // above is the *preload*; what the screen reads is the query it filled (design M0 §7.3).
  const media = useQuery(mediaQuery(Number(id))).data;

  const update = useUpdateMedia(Number(id));
  const remove = useDeleteMedia();

  // Asked as the screen opens rather than when the delete button is pressed: a confirmation dialog
  // that has to fetch before it can warn is a dialog people click through.
  const usage = useQuery(mediaUsageQuery(Number(id)));
  const usedBy = usage.data?.items ?? [];

  if (media === undefined) {
    // The loader has already put it in the cache, so this is the compiler asking rather than a
    // state a reader reaches.
    return null;
  }

  const backToLibrary = () => void navigate({ to: '/staff/$dept/media', params: { dept } });

  const submit = async (values: MediaFormValues) => {
    await update.mutateAsync(values);
    backToLibrary();
  };

  return (
    <PageShell
      title={media.fileName}
      description={t('media.description')}
      breadcrumb={[
        { label: dept },
        { label: t('media.title'), to: `/staff/${deptParam.format(dept)}/media` },
        { label: media.fileName },
      ]}
      actions={
        <ConfirmDialog
          triggerText={t('common.delete')}
          title={t('media.delete.title')}
          description={
            usedBy.length === 0
              ? t('media.delete.description')
              : t('media.delete.inUse', { count: usedBy.length })
          }
          confirmText={t('common.delete')}
          disabled={remove.isPending}
          onConfirm={() => remove.mutate(Number(id), { onSuccess: backToLibrary })}
        />
      }
    >
      <div className="flex flex-col gap-6">
        <MediaPreview media={media} locale={i18n.language} defaultLocale={bootstrap.division.defaultLocale} />

        {usedBy.length === 0 ? null : (
          <section className="border-border flex flex-col gap-2 rounded-md border p-4">
            <Subtle>{t('media.usedBy', { count: usedBy.length })}</Subtle>
            <ul className="flex flex-col gap-1 text-sm">
              {usedBy.map((content) => (
                <li key={content.id}>
                  <Link
                    to="/staff/$dept/content/$id"
                    params={{ dept: content.ownerDepartment, id: String(content.id) }}
                    className="underline"
                  >
                    {resolveLocalized(content.title, i18n.language, bootstrap.division.defaultLocale)}
                  </Link>
                </li>
              ))}
            </ul>
          </section>
        )}

        <SchemaForm
          schema={mediaSchema}
          defaults={toFormValues(media, locales)}
          locales={locales}
          labels="media"
          onSubmit={submit}
          submitLabel={t('common.save')}
          secondaryAction={
            <Button asChild variant="ghost">
              <Link to="/staff/$dept/media" params={{ dept }}>
                {t('common.cancel')}
              </Link>
            </Button>
          }
        />
      </div>
    </PageShell>
  );
}

/**
 * What the file looks like, and the three facts a name does not carry. An image shows itself; a
 * document says what it is, because a PDF drawn as a broken image reads like a failed upload.
 */
function MediaPreview({
  media,
  locale,
  defaultLocale,
}: {
  media: MediaDetailDto;
  locale: string;
  defaultLocale: string;
}) {
  const { t } = useTranslation();
  const written = resolveLocalized(media.alt, locale, defaultLocale);

  return (
    <section className="border-border bg-card flex flex-wrap items-center gap-6 rounded-md border p-4">
      {media.contentType.startsWith('image/') ? (
        <img
          src={media.url}
          alt={written === '' ? media.fileName : written}
          // A box of a fixed size rather than one the picture decides: the library holds anything
          // from an eight pixel icon to a photograph straight from a camera, and a preview whose
          // height is the file's own would make this screen a different shape for every row.
          className="bg-body size-40 shrink-0 rounded object-contain"
        />
      ) : null}

      <dl className="flex flex-col gap-1 text-sm">
        <Fact label={t('media.fields.contentType')} value={media.contentType} />
        <Fact label={t('media.fields.byteSize')} value={String(media.byteSize)} />
        {media.width === null || media.height === null ? null : (
          <Fact label={t('media.fields.dimensions')} value={`${media.width} × ${media.height}`} />
        )}
        <Fact label={t('media.fields.url')} value={media.url} />
      </dl>
    </section>
  );
}

function Fact({ label, value }: { label: string; value: string }) {
  return (
    <div className="flex gap-2">
      <dt className="text-muted-foreground">{label}</dt>
      <dd className="text-foreground">{value}</dd>
    </div>
  );
}
