import type { ReactNode } from 'react';

import { registry } from '../../app/registry';
import type { Bootstrap } from '../../shared/api/bootstrap';
import {
  DepartmentBadge,
  LocaleSwitcher,
  StatusBadge,
  VisibilityBadge,
  type UiKitComponent,
} from '../../shared/ui';

import {
  ConfirmDialogSample,
  DataListSample,
  EmptyStateSample,
  HeroSample,
  LocaleFieldsSample,
  MarkdownSample,
  PageShellSample,
  ProblemAlertSample,
  SchemaFormSample,
  SectionHeaderSample,
  StatTileSample,
} from './uiKitSamples';

/**
 * The order of the gallery, as data. It is a list and not markup so that a test can read it without
 * mounting a router, a query client and an i18n instance — and so that "is every component shown?"
 * is a question with an answer rather than a thing somebody remembers to check.
 */

/** One entry of the kit. */
export interface UiKitSection {
  readonly name: string;
  readonly render: (bootstrap: Bootstrap) => ReactNode;
}

/**
 * Every name here is a `UiKitComponent`, so a section for something that is not on the closed list
 * of design §7.1 does not compile; a component on the list with no section fails the test next to
 * this file. The two together are what make the gallery complete rather than nearly complete.
 */
export const UI_KIT_SECTIONS: readonly UiKitSection[] = [
  { name: 'Hero' satisfies UiKitComponent, render: () => <HeroSample /> },
  { name: 'SectionHeader' satisfies UiKitComponent, render: () => <SectionHeaderSample /> },
  { name: 'StatTile' satisfies UiKitComponent, render: () => <StatTileSample /> },
  { name: 'PageShell' satisfies UiKitComponent, render: () => <PageShellSample /> },
  { name: 'EmptyState' satisfies UiKitComponent, render: () => <EmptyStateSample /> },
  {
    name: 'LocaleSwitcher' satisfies UiKitComponent,
    render: (bootstrap) => (
      <LocaleSwitcher locales={bootstrap.division.locales} signedIn={bootstrap.user !== null} />
    ),
  },
  {
    name: 'LocaleFields' satisfies UiKitComponent,
    render: (bootstrap) => <LocaleFieldsSample locales={bootstrap.division.locales} />,
  },
  { name: 'MarkdownContent' satisfies UiKitComponent, render: () => <MarkdownSample /> },
  {
    name: 'DataList' satisfies UiKitComponent,
    render: (bootstrap) => <DataListSample bootstrap={bootstrap} />,
  },
  {
    name: 'SchemaForm' satisfies UiKitComponent,
    render: (bootstrap) => <SchemaFormSample locales={bootstrap.division.locales} />,
  },
  { name: 'ProblemAlert' satisfies UiKitComponent, render: () => <ProblemAlertSample /> },
  {
    name: 'DepartmentBadge' satisfies UiKitComponent,
    render: () => <DepartmentBadge department="ED" />,
  },
  {
    name: 'VisibilityBadge' satisfies UiKitComponent,
    render: () => (
      <div className="flex flex-wrap gap-2">
        <VisibilityBadge visibility="Public" />
        <VisibilityBadge visibility="Members" />
        <VisibilityBadge visibility="Staff" />
        <VisibilityBadge visibility="Department" />
      </div>
    ),
  },
  {
    name: 'StatusBadge' satisfies UiKitComponent,
    render: () => (
      <div className="flex gap-2">
        <StatusBadge active />
        <StatusBadge active={false} />
      </div>
    ),
  },
  { name: 'ConfirmDialog' satisfies UiKitComponent, render: () => <ConfirmDialogSample /> },
];

/**
 * The blocks of the registry, each with the example props it registered.
 *
 * A data block is mounted with its `exampleData` rather than with an answer from the server: the
 * gallery is a page about the components, and a block that called the API here would show whatever
 * this installation happens to hold today — or nothing at all, on a fresh one.
 */
export const UI_KIT_BLOCKS: readonly UiKitSection[] = registry.blocks.map((block) => ({
  name: block.type,
  render: () => <block.component props={block.example} data={block.exampleData ?? null} />,
}));
