import {
  SidebarContext,
  SidebarItem,
  SidebarProvider,
  type SidebarAsLinkProps,
} from '@ivao/atmosphere-react';
import { ChevronRight, PanelLeftClose, PanelLeftOpen } from 'lucide-react';
import { useContext, useState, type ComponentType, type ReactNode } from 'react';
import { useTranslation } from 'react-i18next';

/**
 * The navigation of the back office, and the twenty-first component of the closed list (§8.3),
 * added deliberately on 10 September 2026 after Carmine asked for the collapse button to be at the
 * top and to be an icon and nothing else.
 *
 * ⚠️ **Why this exists at all, since Atmosphere ships a `Sidebar`.** Its collapse button is not a
 * prop and not a slot: `SidebarContainer` draws it itself, last, full width, and the word on it is
 * the string `"Close sidebar"` written into the library. Read in the bundle, not assumed. So the
 * request could not be met by passing anything — and the same paragraph explains a defect nobody
 * had reported: **the Italian back office was saying "Close sidebar" in English**, which is rule 1
 * of `docs/UI-GUIDELINES.md` broken by a dependency rather than by us.
 *
 * ⚠️ What is **not** rebuilt: the open/closed state is Atmosphere's own `SidebarProvider` and
 * `SidebarContext`, and every leaf is its `SidebarItem`. Only the frame around them and the group
 * header are ours, because those are the two pieces the library does not let anybody reach —
 * `SidebarGroup` is not exported, only its props are.
 *
 * ⚠️ And what this is not: wrapping their `Sidebar` in a container of ours. That was tried once and
 * drew the whole back office inside a 288 pixel `aside` with two collapse buttons (HANDOFF §13).
 * This replaces the aside; it does not nest inside one.
 */

export interface StaffSidebarEntry {
  readonly title: string;
  readonly description: string;
  readonly href: string;
  readonly Icon: ComponentType<{ className?: string }>;
}

export interface StaffSidebarGroup {
  readonly title: string;
  readonly Icon: ComponentType<{ className?: string }>;
  readonly items: readonly StaffSidebarEntry[];
}

export function StaffSidebar({
  groups,
  asLink,
  isActiveCheck,
  top,
}: {
  groups: readonly StaffSidebarGroup[];
  asLink?: ComponentType<SidebarAsLinkProps> | undefined;
  isActiveCheck: (href: string) => boolean;
  /**
   * What sits at the top of the panel beside the collapse button — the search, in the back office.
   * A slot and not an import, because this is a shared component and the search is a feature: the
   * shared side may never reach into `features/` (design M0 §6.5). It is told whether the panel is
   * collapsed, because what fits in 288 pixels does not fit in 68.
   */
  top?: ((collapsed: boolean) => ReactNode) | undefined;
}) {
  return (
    <SidebarProvider>
      <Frame groups={groups} asLink={asLink} isActiveCheck={isActiveCheck} top={top} />
    </SidebarProvider>
  );
}

/**
 * The one entry that is the page you are on: of every entry whose address matches, the **longest**.
 *
 * ⚠️ The fix for a defect Carmine found on 11 September 2026: on a department's documents, its
 * dashboard stayed lit as well. The dashboard's address is the department's own root, and every
 * address of that department begins with it — so a check that asks "does the path start with this
 * address?" says yes to both. Asking which match is the most specific says it once.
 */
function currentEntry(
  groups: readonly StaffSidebarGroup[],
  isActiveCheck: (href: string) => boolean,
): string | null {
  let best: string | null = null;

  for (const group of groups) {
    for (const item of group.items) {
      if (isActiveCheck(item.href) && (best === null || item.href.length > best.length)) {
        best = item.href;
      }
    }
  }

  return best;
}

/**
 * Which group is open. One at a time (Carmine, 11 September 2026: "so it does not get too long"),
 * and `undefined` means nobody has chosen yet.
 *
 * ⚠️ The choice remembers **which page it was made on**, and that is what keeps this free of an
 * effect. Navigating somewhere else — from the ⌘K palette, say, into another department — makes the
 * group of the new page the open one without anybody resetting a piece of state to follow a prop,
 * which is exactly what `react-hooks/set-state-in-effect` refuses, and rightly: the first paint would
 * show the wrong panel.
 */
interface OpenGroup {
  readonly onPage: string | null;
  readonly title: string | null;
}

function Frame({
  groups,
  asLink,
  isActiveCheck,
  top,
}: {
  groups: readonly StaffSidebarGroup[];
  asLink?: ComponentType<SidebarAsLinkProps> | undefined;
  isActiveCheck: (href: string) => boolean;
  top?: ((collapsed: boolean) => ReactNode) | undefined;
}) {
  const { t } = useTranslation();
  const { isSidebarOpen, setIsSidebarOpen, toggleSidebar } = useContext(SidebarContext);

  const current = currentEntry(groups, isActiveCheck);
  const holding = groups.find((group) => group.items.some((item) => item.href === current))?.title ?? null;

  const [chosen, setChosen] = useState<OpenGroup | undefined>(undefined);
  const open = chosen !== undefined && chosen.onPage === current ? chosen.title : holding;

  const choose = (title: string) => {
    // ⚠️ From the collapsed strip, a department is a way *in*, not a switch that does nothing
    // visible (Carmine, 11 September 2026): the panel opens, and on that department. Clicking an
    // open group in an open panel shuts it, which is the only way to close all of them.
    if (!isSidebarOpen) {
      setIsSidebarOpen(true);
      setChosen({ onPage: current, title });
      return;
    }

    setChosen({ onPage: current, title: open === title ? null : title });
  };

  const label = isSidebarOpen ? t('nav.sidebar.collapse') : t('nav.sidebar.expand');
  const Icon = isSidebarOpen ? PanelLeftClose : PanelLeftOpen;

  const collapse = (
    <button
      type="button"
      onClick={toggleSidebar}
      aria-label={label}
      aria-expanded={isSidebarOpen}
      title={label}
      // ⚠️ Deliberately faint (Carmine, 10 September 2026: "less invasive"). It is a control of the
      // frame and not a place to go: findable, never the first thing the eye lands on.
      className="text-fuselage-300 hover:text-fuselage-600 dark:text-fuselage-600 dark:hover:text-fuselage-300 flex size-8 shrink-0 items-center justify-center rounded transition-colors"
    >
      <Icon aria-hidden className="size-4" />
    </button>
  );

  return (
    // ⚠️ No height of its own, and that is the fix rather than an omission (Carmine, 10 September
    // 2026: the panel stopped short of the footer and left a white patch under it). `h-full` is
    // `height: 100%`, and against a row whose own height is `auto` that resolves to the height of
    // the content — which **defeats** the `items-stretch` of the row it sits in. Taking it away is
    // what lets the panel be as tall as whatever is beside it, down to the footer.
    <aside
      className={`border-fuselage-200 bg-fuselage-50 dark:border-fuselage-700 dark:bg-fuselage-900 flex flex-col border-r ${
        isSidebarOpen ? 'w-72' : 'box-content w-17'
      }`}
    >
      {/* The top of the panel: what the caller puts there — the search — and the collapse button
          beside it (Carmine, 11 September 2026). One row open; a column in the collapsed strip,
          because 68 pixels hold one square at a time. The row is a real row now, where the button
          used to float over the corner of the first heading: once something sits beside it, it has
          a line to belong to. */}
      <div
        className={
          isSidebarOpen ? 'flex items-center gap-2 px-3 pt-3' : 'flex flex-col items-center gap-2 pt-3'
        }
      >
        {top === undefined ? null : (
          <div className={isSidebarOpen ? 'min-w-0 flex-1' : ''}>{top(!isSidebarOpen)}</div>
        )}
        {collapse}
      </div>

      <div className="flex flex-col items-start gap-4 px-4 py-4">
        {groups.map((group) => (
          <Group
            key={group.title}
            group={group}
            asLink={asLink}
            current={current}
            open={open === group.title}
            onChoose={() => choose(group.title)}
            sidebarOpen={isSidebarOpen}
          />
        ))}
      </div>
    </aside>
  );
}

/**
 * A heading and what is under it. Which group is open is the frame's to say — one at a time — so a
 * group is told, and says back when it is clicked.
 */
function Group({
  group,
  asLink,
  current,
  open,
  onChoose,
  sidebarOpen,
}: {
  group: StaffSidebarGroup;
  asLink?: ComponentType<SidebarAsLinkProps> | undefined;
  current: string | null;
  open: boolean;
  onChoose: () => void;
  sidebarOpen: boolean;
}) {
  // A shut panel has no room for a list, so a group cannot be open inside one.
  const expanded = open && sidebarOpen;

  const GroupIcon = group.Icon;

  return (
    <div className="w-full">
      <button
        type="button"
        onClick={onChoose}
        aria-expanded={expanded}
        title={sidebarOpen ? undefined : group.title}
        className="group text-fuselage-300 dark:text-fuselage-400 flex w-full text-left"
      >
        {/* The same square the leaves wear, so a heading and its children read as one family. It is
            written here rather than imported because the library keeps that piece to itself. */}
        <div className="bg-fuselage-100 text-fuselage-500 group-hover:bg-atmos-800 group-hover:text-fuselage-50 dark:bg-fuselage-700 dark:text-fuselage-500 dark:group-hover:bg-atmos-700 dark:group-hover:text-ocean-50 flex size-9 shrink-0 items-center justify-center rounded-md p-2 transition-all">
          <GroupIcon />
        </div>

        <div className={`ml-4 flex min-w-0 grow items-center transition-all ${sidebarOpen ? '' : 'hidden'}`}>
          <span className="font-head text-fuselage-600 dark:text-fuselage-100 mr-2 text-base leading-tight font-semibold">
            {group.title}
          </span>
          <ChevronRight
            aria-hidden
            className={`ml-auto size-5 shrink-0 transition-transform ${expanded ? 'rotate-90' : ''}`}
          />
        </div>
      </button>

      {expanded ? (
        <div className="before:bg-fuselage-100 dark:before:bg-fuselage-700 relative flex flex-col gap-2 pt-3 pl-4.5 before:absolute before:top-2 before:left-0 before:h-[calc(100%-8px)] before:w-px before:content-['']">
          {group.items.map((item) => (
            <SidebarItem
              key={item.href}
              title={item.title}
              description={item.description}
              Icon={item.Icon}
              href={item.href}
              // Spread and not passed: `exactOptionalPropertyTypes` is on, and Atmosphere declares
              // this one optional without allowing `undefined` through it. Leaving the key out is
              // the only way to say "nothing" that its own types accept.
              {...(asLink === undefined ? {} : { asLink })}
              isActive={item.href === current}
              isGroupOpen={expanded}
            />
          ))}
        </div>
      ) : null}
    </div>
  );
}
