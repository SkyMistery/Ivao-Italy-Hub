import { Button } from '@ivao/atmosphere-react';
import { Link, createFileRoute } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { contactColumns } from '../../features/contacts/list';
import { contactsListQuery } from '../../features/contacts/queries';
import { DataList, listSearchSchema } from '../../shared/list';
import { PageShell } from '../../shared/ui';

/**
 * The contact queue of a department. Recipe 2 (design M0 §7.3): paging, sorting and searching are
 * the typed search parameters of the route, so the state of the screen is the URL.
 *
 * There is no "new message" button, and that is not an omission: a message is written by a member
 * on `/contact`, and the queue is where it lands.
 */
export const Route = createFileRoute('/_staff/staff/$dept/contacts/')({
  validateSearch: listSearchSchema,
  loaderDeps: ({ search }) => search,
  loader: ({ context, deps, params }) =>
    context.queryClient.ensureQueryData(contactsListQuery(params.dept, deps)),
  component: ContactsPage,
});

function ContactsPage() {
  const { t, i18n } = useTranslation();
  const { bootstrap } = Route.useRouteContext();
  const { dept } = Route.useParams();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();

  const division = bootstrap.division;

  return (
    <PageShell
      title={t('contacts.title')}
      description={t('contacts.description')}
      breadcrumb={[{ label: dept }, { label: t('contacts.title') }]}
    >
      <DataList
        columns={contactColumns}
        query={contactsListQuery(dept, search)}
        labels="contacts"
        locale={i18n.language}
        defaultLocale={division.defaultLocale}
        timezone={division.timezone}
        search={search}
        onSearchChange={(patch) => void navigate({ search: (previous) => ({ ...previous, ...patch }) })}
        actions={(row) => (
          <Button asChild variant="ghost" size="sm">
            <Link to="/staff/$dept/contacts/$id" params={{ dept, id: String(row.id) }}>
              {t('contacts.open')}
            </Link>
          </Button>
        )}
      />
    </PageShell>
  );
}
