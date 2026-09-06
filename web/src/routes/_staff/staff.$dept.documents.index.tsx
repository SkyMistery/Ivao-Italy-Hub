import { Button } from '@ivao/atmosphere-react';
import { Link, createFileRoute } from '@tanstack/react-router';

import { ContentListScreen, CreateLabel, EditLabel } from '../../features/content/ContentListScreen';
import { CONTENT_KINDS } from '../../features/content/kinds';
import { contentListQuery } from '../../features/content/queries';
import { listSearchSchema } from '../../shared/list';

/**
 * The documents of one department, the second kind. A document with a file is a card with a
 * download on the public site; one without is read in the browser like any other page.
 *
 * It is `/staff/<dept>/content` with a different `kind` and a different set of columns, and that is
 * the whole difference: one entity, one editor, one renderer, one publication (design M1 §3.2).
 *
 * The department and its guard are on the layout above.
 */
const CONFIG = CONTENT_KINDS.Document;

export const Route = createFileRoute('/_staff/staff/$dept/documents/')({
  validateSearch: listSearchSchema,
  loaderDeps: ({ search }) => search,
  loader: ({ context, deps, params }) =>
    context.queryClient.ensureQueryData(contentListQuery(params.dept, deps, CONFIG.kind)),
  component: DocumentsPage,
});

function DocumentsPage() {
  const { bootstrap } = Route.useRouteContext();
  const { dept } = Route.useParams();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();

  const open = (id: string) => void navigate({ to: '/staff/$dept/documents/$id', params: { dept, id } });

  return (
    <ContentListScreen
      config={CONFIG}
      bootstrap={bootstrap}
      department={dept}
      search={search}
      onSearchChange={(patch) => void navigate({ search: (previous) => ({ ...previous, ...patch }) })}
      onCreatedFromTemplate={(id) => open(String(id))}
      createButton={
        <Button asChild>
          <Link to="/staff/$dept/documents/$id" params={{ dept, id: 'new' }}>
            <CreateLabel titles={CONFIG.titles} />
          </Link>
        </Button>
      }
      rowAction={(row) => (
        <Button asChild variant="ghost" size="sm">
          <Link to="/staff/$dept/documents/$id" params={{ dept, id: String(row.id) }}>
            <EditLabel />
          </Link>
        </Button>
      )}
    />
  );
}
