import { createFileRoute } from '@tanstack/react-router';

import { Forbidden } from '../../shared/ui';

/**
 * Where a guard sends somebody who is signed in but may not see what they asked for. Public on
 * purpose: it has to be reachable whatever the session turns out to be.
 */
export const Route = createFileRoute('/_public/forbidden')({
  component: Forbidden,
});
