import { render, screen, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { CalendarDays, FileText, KeyRound, LayoutDashboard, Newspaper, ShieldCheck } from 'lucide-react';
import i18next from 'i18next';
import { I18nextProvider, initReactI18next } from 'react-i18next';
import { beforeAll, expect, test } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';
import italianCommon from '../../../../locales/it/common.json';

import { StaffSidebar, type StaffSidebarGroup } from './StaffSidebar';

/**
 * The twenty-first component of the closed list, and the four things it exists to keep true.
 *
 * It replaced Atmosphere's `Sidebar` for one reason — the collapse button is drawn by the library
 * at the bottom, the width of the panel, carrying the English words "Close sidebar" — so these are
 * assertions about exactly that: where the button is, that it is only an icon, and that what it
 * says comes from the language files like every other word in the hub.
 */

const i18n = i18next.createInstance();

beforeAll(async () => {
  await i18n.use(initReactI18next).init({
    lng: 'en',
    fallbackLng: 'en',
    ns: ['common'],
    defaultNS: 'common',
    resources: { en: { common: englishCommon }, it: { common: italianCommon } },
    interpolation: { escapeValue: false },
  });
});

const groups: StaffSidebarGroup[] = [
  {
    title: 'Events',
    Icon: CalendarDays,
    items: [
      // The department's own root, as the real dashboard entry is: the address every other address
      // of the department begins with, which is what the most-specific rule is about.
      { title: 'Dashboard', description: 'Its home.', href: '/staff/ed', Icon: LayoutDashboard },
      { title: 'Pages', description: 'What it publishes.', href: '/staff/ed/content', Icon: FileText },
      { title: 'News', description: 'What it announces.', href: '/staff/ed/news', Icon: Newspaper },
    ],
  },
  {
    title: 'Administration',
    Icon: ShieldCheck,
    items: [
      {
        title: 'Permissions',
        description: 'Who holds what.',
        href: '/staff/admin/permissions',
        Icon: KeyRound,
      },
    ],
  },
];

/**
 * The same check `StaffLayout` hands the sidebar: an entry matches when the path is its address or
 * begins with it. Deliberately the loose one, because making the sidebar pick the right entry out
 * of several matches is the thing under test.
 */
const prefixOf = (here: string) => (href: string) => here === href || here.startsWith(`${href}/`);

function mount(here = '/staff/ed/content') {
  return render(
    <I18nextProvider i18n={i18n}>
      <StaffSidebar groups={groups} isActiveCheck={prefixOf(here)} />
    </I18nextProvider>,
  );
}

/** Whether an entry is drawn as the current one: its square takes the filled, active colours. */
const isLit = (name: string) =>
  screen.getByRole('link', { name: new RegExp(name) }).querySelector('.bg-atmos-700') !== null;

test('the collapse button is the first thing in the panel, and it is an icon and nothing else', () => {
  mount();

  const panel = screen.getByRole('complementary');
  const [first] = within(panel).getAllByRole('button');

  // First in the document order of the panel, which is what "at the top" is: a button placed last
  // and floated up would read as the last thing to anybody who is not looking at the screen.
  expect(first).toHaveAccessibleName(englishCommon.nav.sidebar.collapse);

  // ⚠️ No text. This is the half of the request that Atmosphere's own button breaks: it writes
  // "Close sidebar" beside its chevron and takes the width of the panel to do it.
  expect(first).toHaveTextContent('');
  expect(screen.queryByText('Close sidebar')).not.toBeInTheDocument();
});

test('it collapses and opens again, and says which of the two it will do', async () => {
  const user = userEvent.setup();
  mount();

  const panel = screen.getByRole('complementary');

  expect(panel.className).toContain('w-72');
  expect(screen.getByRole('link', { name: /Pages/ })).toBeInTheDocument();

  await user.click(screen.getByRole('button', { name: englishCommon.nav.sidebar.collapse }));

  // ⚠️ Asserted on the width and on what is drawn, not with `toBeVisible`: the words are hidden by
  // a Tailwind class, and jsdom loads no stylesheet — so a visibility assertion here would pass
  // whatever the component did. What can be measured is that the panel narrowed and that the list
  // of a group is gone, because a shut panel has no room for one.
  expect(panel.className).toContain('w-17');
  expect(screen.queryByRole('link', { name: /Pages/ })).not.toBeInTheDocument();
  expect(screen.getByRole('button', { name: englishCommon.nav.sidebar.expand })).toBeInTheDocument();

  await user.click(screen.getByRole('button', { name: englishCommon.nav.sidebar.expand }));

  expect(panel.className).toContain('w-72');
  expect(screen.getByRole('link', { name: /Pages/ })).toBeInTheDocument();
});

test('what the button says comes from the language files, in every language', async () => {
  await i18n.changeLanguage('it');
  mount();

  // The defect this component was written for: a dependency was putting an English sentence on the
  // screen of an Italian back office, and no language file could reach it.
  expect(screen.getByRole('button', { name: italianCommon.nav.sidebar.collapse })).toBeVisible();

  await i18n.changeLanguage('en');
});

test('the group holding the page you are on opens itself, and the others do not', () => {
  mount('/staff/admin/permissions');

  // Where you are, said without a click. After a reload the alternative is a panel of shut
  // headings that tells you nothing about which screen you are looking at.
  expect(screen.getByRole('link', { name: /Permissions/ })).toBeVisible();
  expect(screen.queryByRole('link', { name: /Pages/ })).not.toBeInTheDocument();
});

test('only the most specific entry is lit: the dashboard is not current on the documents', () => {
  // ⚠️ The defect Carmine found on 11 September 2026. The dashboard's address is the department's own
  // root, so every address of the department begins with it, and a prefix check says yes to both
  // it and the entry you are really on. The sidebar is handed exactly that loose check, and has to
  // pick the one.
  mount('/staff/ed/news');

  expect(isLit('News')).toBe(true);
  expect(isLit('Dashboard')).toBe(false);
});

test('a detail page still lights the list it belongs to', () => {
  // The other half of the same rule: most specific among the entries, not exact. A row opened from
  // the news is still in the news, and the sidebar still says so.
  mount('/staff/ed/news/42');

  expect(isLit('News')).toBe(true);
  expect(isLit('Dashboard')).toBe(false);
});

test('one department at a time: opening a second one shuts the first', async () => {
  // Carmine, 11 September 2026: nine departments open at once is a panel taller than any screen.
  const user = userEvent.setup();
  mount('/staff/ed/content');

  expect(screen.getByRole('link', { name: /Pages/ })).toBeInTheDocument();

  await user.click(screen.getByRole('button', { name: /Administration/ }));

  expect(screen.getByRole('link', { name: /Permissions/ })).toBeInTheDocument();
  expect(screen.queryByRole('link', { name: /Pages/ })).not.toBeInTheDocument();

  // And clicking the open one shuts it: the only way to have none open, which a panel should allow.
  await user.click(screen.getByRole('button', { name: /Administration/ }));
  expect(screen.queryByRole('link', { name: /Permissions/ })).not.toBeInTheDocument();
});

test('from the collapsed strip, a department opens the panel on that department', async () => {
  // Carmine, 11 September 2026. Collapsed, a square used to be a switch that changed nothing anybody
  // could see — the group toggled inside a panel too narrow to show it.
  const user = userEvent.setup();
  mount('/staff/ed/content');

  await user.click(screen.getByRole('button', { name: englishCommon.nav.sidebar.collapse }));

  const panel = screen.getByRole('complementary');
  expect(panel.className).toContain('w-17');

  // Collapsed, the heading is a square whose name is its tooltip.
  await user.click(screen.getByTitle('Administration'));

  expect(panel.className).toContain('w-72');
  expect(screen.getByRole('link', { name: /Permissions/ })).toBeInTheDocument();
  expect(screen.queryByRole('link', { name: /Pages/ })).not.toBeInTheDocument();
});
