import { createFileRoute } from '@tanstack/react-router';
import { z } from 'zod';

import { PublicCalendarScreen } from '../../features/calendar/PublicCalendarScreen';
import { DEPARTMENTS } from '../../shared/api/department';
import { CALENDAR_VIEWS } from '../../shared/ui';

/**
 * `/calendar`: month, week and agenda over the one calendar of the division (design M1 §4).
 *
 * Everything a visitor chose is in the address — which view, which department, which kind, which
 * month — so what they are looking at is a thing they can send to somebody else. `catch` on each:
 * a hand-edited address with nonsense in it shows the calendar rather than an error page.
 *
 * A static route beats `/$slug`, so this address stays this page whatever anybody slugs a page.
 */
const searchSchema = z.object({
  view: z.enum(CALENDAR_VIEWS).optional().catch(undefined),
  department: z.enum(DEPARTMENTS).optional().catch(undefined),
  kind: z.string().optional().catch(undefined),
  on: z.string().optional().catch(undefined),
});

export const Route = createFileRoute('/_public/calendar')({
  validateSearch: searchSchema,
  component: PublicCalendarPage,
});

function PublicCalendarPage() {
  const { bootstrap } = Route.useRouteContext();
  const search = Route.useSearch();
  const navigate = Route.useNavigate();

  return (
    <PublicCalendarScreen
      filters={search}
      onFilter={(patch) => void navigate({ search: patch })}
      // The zone of the division, from `/api/me` and never a constant (plan §9.5).
      timezone={bootstrap.division.timezone}
    />
  );
}
