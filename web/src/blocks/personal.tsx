import { Badge, H3, H4 } from '@ivao/atmosphere-react';
import { useQuery } from '@tanstack/react-query';
import { useTranslation } from 'react-i18next';

import { bootstrapQuery } from '../features/me/queries';
import { deptParam } from '../shared/api/department';
import { reachableDepartments, type LocalizedString } from '../shared/api/bootstrap';
import { useLocalized } from '../shared/i18n/useLocalized';
import { useMoment } from '../shared/i18n/useMoment';
import type { BlockComponentProps } from '../shared/modules';
import { MarkdownContent } from '../shared/ui';

/**
 * The blocks of the personal dashboards (note 2026-09-13-le-dashboard-a-tutto-schermo §3.5): each
 * draws what belongs to **whoever is looking**. The first two read it from what the browser already
 * knows about them; `myWork` is answered for them by the server, always live.
 */

/** A greeting by name, and a word the web team writes under it. */
export function WelcomeBlock({ props }: BlockComponentProps) {
  const { t } = useTranslation();
  const read = useLocalized();
  const { data: bootstrap } = useQuery(bootstrapQuery);

  const user = bootstrap?.user ?? null;
  const message = read(props.message as LocalizedString | null | undefined);

  return (
    <div className="flex flex-col gap-3">
      <H3>{user === null ? t('blocks.welcome.anonymous') : t('blocks.welcome.greeting', { name: user.firstName || user.vid })}</H3>
      {user === null ? null : (
        <div className="flex flex-wrap items-center gap-2">
          <Badge variant="filled" text={`${t('me.vid')}: ${user.vid}`} />
          {user.isStaff ? <Badge variant="flat" text={t('me.staff')} /> : null}
          {user.positions.map((position) => (
            <Badge key={position} variant="flat" text={position} />
          ))}
        </div>
      )}
      {message === '' ? null : <MarkdownContent markdown={message} />}
    </div>
  );
}

/** The dashboards of the departments this person works in, one link each. */
export function MyDepartmentsBlock() {
  const { t } = useTranslation();
  const { data: bootstrap } = useQuery(bootstrapQuery);

  const departments = bootstrap === undefined ? [] : reachableDepartments(bootstrap);

  return (
    <div className="flex flex-col gap-3">
      <H4>{t('blocks.myDepartments.title')}</H4>
      {departments.length === 0 ? (
        <p className="text-muted-foreground text-sm">{t('blocks.myDepartments.empty')}</p>
      ) : (
        <ul className="flex flex-wrap gap-2">
          {departments.map((department) => (
            <li key={department}>
              <a
                href={`/staff/${deptParam.format(department)}`}
                className="border-border hover:bg-accent inline-flex rounded-md border px-3 py-1.5 text-sm"
              >
                {t(`departments.${department}`)}
              </a>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}

/** One row of what waits: a title in every language or a text as it was written, and where to go. */
interface MyWorkItem {
  readonly title?: LocalizedString | null;
  readonly text?: string | null;
  readonly url: string;
  readonly department: string;
  readonly at?: string | null;
  readonly status?: string;
  readonly sentBack?: boolean;
}

interface MyWorkData {
  readonly what?: string | null;
  readonly items?: readonly MyWorkItem[];
}

/** What waits for whoever is looking: pages to approve, contacts, documents to review, drafts. */
export function MyWorkBlock({ props, data }: BlockComponentProps) {
  const { t } = useTranslation();
  const read = useLocalized();
  const moment = useMoment();

  const what = typeof props.what === 'string' ? props.what : 'drafts';
  const items = (data as MyWorkData | null | undefined)?.items;

  return (
    <div className="flex flex-col gap-3">
      <H4>{t(`blocks.myWork.titles.${what}`)}</H4>
      {items === undefined ? (
        <p className="text-muted-foreground text-sm">{t('common.loading')}</p>
      ) : items.length === 0 ? (
        <p className="text-muted-foreground text-sm">{t(`blocks.myWork.empty.${what}`)}</p>
      ) : (
        <ul className="flex flex-col divide-y">
          {items.map((item) => (
            <li key={item.url} className="flex flex-col gap-0.5 py-2">
              <a href={item.url} className="font-medium hover:underline">
                {item.text ?? read(item.title)}
              </a>
              <span className="text-muted-foreground flex flex-wrap gap-2 text-xs">
                <span>{t(`departments.${item.department}`)}</span>
                {item.at ? <span>{moment(item.at, { time: false })}</span> : null}
                {item.sentBack === true ? <span>{t('blocks.myWork.sentBack')}</span> : null}
                {item.status === 'Ready' ? <span>{t('content.options.status.Ready')}</span> : null}
              </span>
            </li>
          ))}
        </ul>
      )}
    </div>
  );
}
