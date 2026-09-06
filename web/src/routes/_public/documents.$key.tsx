import { createFileRoute } from '@tanstack/react-router';

import { CORE_BLOCK_TYPES } from '../../blocks';
import { PublicEntryScreen } from '../../features/content/PublicEntryScreen';
import { PublicListScreen } from '../../features/content/PublicListScreen';
import { publicListSearchSchema } from '../../features/content/publicSearch';
import { publicContentQuery, type PublicContentDto } from '../../features/content/queries';
import type { Department } from '../../shared/api/bootstrap';
import { isDepartment } from '../../shared/api/department';
import { NotFound } from '../../shared/ui';

/**
 * `/documents/{something}` — and the something is either a department or a document.
 *
 * Design M1 §3.3 asks for both `/documents/{dept}` and `/documents/{slug}`, and one path segment
 * cannot be two things: what decides is whether the segment names a department, which is a closed
 * set the contract already spells out (`shared/api/department.ts`). The department wins.
 *
 * ⚠️ So a document slugged `ed` would never be reachable. That is a real corner and it is written
 * here rather than discovered: nine words out of every possible slug are spoken for, they are the
 * department codes, and the editor is the place to pick another address.
 */
type Shown = { readonly department: Department } | { readonly content: PublicContentDto };

export const Route = createFileRoute('/_public/documents/$key')({
  validateSearch: publicListSearchSchema,
  loader: async ({ context, params }): Promise<Shown> => {
    const upper = params.key.toUpperCase();

    return isDepartment(upper)
      ? { department: upper }
      : { content: await context.queryClient.ensureQueryData(publicContentQuery('Document', params.key)) };
  },
  notFoundComponent: NotFound,
  errorComponent: NotFound,
  component: PublicDocument,
});

function PublicDocument() {
  const shown = Route.useLoaderData();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();

  if ('content' in shown) {
    return <PublicEntryScreen content={shown.content} />;
  }

  return (
    <PublicListScreen
      type={CORE_BLOCK_TYPES.documentList}
      titles="documents"
      filters={search}
      onFilter={(patch) => void navigate({ search: patch })}
      layout={{ groupByCategory: true }}
      fixedDepartment={shown.department}
    />
  );
}
