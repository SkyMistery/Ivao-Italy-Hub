import { useQuery } from '@tanstack/react-query';
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

  // The row as it stands now. ⚠️ Not `Route.useLoaderData()`: a loader runs on navigation and
  // never again, so after one save the screen still held the `rowVersion` from when the page
  // opened, and the second save was answered 409 — blaming somebody who does not exist. The loader
  // above is the *preload*; what the screen reads is the query it filled (design M0 §7.3).
  const row = useQuery({ ...contentQuery(Number(id)), enabled: id !== 'new' }).data ?? null;

  return (
    <ContentFormScreen
      config={CONFIG}
      bootstrap={bootstrap}
      department={dept}
      id={id}
      content={row}
      breadcrumbTo={`/staff/${deptParam.format(dept)}/documents`}
      onCreated={async (created) => {
        await navigate({ to: '/staff/$dept/documents/$id', params: { dept, id: String(created) } });
      }}
      onFinished={() => void navigate({ to: '/staff/$dept/documents', params: { dept } })}
    />
  );
}
