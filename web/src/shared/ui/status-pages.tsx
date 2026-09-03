import { Button } from '@ivao/atmosphere-react';
import { Link } from '@tanstack/react-router';
import { FileQuestion, ShieldOff } from 'lucide-react';
import { useTranslation } from 'react-i18next';

import { EmptyState } from './layout-pieces';

/**
 * The two answers a router gives that are not a page: there is nothing here, and you may not see
 * what is here. Both are translated and both offer a way out, because a dead end in a back office
 * is a support message (design M0 §7.2).
 */

export function NotFound() {
  const { t } = useTranslation();

  return (
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
