import { H1, Lead } from '@ivao/atmosphere-react';
import { Link } from '@tanstack/react-router';
import { ChevronRight } from 'lucide-react';
import type { ReactNode } from 'react';

/**
 * The frame of a back office page: where you are, what the page is called, and what you can do on
 * it. Every screen of `/staff` uses it, so the title and the actions never drift from one page to
 * the next.
 */
export interface Crumb {
  readonly label: string;
  /** Absent on the last crumb, which is the page you are already on. */
  readonly to?: string;
}

export function PageShell({
  title,
  description,
  breadcrumb = [],
  actions,
  children,
}: {
  title: string;
  description?: string;
  breadcrumb?: readonly Crumb[];
  actions?: ReactNode;
  children: ReactNode;
}) {
  return (
    <div className="flex flex-col gap-6">
      {breadcrumb.length === 0 ? null : (
        <nav aria-label="breadcrumb">
          <ol className="text-muted-foreground flex flex-wrap items-center gap-1 text-sm">
            {breadcrumb.map((crumb, index) => (
              <li key={crumb.label} className="flex items-center gap-1">
                {index === 0 ? null : <ChevronRight aria-hidden className="size-3" />}
                {crumb.to === undefined ? (
                  <span aria-current="page">{crumb.label}</span>
                ) : (
                  <Link to={crumb.to} className="hover:text-foreground underline-offset-2 hover:underline">
                    {crumb.label}
                  </Link>
                )}
              </li>
            ))}
          </ol>
        </nav>
      )}

      <div className="flex flex-wrap items-start justify-between gap-4">
        <div className="flex flex-col gap-1">
          <H1>{title}</H1>
          {description === undefined ? null : <Lead>{description}</Lead>}
        </div>
        {actions === undefined ? null : <div className="flex flex-wrap items-center gap-2">{actions}</div>}
      </div>

      {children}
    </div>
  );
}
