import { H1, Lead } from '@ivao/atmosphere-react';
import { Link } from '@tanstack/react-router';
import { ChevronRight } from 'lucide-react';
import { createContext, useContext, useMemo, useState, type ReactNode } from 'react';
import { createPortal } from 'react-dom';
import { useTranslation } from 'react-i18next';

/**
 * The frame of a page: where you are, what the page is called, and what you can do on it. Every
 * screen of `/staff` uses it, so the title and the actions never drift from one page to the next —
 * and the public pages use it too, which is why it has two densities.
 */
export interface Crumb {
  readonly label: string;
  /** Absent on the last crumb, which is the page you are already on. */
  readonly to?: string;
}

/**
 * Whether the frame is drawn in one line. The back office says yes, once, in its layout; the public
 * site says nothing and keeps the large title and summary a reader expects of a page.
 *
 * ⚠️ A context and not a prop, and that is the point of it: thirty screens of `/staff` use this
 * component, and a prop would have been thirty edits to say the same thing. The layout knows which
 * area it is; the screens do not need to.
 */
const CompactContext = createContext(false);

export function CompactPageShells({ children }: { children: ReactNode }) {
  return <CompactContext.Provider value={true}>{children}</CompactContext.Provider>;
}

/**
 * Where a screen's own controls go when they are made deep inside it rather than handed to the
 * frame as `actions`. `null` outside a one-line frame; `target` is `null` for the one render before
 * the frame's slot exists.
 */
const ActionsContext = createContext<{ readonly target: HTMLElement | null } | null>(null);

/**
 * Puts its children on the frame's own line, beside the title, from anywhere below it.
 *
 * ⚠️ Why a portal and not the `actions` prop: the content editor's toolbar is built from the
 * editor's own state — the undo history, the form it submits by `form=`, whether the outline or the
 * page is showing — and handing all of that up to the screen that draws the frame would turn the
 * editor inside out to move five buttons one line higher. The buttons stay where their state is;
 * only where they are drawn changes. Outside a one-line frame they are drawn where they stand.
 */
export function PageActions({ children }: { children: ReactNode }) {
  const slot = useContext(ActionsContext);

  if (slot === null) {
    return <>{children}</>;
  }

  // Nothing for the one render before the slot exists, rather than a flash of the toolbar in the
  // wrong place.
  return slot.target === null ? null : createPortal(children, slot.target);
}

export function PageShell({
  title,
  description,
  note,
  breadcrumb = [],
  actions,
  children,
}: {
  title: string;
  /**
   * What the page is for, in a sentence. Drawn under the title on the public site. ⚠️ **Not drawn**
   * in the back office since 11 September 2026: there it is the same sentence the sidebar already
   * shows under the entry that leads here, and it cost a line of every screen. It stays as the
   * title's tooltip.
   */
  description?: string;
  /**
   * Something true of **this** page that has to stay on screen — how many pages were made from this
   * template, the rule a form is filled against. Unlike `description`, it is drawn in both
   * densities: it is information, not a caption.
   */
  note?: string;
  breadcrumb?: readonly Crumb[];
  actions?: ReactNode;
  children: ReactNode;
}) {
  // The label of the trail is read out loud and never seen, which is exactly why it was the last
  // English string left in a component: nobody looking at the screen could notice it.
  const { t } = useTranslation();
  const compact = useContext(CompactContext);
  const [target, setTarget] = useState<HTMLElement | null>(null);
  const slot = useMemo(() => ({ target }), [target]);

  if (compact) {
    // ⚠️ One line (Carmine, 11 September 2026: "can we stop wasting all this space at the top"). The
    // title *is* the end of the trail — it used to be written twice, once small in the trail and
    // once very large under it — so the last crumb that names this page is dropped and the title
    // takes its place, as the page's heading. A crumb that is a link stays: on an editor the trail
    // ends at the list, and the title says which row.
    const last = breadcrumb.at(-1);
    const trail = last !== undefined && last.to === undefined ? breadcrumb.slice(0, -1) : breadcrumb;

    return (
      <div className="flex flex-col gap-4">
        {/* Sticky, so where you are and what you can do stay in sight while a long list or a long
            page scrolls under them — which is what the content editor's toolbar did on its own
            before it moved onto this line. */}
        <div className="bg-body sticky top-0 z-20 -mx-6 -mt-5 flex flex-wrap items-center gap-x-4 gap-y-2 px-6 py-3">
          <nav aria-label={t('common.breadcrumb')} className="min-w-0">
            <ol className="flex flex-wrap items-center gap-1.5">
              {trail.map((crumb) => (
                <li key={crumb.label} className="text-muted-foreground flex items-center gap-1.5 text-sm">
                  {crumb.to === undefined ? (
                    <span>{crumb.label}</span>
                  ) : (
                    <Link to={crumb.to} className="hover:text-foreground underline-offset-2 hover:underline">
                      {crumb.label}
                    </Link>
                  )}
                  <ChevronRight aria-hidden className="size-3.5" />
                </li>
              ))}
              <li className="flex items-center">
                {/* Still the page's `<h1>`: a heading a screen reader jumps to, and the title of the
                    tab, are not things to give up for a line of height. */}
                <h1
                  aria-current="page"
                  title={description}
                  className="font-head text-foreground text-2xl leading-tight font-extrabold"
                >
                  {title}
                </h1>
              </li>
            </ol>
          </nav>

          {/* The slot is always there, even with no `actions`: a screen may still send its own
              controls up with `PageActions`, and they need somewhere to land. */}
          <div ref={setTarget} className="ml-auto flex flex-wrap items-center gap-2">
            {actions}
          </div>
        </div>

        {note === undefined ? null : <p className="text-muted-foreground -mt-2 text-sm">{note}</p>}

        <ActionsContext.Provider value={slot}>{children}</ActionsContext.Provider>
      </div>
    );
  }

  return (
    <div className="flex flex-col gap-6">
      {breadcrumb.length === 0 ? null : (
        <nav aria-label={t('common.breadcrumb')}>
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
          {note === undefined ? null : <Lead>{note}</Lead>}
        </div>
        {actions === undefined ? null : <div className="flex flex-wrap items-center gap-2">{actions}</div>}
      </div>

      {children}
    </div>
  );
}
