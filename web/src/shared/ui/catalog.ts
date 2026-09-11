/**
 * The closed list of custom components (design M0 §7.1, design M1 §12). Adding one is a decision, written down
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
  // The fifth added since the list was written, and the first since M1 closed with the four it
  // predicted: Carmine asked for it after the demo, because an editor that saved said nothing.
  'Notice',
  'MediaPicker',
  'CalendarView',
  'ContactForm',
  'LiveStatusStrip',
  // The twenty-first, and the second Carmine has asked for: Atmosphere's sidebar draws its own
  // collapse button, at the bottom, full width, with an English word written into the library.
  'StaffSidebar',
] as const;

export type UiKitComponent = (typeof UI_KIT_COMPONENTS)[number];
