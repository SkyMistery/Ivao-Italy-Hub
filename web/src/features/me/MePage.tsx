import { Badge, H1, H3, Lead } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { loginHref } from '../../shared/api/client';

import { bootstrapQuery } from './queries';

/**
 * What the hub knows about the person who is signed in. In F6 this becomes the member dashboard
 * composed of the widgets registered by the modules; here it is the proof that the identity, the
 * positions and the effective permissions came through the login intact.
 */
export function MePage() {
  const { t } = useTranslation();
  const { data: bootstrap, isPending } = useQuery(bootstrapQuery);

  if (isPending) {
    return <Lead>{t('common.loading')}</Lead>;
  }

  const user = bootstrap?.user ?? null;
  if (user === null) {
    return (
      <>
        <H1>{t('me.title')}</H1>
        <Lead>
          <a className="underline" href={loginHref('/me')}>
            {t('auth.login')}
          </a>
        </Lead>
      </>
    );
  }

  return (
    <>
      <H1>{t('me.title')}</H1>

      <div className="flex flex-wrap items-center gap-2">
        <Badge variant="filled" text={`${t('me.vid')}: ${user.vid}`} />
        {user.isStaff ? <Badge variant="flat" text={t('me.staff')} /> : null}
        {user.isSuperadmin ? <Badge variant="leaked" text={t('me.superadmin')} /> : null}
      </div>

      <Lead>{[user.firstName, user.lastName].filter((part) => part.length > 0).join(' ')}</Lead>

      <section className="flex flex-col gap-1">
        <H3>{t('me.positions')}</H3>
        <Values values={[...user.positions]} empty={t('me.none')} />
      </section>

      <section className="flex flex-col gap-1">
        <H3>{t('me.departments')}</H3>
        <Values values={[...user.departments]} empty={t('me.none')} />
      </section>

      <section className="flex flex-col gap-1">
        <H3>{t('me.firs')}</H3>
        <Values values={[...user.firs]} empty={t('me.none')} />
      </section>

      <section className="flex flex-col gap-1">
        <H3>{t('me.permissions')}</H3>
        <Values
          values={(bootstrap?.permissions ?? []).map((permission) =>
            permission.department === null
              ? permission.name
              : `${permission.name} (${permission.department})`,
          )}
          empty={t('me.none')}
        />
      </section>
    </>
  );
}

function Values({ values, empty }: { values: string[]; empty: string }) {
  if (values.length === 0) {
    return <p className="text-muted-foreground">{empty}</p>;
  }

  return (
    <ul className="flex flex-wrap gap-2">
      {values.map((value) => (
        <li key={value}>
          <Badge variant="flat" text={value} />
        </li>
      ))}
    </ul>
  );
}
