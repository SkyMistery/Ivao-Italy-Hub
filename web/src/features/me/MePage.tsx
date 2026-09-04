import { H1 } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { registry } from '../../app/registry';

import { bootstrapQuery } from './queries';

/**
 * The member dashboard. It holds no tile of its own: the server declares what belongs on it in
 * `registries.widgets`, this composes whatever the browser has a component for, and a module adds
 * one by putting it in its manifest (design M0 §6.3 and §6.5).
 *
 * A tile the server declares and this build cannot draw is said out loud rather than left as a gap,
 * and only to the staff — the same rule the block renderer follows, for the same reason: a visitor
 * cannot act on it, and somebody who can needs to know the two sides are out of step.
 */
export function MePage() {
  const { t } = useTranslation();
  const { data: bootstrap } = useQuery(bootstrapQuery);

  const declared = bootstrap?.registries.widgets ?? [];
  const isStaff = bootstrap?.user?.isStaff === true || bootstrap?.user?.isSuperadmin === true;

  const missing = declared
    .filter((widget) => !registry.widgets.some((known) => known.key === widget.key))
    .map((widget) => widget.key);

  return (
    <>
      <H1>{t('me.title')}</H1>

      {isStaff && missing.length > 0 ? (
        <div className="border-border text-muted-foreground rounded-md border border-dashed p-4 text-sm">
          {t('widgets.unknown', { keys: missing.join(', ') })}
        </div>
      ) : null}

      <div className="flex flex-col gap-8">
        {declared.map((widget) => {
          const known = registry.widgets.find((candidate) => candidate.key === widget.key);
          if (!known) {
            return null;
          }

          const Widget = known.component;
          return (
            <section key={widget.key} className="flex flex-col gap-3">
              <Widget />
            </section>
          );
        })}
      </div>
    </>
  );
}
