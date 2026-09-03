/**
 * The closed list of custom components of M0 (design §7.1). Adding one is a decision, written down
 * in `docs/UI-GUIDELINES.md` and added here; it is not something a screen does on its own.
 *
 * The list lives in its own file because two things read it: `/staff/admin/ui-kit`, which mounts
 * every entry, and the test that says the ui-kit is complete. Neither may hold its own copy —
 * a copy is how a component quietly stops being shown.
 */
export const UI_KIT_COMPONENTS = [
  'Hero',
  'SectionHeader',
  'StatTile',
  'PageShell',
  'EmptyState',
  'LocaleSwitcher',
  'LocaleFields',
  'MarkdownContent',
  'DataList',
  'SchemaForm',
  'ProblemAlert',
  'DepartmentBadge',
  'VisibilityBadge',
  'StatusBadge',
  'ConfirmDialog',
] as const;

export type UiKitComponent = (typeof UI_KIT_COMPONENTS)[number];
