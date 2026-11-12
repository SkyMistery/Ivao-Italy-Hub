import { Button } from '@ivao/atmosphere-react';
import { Link, useRouter, type ErrorComponentProps } from '@tanstack/react-router';
import { CloudOff, FileQuestion, ShieldOff } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { ApiError } from '../api/problem';
import { PageMetadata } from '../seo/PageMetadata';

import { EmptyState } from './layout-pieces';

/**
 * The answers a router gives that are not a page: there is nothing here, you may not see what is
 * here, and what is here could not be read. All are translated and all offer a way out, because a
 * dead end in a back office is a support message (design M0 §7.2).
 */

export function NotFound() {
  const { t } = useTranslation();

  return (
    <>
      {/* Its tab says so too (#224), wherever it is drawn: by a route, or by a module's page whose
          row is not there. */}
      <PageMetadata title={t('notFound.title')} />
      <EmptyState
        Icon={FileQuestion}
        title={t('notFound.title')}
        description={t('notFound.description')}
        action={
          <Button asChild variant="secondary">
            <Link to="/">{t('notFound.home')}</Link>
          </Button>
        }
      />
    </>
  );
}

export function Forbidden() {
  const { t } = useTranslation();

  return (
    <EmptyState
      Icon={ShieldOff}
      title={t('forbidden.title')}
      description={t('forbidden.description')}
      action={
        <Button asChild variant="secondary">
          <Link to="/">{t('forbidden.home')}</Link>
        </Button>
      }
    />
  );
}

/**
 * The third answer: something is here, and it could not be read just now — a 500, the network, an answer nobody
 * expected. Said in the reader's language and with a way to try again, never as the router's English screen with the raw
 * message (issue #332, item 5). `onRetry` is what trying again means where it is drawn.
 */
export function Failed({ onRetry }: { onRetry: () => void }) {
  const { t } = useTranslation();

  return (
    <>
      <PageMetadata title={t('failed.title')} />
      <EmptyState
        Icon={CloudOff}
        title={t('failed.title')}
        description={t('failed.description')}
        action={
          <Button variant="secondary" onClick={onRetry}>
            {t('failed.retry')}
          </Button>
        }
      />
    </>
  );
}

/**
 * What a route draws when its loader or its screen throws: the router's default (`createHubRouter`), so that no route is
 * left to the English one. A 404 is `NotFound` and a 403 is `Forbidden`, as the guards say them; everything else is
 * `Failed`, and trying again reads the route again.
 */
export function RouteError({ error, reset }: ErrorComponentProps) {
  const router = useRouter();

  if (error instanceof ApiError && error.status === 404) {
    return <NotFound />;
  }

  if (error instanceof ApiError && error.status === 403) {
    return <Forbidden />;
  }

  return (
    <Failed
      onRetry={() => {
        // The loaders again, and the boundary cleared for what they bring.
        void router.invalidate().then(reset);
      }}
    />
  );
}
