import { Button } from '@ivao/atmosphere-react';
import { Link, createFileRoute } from '@tanstack/react-router';

import { ContentListScreen, CreateLabel, EditLabel } from '../../features/content/ContentListScreen';
import { CONTENT_KINDS } from '../../features/content/kinds';
import { contentListQuery } from '../../features/content/queries';
import { listSearchSchema } from '../../shared/list';

/**
 * The news of one department, the first of the two kinds design M1 §3 set out to prove cost
 * nothing: a `kind` of `cms_contents` and not a table of its own.
 *
 * It is `/staff/<dept>/content` with a different `kind` and a different set of columns, and that is
 * the whole difference: one entity, one editor, one renderer, one publication (design M1 §3.2).
 *
 * The department and its guard are on the layout above.
 */
const CONFIG = CONTENT_KINDS.News;

export const Route = createFileRoute('/_staff/staff/$dept/news/')({
  validateSearch: listSearchSchema,
  loaderDeps: ({ search }) => search,
  loader: ({ context, deps, params }) =>
    context.queryClient.ensureQueryData(contentListQuery(params.dept, deps, CONFIG.kind)),
  component: NewsListPage,
});

function NewsListPage() {
  const { bootstrap } = Route.useRouteContext();
  const { dept } = Route.useParams();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();

  const open = (id: string) => void navigate({ to: '/staff/$dept/news/$id', params: { dept, id } });

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
          <Link to="/staff/$dept/news/$id" params={{ dept, id: 'new' }}>
            <CreateLabel titles={CONFIG.titles} />
          </Link>
        </Button>
      }
      rowAction={(row) => (
        <Button asChild variant="ghost" size="sm">
          <Link to="/staff/$dept/news/$id" params={{ dept, id: String(row.id) }}>
            <EditLabel />
          </Link>
        </Button>
      )}
    />
  );
}
