import { createFileRoute } from '@tanstack/react-router';
import { z } from 'zod';

import { LoginErrorPage } from '../../features/auth/LoginErrorPage';

/**
 * A route of the application, not an endpoint: it is deliberately not excluded from the SPA
 * fallback. The server redirects here when the IVAO round trip does not close.
 */
export const Route = createFileRoute('/_public/login-error')({
  validateSearch: z.object({ code: z.string().optional() }),
  component: LoginError,
});

function LoginError() {
  const { code } = Route.useSearch();
  return <LoginErrorPage code={code} />;
}
