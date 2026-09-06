import { createFileRoute, useNavigate } from '@tanstack/react-router';

import { ContentFormScreen } from '../../features/content/ContentFormScreen';
import { CONTENT_KINDS } from '../../features/content/kinds';
import { contentQuery, type ContentDetailDto } from '../../features/content/queries';
import { deptParam } from '../../shared/api/department';

/**
 * One document, in the editor. The screen is the one pages use; what this file owns is the address, which is
 * the one thing that cannot be configuration.
 */
const CONFIG = CONTENT_KINDS.Document;

export const Route = createFileRoute('/_staff/staff/$dept/documents/$id')({
  loader: async ({ context, params }): Promise<ContentDetailDto | null> =>
    params.id === 'new' ? null : context.queryClient.ensureQueryData(contentQuery(Number(params.id))),
  component: DocumentForm,
});

function DocumentForm() {
  const { bootstrap } = Route.useRouteContext();
  const { dept, id } = Route.useParams();
  const navigate = useNavigate();

  return (
    <ContentFormScreen
      config={CONFIG}
      bootstrap={bootstrap}
      department={dept}
      id={id}
      content={Route.useLoaderData()}
      breadcrumbTo={`/staff/${deptParam.format(dept)}/documents`}
      onCreated={async (created) => {
        await navigate({ to: '/staff/$dept/documents/$id', params: { dept, id: String(created) } });
      }}
      onFinished={() => void navigate({ to: '/staff/$dept/documents', params: { dept } })}
    />
  );
}
