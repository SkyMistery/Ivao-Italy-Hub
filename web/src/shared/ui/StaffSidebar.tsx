import {
  SidebarContext,
  SidebarItem,
  SidebarProvider,
  type SidebarAsLinkProps,
} from '@ivao/atmosphere-react';
import { ChevronRight, PanelLeftClose, PanelLeftOpen } from 'lucide-react';
import { useContext, useState, type ComponentType } from 'react';
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
}: {
  groups: readonly StaffSidebarGroup[];
  asLink?: ComponentType<SidebarAsLinkProps> | undefined;
  isActiveCheck: (href: string) => boolean;
}) {
  return (
    <SidebarProvider>
      <Frame groups={groups} asLink={asLink} isActiveCheck={isActiveCheck} />
    </SidebarProvider>
  );
}

function Frame({
  groups,
  asLink,
  isActiveCheck,
}: {
  groups: readonly StaffSidebarGroup[];
  asLink?: ComponentType<SidebarAsLinkProps> | undefined;
  isActiveCheck: (href: string) => boolean;
}) {
  const { t } = useTranslation();
  const { isSidebarOpen, toggleSidebar } = useContext(SidebarContext);

  const label = isSidebarOpen ? t('nav.sidebar.collapse') : t('nav.sidebar.expand');
  const Icon = isSidebarOpen ? PanelLeftClose : PanelLeftOpen;

  return (
    <aside
      className={`border-fuselage-200 bg-fuselage-50 dark:border-fuselage-700 dark:bg-fuselage-900 relative flex h-full flex-col border-r ${
        isSidebarOpen ? 'w-72' : 'box-content w-17'
      }`}
    >
      {/* ⚠️ Out of the flow while the panel is open (Carmine, 10 September 2026: the band it had to
          itself was empty space). It sits in the corner *beside* the first heading rather than on a
          line of its own, and what makes room for it is the padding every heading carries on its
          right — so the headings stay in one column and only the corner is given up.

          Collapsed, it goes back into the flow: a strip 68 pixels wide has no corner to spare, and
          the button would land on the first icon.

          `title` as well as `aria-label`, because a button that is only an icon says what it is on
          hover and nowhere else. */}
      <div className={isSidebarOpen ? 'absolute top-2 right-2 z-10' : 'flex justify-center pt-2'}>
        <button
          type="button"
          onClick={toggleSidebar}
          aria-label={label}
          aria-expanded={isSidebarOpen}
          title={label}
          // ⚠️ Deliberately faint (Carmine, 10 September 2026: "less invasive"). It is a control of
          // the frame and not a place to go: it should be findable and never the first thing the
          // eye lands on, so it is small, unfilled, and only takes a colour under the pointer.
          className="text-fuselage-300 hover:text-fuselage-600 dark:text-fuselage-600 dark:hover:text-fuselage-300 flex size-6 items-center justify-center rounded transition-colors"
        >
          <Icon aria-hidden className="size-4" />
        </button>
      </div>

      <div className={`flex flex-col items-start gap-4 px-4 ${isSidebarOpen ? 'py-3' : 'py-4'}`}>
        {groups.map((group) => (
          <Group
            key={group.title}
            group={group}
            asLink={asLink}
            isActiveCheck={isActiveCheck}
            sidebarOpen={isSidebarOpen}
          />
        ))}
      </div>
    </aside>
  );
}

/**
 * A heading and what is under it, opened by clicking the heading — and opened by itself when the
 * page you are on is one of its own, which is what tells you where you are after a reload.
 */
function Group({
  group,
  asLink,
  isActiveCheck,
  sidebarOpen,
}: {
  group: StaffSidebarGroup;
  asLink?: ComponentType<SidebarAsLinkProps> | undefined;
  isActiveCheck: (href: string) => boolean;
  sidebarOpen: boolean;
}) {
  const holdsTheCurrentPage = group.items.some((item) => isActiveCheck(item.href));

  // ⚠️ `null` means "nobody has said", and it is what keeps this free of an effect: a group is open
  // because the page you are on is one of its own, until somebody says otherwise by clicking it.
  // Synchronising a piece of state to a prop in a `useEffect` would be the same thing written with
  // a render in between, and `react-hooks/set-state-in-effect` refuses it — rightly, because the
  // first paint would show the wrong panel.
  const [openedByHand, setOpenedByHand] = useState<boolean | null>(null);

  const open = openedByHand ?? holdsTheCurrentPage;

  // A shut panel has no room for a list, so a group cannot be open inside one.
  const expanded = open && sidebarOpen;

  const GroupIcon = group.Icon;

  return (
    <div className="w-full">
      <button
        type="button"
        onClick={() => setOpenedByHand(!open)}
        aria-expanded={expanded}
        title={sidebarOpen ? undefined : group.title}
        className="group text-fuselage-300 dark:text-fuselage-400 flex w-full text-left"
      >
        {/* The same square the leaves wear, so a heading and its children read as one family. It is
            written here rather than imported because the library keeps that piece to itself. */}
        <div className="bg-fuselage-100 text-fuselage-500 group-hover:bg-atmos-800 group-hover:text-fuselage-50 dark:bg-fuselage-700 dark:text-fuselage-500 dark:group-hover:bg-atmos-700 dark:group-hover:text-ocean-50 flex size-9 items-center justify-center rounded-md p-2 transition-all">
          <GroupIcon />
        </div>

        {/* ⚠️ `pr-7` is what keeps the corner free for the collapse button, which floats over it
            while the panel is open. Every heading carries it, not only the first: one column of
            chevrons reads as a column, and one chevron out of line reads as a mistake. */}
        <div className={`ml-4 flex grow items-center pr-7 transition-all ${sidebarOpen ? '' : 'hidden'}`}>
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
              isActive={isActiveCheck(item.href)}
              isGroupOpen={expanded}
            />
          ))}
        </div>
      ) : null}
    </div>
  );
}
