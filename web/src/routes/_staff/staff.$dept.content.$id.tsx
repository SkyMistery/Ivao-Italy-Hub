import { createFileRoute, useNavigate } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { ContentEditor } from '../../features/content/ContentEditor';
import {
  useCreateContent,
  useDeleteContent,
  usePublishContent,
  useUpdateContent,
} from '../../features/content/mutations';
import { contentQuery, type ContentDetailDto } from '../../features/content/queries';
import { deptParam } from '../../shared/api/department';
import { PageShell } from '../../shared/ui';

/**
 * One page, in the editor. `new` is a row that does not exist yet: it is created by the first save,
 * which is also when it gets an address of its own — until then there is nothing to publish and
 * nothing to delete, and the editor says so by being handed nothing to call.
 */
export const Route = createFileRoute('/_staff/staff/$dept/content/$id')({
  loader: async ({ context, params }): Promise<ContentDetailDto | null> =>
    params.id === 'new' ? null : context.queryClient.ensureQueryData(contentQuery(Number(params.id))),
  component: ContentForm,
});

function ContentForm() {
  const { t } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const { dept, id } = Route.useParams();
  const navigate = useNavigate();

  const isNew = id === 'new';
  const locales = bootstrap.division.locales;
  const content = Route.useLoaderData();

  const create = useCreateContent();
  const update = useUpdateContent(Number(id));
  const remove = useDeleteContent();
  const publish = usePublishContent(Number(id));

  const backToList = () => void navigate({ to: '/staff/$dept/content', params: { dept } });

  return (
    <PageShell
      title={isNew ? t('content.create') : t('content.edit')}
      breadcrumb={[
        { label: dept },
        { label: t('content.title'), to: `/staff/${deptParam.format(dept)}/content` },
        { label: isNew ? t('content.create') : t('content.edit') },
      ]}
    >
      <ContentEditor
        content={content}
        department={dept}
        locales={locales}
        busy={create.isPending || update.isPending || publish.isPending || remove.isPending}
        publishError={publish.error}
        onSave={async (values, body) => {
          const seo = content?.seo ?? null;

          if (isNew) {
            const created = await create.mutateAsync({ values, body, seo });
            await navigate({ to: '/staff/$dept/content/$id', params: { dept, id: String(created.id) } });
            return created;
          }

          return update.mutateAsync({ values, body, seo });
        }}
        onPublish={isNew ? null : () => publish.mutate(null)}
        onDelete={isNew ? null : () => remove.mutate(Number(id), { onSuccess: backToList })}
      />
    </PageShell>
  );
}
