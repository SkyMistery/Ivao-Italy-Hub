import {
  Building2,
  CalendarDays,
  ChartColumn,
  ChevronsUpDown,
  FileText,
  Frame,
  Heading,
  Image,
  Images,
  Info,
  LayoutGrid,
  Link2,
  Milestone,
  Minus,
  MousePointerClick,
  MoveVertical,
  Newspaper,
  PanelTop,
  PanelsTopLeft,
  Pilcrow,
  Pointer,
  Quote,
  Radio,
  Shapes,
  Table,
  Users,
  Video,
} from 'lucide-react';

import type { BlockRegistration } from '../shared/modules';

import {
  AccordionBlock,
  ButtonGroupBlock,
  CalendarBlock,
  CalloutBlock,
  CardGridBlock,
  CtaBlock,
  DividerBlock,
  DocumentListBlock,
  EmbedBlock,
  GalleryBlock,
  HeadingBlock,
  HeroBlock,
  IconGridBlock,
  ImageBlock,
  LinkListBlock,
  LogoGridBlock,
  NetworkStatsBlock,
  NewsListBlock,
  SpacerBlock,
  StaffListBlock,
  StatsBlock,
  TableBlock,
  TabsBlock,
  TestimonialBlock,
  TextBlock,
  TimelineBlock,
  VideoBlock,
} from './blocks';
import {
  accordionSchema,
  buttonGroupSchema,
  calendarSchema,
  calloutSchema,
  cardGridSchema,
  ctaSchema,
  dividerSchema,
  documentListSchema,
  embedSchema,
  gallerySchema,
  headingSchema,
  heroSchema,
  iconGridSchema,
  imageSchema,
  linkListSchema,
  logoGridSchema,
  networkStatsSchema,
  newsListSchema,
  spacerSchema,
  staffListSchema,
  statsSchema,
  tableSchema,
  tabsSchema,
  testimonialSchema,
  textSchema,
  timelineSchema,
  videoSchema,
} from './schemas';

/**
 * The blocks of the core, tied together: a type, a schema, a component, an icon, the i18n key of
 * its name, and example properties for the gallery (design M0 §5.4).
 *
 * The server declares the same ones as `IBlockDescriptor`s and publishes them in `/api/me`. The
 * halves are deliberately separate — what a block *is* lives here, what it *means* lives only in
 * TypeScript (CLAUDE.md §2) — and the ui-kit is where a mismatch shows up.
 *
 * A block costs exactly five things and not one more (design M1 §1.3): a schema, a component, a
 * registration here, its i18n keys in every language, and — for a data block — a provider. In
 * particular nobody adds a section to the gallery: it mounts whatever is declared here, which is
 * the property it exists for.
 */

export const CORE_BLOCK_TYPES = {
  heading: 'heading',
  text: 'text',
  callout: 'callout',
  cta: 'cta',
  linkList: 'linkList',
  hero: 'hero',
  image: 'image',
  video: 'video',
  embed: 'embed',
  timeline: 'timeline',
  table: 'table',
  cardGrid: 'cardGrid',
  iconGrid: 'iconGrid',
  gallery: 'gallery',
  logoGrid: 'logoGrid',
  tabs: 'tabs',
  accordion: 'accordion',
  testimonial: 'testimonial',
  buttonGroup: 'buttonGroup',
  spacer: 'spacer',
  divider: 'divider',
  stats: 'stats',
  networkStats: 'networkStats',
  calendar: 'calendar',
  newsList: 'newsList',
  documentList: 'documentList',
  staffList: 'staffList',
} as const;

/** Two languages of prose, written once and read by the examples below. */
const sample = {
  title: { en: 'What we do', it: 'Cosa facciamo' },
  lead: {
    en: 'A sentence of the kind a page of this hub is written in.',
    it: 'Una frase del genere di quelle con cui è scritta una pagina di questo hub.',
  },
};

export const coreBlockRegistrations: readonly BlockRegistration[] = [
  {
    type: CORE_BLOCK_TYPES.heading,
    version: 1,
    kind: 'Content',
    schema: headingSchema,
    component: HeadingBlock,
    example: { level: 2, text: { en: 'A heading', it: 'Un titolo' } },
    editorLabelKey: 'blocks.heading.label',
    icon: Heading,
  },
  {
    type: CORE_BLOCK_TYPES.text,
    version: 1,
    kind: 'Content',
    schema: textSchema,
    component: TextBlock,
    example: {
      markdown: {
        en: 'A paragraph with **bold** and a [link](https://www.ivao.aero).',
        it: 'Un paragrafo con **grassetto** e un [link](https://www.ivao.aero).',
      },
    },
    editorLabelKey: 'blocks.text.label',
    icon: Pilcrow,
  },
  {
    type: CORE_BLOCK_TYPES.callout,
    version: 1,
    kind: 'Content',
    schema: calloutSchema,
    component: CalloutBlock,
    example: {
      tone: 'info',
      title: { en: 'Worth knowing', it: 'Da sapere' },
      text: {
        en: 'Something the reader should not miss.',
        it: 'Qualcosa che il lettore non deve perdere.',
      },
    },
    editorLabelKey: 'blocks.callout.label',
    icon: Info,
  },
  {
    type: CORE_BLOCK_TYPES.cta,
    version: 1,
    kind: 'Content',
    schema: ctaSchema,
    component: CtaBlock,
    example: {
      label: { en: 'Start here', it: 'Comincia da qui' },
      href: 'https://www.ivao.aero',
    },
    editorLabelKey: 'blocks.cta.label',
    icon: MousePointerClick,
  },
  {
    type: CORE_BLOCK_TYPES.linkList,
    version: 1,
    kind: 'Data',
    schema: linkListSchema,
    component: LinkListBlock,
    example: { category: '', limit: 10 },
    // The gallery is a page about the components: a data block shows this rather than whatever
    // this installation happens to hold today, or nothing at all on a fresh one.
    exampleData: {
      items: [
        {
          title: { en: 'IVAO', it: 'IVAO' },
          url: 'https://www.ivao.aero',
          description: { en: 'The network itself.', it: 'La rete stessa.' },
        },
      ],
    },
    editorLabelKey: 'blocks.linkList.label',
    icon: Link2,
  },

  // --- Content ---------------------------------------------------------------------------------

  {
    type: CORE_BLOCK_TYPES.hero,
    version: 1,
    kind: 'Content',
    schema: heroSchema,
    component: HeroBlock,
    example: {
      eyebrow: { en: 'The division', it: 'La divisione' },
      title: sample.title,
      text: sample.lead,
      align: 'left',
      tone: 'muted',
      primary: { label: { en: 'Start here', it: 'Comincia da qui' }, href: '/start' },
    },
    editorLabelKey: 'blocks.hero.label',
    icon: PanelTop,
  },
  {
    type: CORE_BLOCK_TYPES.image,
    version: 1,
    kind: 'Content',
    schema: imageSchema,
    component: ImageBlock,
    // The gallery shows the block with the first file of the library, whichever it is: there is no
    // fixture picture in the repository, and a made up address would be a broken one everywhere.
    example: { mediaId: 1, caption: { en: 'A picture', it: "Un'immagine" }, width: 'full', rounded: true },
    editorLabelKey: 'blocks.image.label',
    icon: Image,
  },
  {
    type: CORE_BLOCK_TYPES.video,
    version: 1,
    kind: 'Content',
    schema: videoSchema,
    component: VideoBlock,
    // A file of the library rather than an address: the gallery is a page of the back office, and
    // it should not quietly call a site on the other side of the world every time somebody opens
    // it. The other half — an address turned into a player — is what `allowlist.test.ts` is for.
    example: { mediaId: 1, aspect: '16x9' },
    editorLabelKey: 'blocks.video.label',
    icon: Video,
  },
  {
    type: CORE_BLOCK_TYPES.embed,
    version: 1,
    kind: 'Content',
    schema: embedSchema,
    component: EmbedBlock,
    example: {
      url: 'https://player.vimeo.com/video/76979871',
      title: { en: 'An embedded page', it: 'Una pagina incorporata' },
      height: 360,
    },
    editorLabelKey: 'blocks.embed.label',
    icon: Frame,
  },
  {
    type: CORE_BLOCK_TYPES.timeline,
    version: 1,
    kind: 'Content',
    schema: timelineSchema,
    component: TimelineBlock,
    example: {
      variant: 'steps',
      items: [
        {
          title: { en: 'Sign up', it: 'Iscriviti' },
          text: { en: 'It takes a minute.', it: 'Ci vuole un minuto.' },
          icon: 'checkCircle',
        },
        {
          title: { en: 'Fly', it: 'Vola' },
          text: { en: 'Anywhere, any day.', it: 'Ovunque, ogni giorno.' },
          icon: 'plane',
        },
      ],
    },
    editorLabelKey: 'blocks.timeline.label',
    icon: Milestone,
  },
  {
    type: CORE_BLOCK_TYPES.table,
    version: 1,
    kind: 'Content',
    schema: tableSchema,
    component: TableBlock,
    example: {
      caption: { en: 'Opening hours', it: 'Orari' },
      columns: [
        { label: { en: 'Day', it: 'Giorno' }, align: 'left' },
        { label: { en: 'From', it: 'Dalle' }, align: 'right' },
      ],
      rows: [
        { cells: [{ text: { en: 'Monday', it: 'Lunedì' } }, { text: { en: '18:00z', it: '18:00z' } }] },
        { cells: [{ text: { en: 'Friday', it: 'Venerdì' } }, { text: { en: '20:00z', it: '20:00z' } }] },
      ],
    },
    editorLabelKey: 'blocks.table.label',
    icon: Table,
  },

  // --- Layout and containers -------------------------------------------------------------------

  {
    type: CORE_BLOCK_TYPES.cardGrid,
    version: 1,
    kind: 'Content',
    schema: cardGridSchema,
    component: CardGridBlock,
    example: {
      columns: 3,
      cards: [
        { title: { en: 'Fly', it: 'Vola' }, text: sample.lead, icon: 'plane' },
        { title: { en: 'Control', it: 'Controlla' }, text: sample.lead, icon: 'towerControl' },
        { title: { en: 'Learn', it: 'Impara' }, text: sample.lead, icon: 'graduationCap' },
      ],
    },
    editorLabelKey: 'blocks.cardGrid.label',
    icon: LayoutGrid,
  },
  {
    type: CORE_BLOCK_TYPES.iconGrid,
    version: 1,
    kind: 'Content',
    schema: iconGridSchema,
    component: IconGridBlock,
    example: {
      columns: 3,
      items: [
        { icon: 'radar', title: { en: 'Radar', it: 'Radar' }, text: sample.lead },
        { icon: 'headphones', title: { en: 'Support', it: 'Supporto' }, text: sample.lead },
        { icon: 'award', title: { en: 'Awards', it: 'Riconoscimenti' }, text: sample.lead },
      ],
    },
    editorLabelKey: 'blocks.iconGrid.label',
    icon: Shapes,
  },
  {
    type: CORE_BLOCK_TYPES.gallery,
    version: 1,
    kind: 'Content',
    schema: gallerySchema,
    component: GalleryBlock,
    example: { images: [{ mediaId: 1 }, { mediaId: 2 }, { mediaId: 3 }], columns: 3, lightbox: true },
    editorLabelKey: 'blocks.gallery.label',
    icon: Images,
  },
  {
    type: CORE_BLOCK_TYPES.logoGrid,
    version: 1,
    kind: 'Content',
    schema: logoGridSchema,
    component: LogoGridBlock,
    example: {
      columns: 4,
      items: [
        { mediaId: 1, name: 'A partner' },
        { mediaId: 2, name: 'Another partner' },
      ],
    },
    editorLabelKey: 'blocks.logoGrid.label',
    icon: Building2,
  },
  {
    type: CORE_BLOCK_TYPES.tabs,
    version: 1,
    kind: 'Content',
    schema: tabsSchema,
    component: TabsBlock,
    example: {
      tabs: [
        {
          label: { en: 'Pilots', it: 'Piloti' },
          body: {
            en: 'Markdown per tab, and **never** blocks inside a block.',
            it: 'Markdown per scheda, e **mai** blocchi dentro un blocco.',
          },
        },
        {
          label: { en: 'Controllers', it: 'Controllori' },
          body: { en: 'A second tab.', it: 'Una seconda scheda.' },
        },
      ],
    },
    editorLabelKey: 'blocks.tabs.label',
    icon: PanelsTopLeft,
  },
  {
    type: CORE_BLOCK_TYPES.accordion,
    version: 1,
    kind: 'Content',
    schema: accordionSchema,
    component: AccordionBlock,
    example: {
      allowMultiple: false,
      items: [
        {
          question: { en: 'How do I join?', it: 'Come mi iscrivo?' },
          answer: { en: 'From the network itself.', it: 'Dalla rete stessa.' },
        },
        {
          question: { en: 'Is it free?', it: 'È gratis?' },
          answer: { en: 'Yes.', it: 'Sì.' },
        },
      ],
    },
    editorLabelKey: 'blocks.accordion.label',
    icon: ChevronsUpDown,
  },

  // --- Interactive and structure ---------------------------------------------------------------

  {
    type: CORE_BLOCK_TYPES.testimonial,
    version: 1,
    kind: 'Content',
    schema: testimonialSchema,
    component: TestimonialBlock,
    example: {
      quote: {
        en: 'The best evening I have spent on a frequency.',
        it: 'La serata migliore che abbia passato su una frequenza.',
      },
      author: 'A member',
      role: { en: 'Pilot', it: 'Pilota' },
    },
    editorLabelKey: 'blocks.testimonial.label',
    icon: Quote,
  },
  {
    type: CORE_BLOCK_TYPES.buttonGroup,
    version: 1,
    kind: 'Content',
    schema: buttonGroupSchema,
    component: ButtonGroupBlock,
    example: {
      align: 'left',
      buttons: [
        { label: { en: 'Start here', it: 'Comincia da qui' }, href: '/start', variant: 'primary' },
        { label: { en: 'Read more', it: 'Approfondisci' }, href: '/about', variant: 'secondary' },
      ],
    },
    editorLabelKey: 'blocks.buttonGroup.label',
    icon: Pointer,
  },
  {
    type: CORE_BLOCK_TYPES.spacer,
    version: 1,
    kind: 'Content',
    schema: spacerSchema,
    component: SpacerBlock,
    example: { size: 'md' },
    editorLabelKey: 'blocks.spacer.label',
    icon: MoveVertical,
  },
  {
    type: CORE_BLOCK_TYPES.divider,
    version: 1,
    kind: 'Content',
    schema: dividerSchema,
    component: DividerBlock,
    example: { variant: 'line', spacing: 'md' },
    editorLabelKey: 'blocks.divider.label',
    icon: Minus,
  },

  // --- Data --------------------------------------------------------------------------------------
  //
  // Six blocks that draw what the hub knows rather than what an editor typed. Each one has an
  // `IDataBlockProvider` registered for the same type on the server — the fifth thing a block costs
  // (design M1 §1.3) — and an `exampleData`, because the gallery is a page about the components and
  // must not show whatever this installation happens to hold today, or nothing at all on a fresh one.

  {
    type: CORE_BLOCK_TYPES.stats,
    version: 1,
    kind: 'Data',
    schema: statsSchema,
    component: StatsBlock,
    example: {
      columns: 3,
      metrics: [{ metric: 'knownMembers' }, { metric: 'staffMembers' }, { metric: 'publishedNews' }],
    },
    exampleData: {
      metrics: [
        { metric: 'knownMembers', value: 1284 },
        { metric: 'staffMembers', value: 37 },
        { metric: 'publishedNews', value: 96 },
      ],
    },
    editorLabelKey: 'blocks.stats.label',
    icon: ChartColumn,
  },
  {
    type: CORE_BLOCK_TYPES.networkStats,
    version: 1,
    kind: 'Data',
    // The one block of the set that is never captured. The editor does not offer the choice and
    // publication does not freeze it: a picture of who is online, kept from the day a page was
    // published, is an expired figure passed off as the present one (design M1 §1.5).
    alwaysLive: true,
    schema: networkStatsSchema,
    component: NetworkStatsBlock,
    example: {
      figures: [{ figure: 'divisionAtc' }, { figure: 'divisionPilots' }],
      showPositions: true,
    },
    exampleData: {
      updatedAt: '2026-09-06T18:00:00.000Z',
      figures: [
        { figure: 'divisionAtc', value: 3 },
        { figure: 'divisionPilots', value: 22 },
      ],
      positions: [
        { callsign: 'LIRR_CTR', station: 'LIRR', frequency: '129.075' },
        { callsign: 'LIMC_APP', station: 'LIMC', frequency: '126.550' },
      ],
    },
    editorLabelKey: 'blocks.networkStats.label',
    icon: Radio,
  },
  {
    type: CORE_BLOCK_TYPES.calendar,
    version: 1,
    kind: 'Data',
    schema: calendarSchema,
    component: CalendarBlock,
    example: { kinds: [], range: 'month', limit: 5 },
    exampleData: {
      items: [
        {
          id: 1,
          kind: 'meeting',
          title: { en: 'Staff meeting', it: 'Riunione dello staff' },
          description: { en: 'On the division voice server.', it: 'Sul server vocale della divisione.' },
          startsAt: '2026-09-20T19:00:00.000Z',
          endsAt: '2026-09-20T20:30:00.000Z',
          allDay: false,
          department: 'HQ',
          url: null,
        },
      ],
    },
    editorLabelKey: 'blocks.calendar.label',
    icon: CalendarDays,
  },
  {
    type: CORE_BLOCK_TYPES.newsList,
    version: 1,
    kind: 'Data',
    schema: newsListSchema,
    component: NewsListBlock,
    example: { category: '', limit: 3, layout: 'cards', pinnedFirst: true },
    exampleData: {
      items: [
        {
          id: 1,
          title: { en: 'A season of events', it: 'Una stagione di eventi' },
          summary: sample.lead,
          url: '/news/a-season-of-events',
          category: null,
          publishedAt: '2026-09-01T08:00:00.000Z',
          coverMediaId: null,
          pinned: true,
        },
      ],
    },
    editorLabelKey: 'blocks.newsList.label',
    icon: Newspaper,
  },
  {
    type: CORE_BLOCK_TYPES.documentList,
    version: 1,
    kind: 'Data',
    schema: documentListSchema,
    component: DocumentListBlock,
    example: { category: '', limit: 10, groupByCategory: true },
    exampleData: {
      items: [
        {
          id: 1,
          title: { en: 'Local procedures', it: 'Procedure locali' },
          summary: null,
          url: '/documents/local-procedures',
          category: null,
          publishedAt: '2026-08-12T08:00:00.000Z',
          fileMediaId: null,
          sort: 0,
        },
      ],
    },
    editorLabelKey: 'blocks.documentList.label',
    icon: FileText,
  },
  {
    type: CORE_BLOCK_TYPES.staffList,
    version: 1,
    kind: 'Data',
    schema: staffListSchema,
    component: StaffListBlock,
    example: { includeFirStaff: true, layout: 'cards' },
    exampleData: {
      groups: [
        {
          department: 'ED',
          fir: null,
          members: [{ vid: 100001, name: 'A member of the staff', position: 'XX-EC', level: 'Coordinator' }],
        },
      ],
    },
    editorLabelKey: 'blocks.staffList.label',
    icon: Users,
  },
];
