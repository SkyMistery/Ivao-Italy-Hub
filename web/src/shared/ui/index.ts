export { Hero, SectionHeader, StatTile, EmptyState } from './layout-pieces';
export { CompactPageShells, PageActions, PageShell, type Crumb } from './PageShell';
export { DepartmentBadge, VisibilityBadge, StatusBadge, RatingBadge, type Visibility } from './badges';
export { LocaleSwitcher } from './LocaleSwitcher';
export { MarkdownContent } from './MarkdownContent';
export { ConfirmDialog } from './ConfirmDialog';
export { Dialog } from './Dialog';
export { Notice } from './Notice';
export { NOTICE_TONES, useNotice, useRefusalNotice, type NoticeTone } from './notices';
export { CalendarView } from './CalendarView';
export { LiveStatusStrip, type LiveNetworkStatus } from './LiveStatusStrip';
export { ContactForm } from './ContactForm';
export { MessageThread, type MessageThreadValue } from './MessageThread';
export { StaffSidebar, type StaffSidebarEntry, type StaffSidebarGroup } from './StaffSidebar';
export { contactSchema, type ContactFormValues } from './contact';
export { isErased, personName, type NamedPerson } from './people';
export {
  CALENDAR_KIND_COLOURS,
  CALENDAR_SCREEN_VIEWS,
  CALENDAR_VIEWS,
  calendarKindColour,
  calendarSpan,
  isCalendarGrid,
  calendarDays,
  calendarWindow,
  type CalendarItem,
  type CalendarKindColour,
  type CalendarViewMode,
} from './calendar';
export { MediaPicker, type PickableMedia, type MediaPage, type MediaLibraryQuery } from './MediaPicker';
export { narrowLibrary, NOTHING_ASKED, type MediaBrowse } from './narrowLibrary';
export { NotFound, Forbidden, Failed, RouteError } from './status-pages';
// ⚠️ The map through its lazy half, and its types with `export type` (issue #400, `LazyRouteMap.tsx`): a value exported
// from './RouteMap' here puts MapLibre back into the entry chunk of every page, and an inline `{ type … }` its stylesheet.
export { RouteMap } from './LazyRouteMap';
export type { RouteMapAirport, RouteMapLeg, RouteMapProps, RouteMapStatus, RouteMapTrack } from './RouteMap';
export { boundsOf, greatCirclePath, trackPath, GREAT_CIRCLE_SEGMENTS, type GeoPoint } from './greatCircle';
export { UI_KIT_COMPONENTS, type UiKitComponent } from './catalog';
