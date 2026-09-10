import {
  Award,
  BookOpen,
  Calendar,
  CheckCircle2,
  ClipboardList,
  Clock,
  Cloud,
  Compass,
  Download,
  ExternalLink,
  FileText,
  Globe,
  GraduationCap,
  Headphones,
  Heart,
  Info,
  Landmark,
  LifeBuoy,
  Mail,
  Map,
  MapPin,
  MessageCircle,
  Newspaper,
  Plane,
  PlaneLanding,
  PlaneTakeoff,
  Radio,
  Radar,
  Shield,
  Sparkles,
  Star,
  TowerControl,
  Trophy,
  Users,
  Wrench,
} from 'lucide-react';

import type { ComponentProps, ComponentType } from 'react';

import { DiscordMark, FacebookMark, InstagramMark, XMark, YoutubeMark } from './brands';

/**
 * The icons an editor may choose from, by name.
 *
 * Two rules from `docs/UI-GUIDELINES.md` meet here. Icons come from `lucide`, which ships with
 * Atmosphere — so nobody draws an `<svg>` in a screen. And an icon a block carries is picked from
 * an **allow list**, not typed: `lucide` ships thousands of names, and a select over thousands is a
 * text box with more steps (design M1 §1.5).
 *
 * It starts at the size of what the site actually says — flying, controlling, training, events,
 * documents, contact — and grows when a page needs something it has not got. Growing it is adding a
 * line here; it is not a decision, because the set it draws from is already decided.
 *
 * ⚠️ This lives in `shared/` and not in `blocks/`, where the implementation plan first put it, for
 * one reason: `blocks/` already imports the form generator (for `localized()`), so the generator
 * importing `blocks/` would close a circle between the two. An allow list of icons is a UI asset
 * and belongs on the generic side; `blocks/` reads it from here.
 *
 * The folder is also where an icon `lucide` genuinely lacks would be drawn by hand (design M1
 * §1.4). None has been missing so far, so there is none.
 */
/**
 * ⚠️ Typed on what every drawer of an icon actually needs — an `<svg>` component — and not on
 * `LucideIcon`, because five of these are not lucide's: it dropped every brand mark at version 1 and
 * they are drawn in './brands'. The narrower type would have made the set impossible to complete.
 */
export const ICONS: Readonly<Record<string, ComponentType<ComponentProps<'svg'>>>> = {
  award: Award,
  bookOpen: BookOpen,
  calendar: Calendar,
  checkCircle: CheckCircle2,
  clipboardList: ClipboardList,
  clock: Clock,
  cloud: Cloud,
  compass: Compass,
  download: Download,
  externalLink: ExternalLink,
  fileText: FileText,
  globe: Globe,
  graduationCap: GraduationCap,
  headphones: Headphones,
  heart: Heart,
  info: Info,
  landmark: Landmark,
  lifeBuoy: LifeBuoy,
  mail: Mail,
  map: Map,
  mapPin: MapPin,
  messageCircle: MessageCircle,
  newspaper: Newspaper,
  plane: Plane,
  planeLanding: PlaneLanding,
  planeTakeoff: PlaneTakeoff,
  radar: Radar,
  radio: Radio,
  shield: Shield,
  sparkles: Sparkles,
  star: Star,
  towerControl: TowerControl,
  trophy: Trophy,
  users: Users,
  wrench: Wrench,

  // The five `lucide` dropped when it reached version 1, drawn in './brands'. They are here and
  // not in a list of their own because everything that offers an icon offers the same set.
  discord: DiscordMark,
  facebook: FacebookMark,
  instagram: InstagramMark,
  x: XMark,
  youtube: YoutubeMark,
};

/** The names, in the order the select shows them. */
export const ICON_NAMES = Object.keys(ICONS);

/**
 * The component for a name, or null. Null rather than a fallback icon on purpose: a name that is
 * not on the list is a page written by a newer release, and drawing a wrong picture is worse than
 * drawing none.
 */
export function iconByName(name: string | null | undefined): ComponentType<ComponentProps<'svg'>> | null {
  return name === null || name === undefined ? null : (ICONS[name] ?? null);
}
