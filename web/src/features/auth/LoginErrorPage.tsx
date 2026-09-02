import { Button, H1, Lead } from '@ivao/atmosphere-react';
import { useTranslation } from 'react-i18next';

import { loginHref } from '../../shared/api/client';

/** The reasons the server is willing to name. Anything else is shown as unknown. */
const KNOWN_REASONS = ['portal', 'correlation', 'nonce', 'profile'] as const;

type KnownReason = (typeof KNOWN_REASONS)[number];

/**
 * Where a login that could not be completed lands. It deliberately does not bounce back to the
 * login on its own: if the fault is stable, IVAO still has its session open and would send the
 * browser straight back, which is a loop with no way out. The retry is a button.
 */
export function LoginErrorPage({ code }: { code: string | undefined }) {
  const { t } = useTranslation();
  const reason: KnownReason | 'unknown' = KNOWN_REASONS.includes(code as KnownReason)
    ? (code as KnownReason)
    : 'unknown';

  return (
    <>
      <H1>{t('loginError.title')}</H1>
      <Lead>{t(`loginError.reason.${reason}`)}</Lead>
      <div>
        <Button asChild>
          <a href={loginHref('/')}>{t('loginError.retry')}</a>
        </Button>
      </div>
    </>
  );
}
