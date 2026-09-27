import { CalendarClock, GraduationCap, Inbox, ListTodo } from 'lucide-react';
import { z } from 'zod';

import type { BlockRegistration } from '../../../shared/modules';
import type { StaffTrainingRowDto, TrainingRatingDto, TrainingState } from '../api';

import { ApprovalQueueBlock, type ApprovalQueueData } from './approvalQueue';
import { MyTrainingBlock, type MyTrainingData } from './myTraining';
import { TrainerQueueBlock, type TrainerQueueData } from './trainerQueue';
import { UpcomingSessionsBlock, type UpcomingSessionsData } from './upcomingSessions';

/**
 * The blocks of the training (design M3 §4.3, A10b), as the manifest lists them. Each has its other half in `TrainingModule.Blocks`
 * on the server, and the manifest test compares the two. All four are always live, because each answers for whoever is looking.
 *
 * The examples the gallery draws are of their own making — ratings, positions and people no network has — and their dates are
 * counted from when the gallery is drawn, so that what is to come stays to come.
 */

const DAY = 24 * 60 * 60 * 1000;

/** A moment this many days from when the gallery is drawn. */
function inDays(days: number): string {
  return new Date(Date.now() + days * DAY).toISOString();
}

/** The example's ratings: one of each ladder, of the example's own making. */
const exampleAtc = {
  kind: 'Atc',
  number: 12,
  shortName: 'A2',
  nameKey: 'ratings.Atc.A2',
} as const satisfies TrainingRatingDto;
const examplePilot = {
  kind: 'Pilot',
  number: 22,
  shortName: 'P2',
  nameKey: 'ratings.Pilot.P2',
} as const satisfies TrainingRatingDto;

const exampleTrainee = { vid: 100001, name: 'Alex Trainee' };
const exampleTrainer = { vid: 100002, name: 'Sam Trainer' };

/** A training as the staff's list shows it, on the example's rating. */
function exampleRow(
  id: number,
  state: TrainingState,
  more: Partial<StaffTrainingRowDto> = {},
): StaffTrainingRowDto {
  return {
    id,
    kind: exampleAtc.kind,
    rating: exampleAtc.number,
    ratingShortName: exampleAtc.shortName,
    isMockExam: false,
    position: 'XXXX_BOX',
    state,
    trainee: exampleTrainee,
    trainer: exampleTrainer,
    createdAt: '2026-09-20T18:00:00.000Z',
    scheduledStartUtc: null,
    held: false,
    ...more,
  };
}

/**
 * The sessions still to be held (§4.1): on `/training`, on a page of the site, on a dashboard. One property, how many at most;
 * nothing written, the ten soonest. Who is in them only for a signed in reader, as the server answers.
 */
export const upcomingSessionsBlock: BlockRegistration = {
  type: 'training.upcomingSessions',
  version: 1,
  kind: 'Data',
  alwaysLive: true,
  schema: z.object({
    // Zero is every session, up to the server's bound: a property that is not written is not a way of asking for none.
    limit: z.number().int().min(0).default(10),
  }),
  component: UpcomingSessionsBlock,
  example: { limit: 5 },
  exampleData: {
    signedIn: true,
    items: [
      {
        id: 41,
        kind: exampleAtc.kind,
        ratingShortName: exampleAtc.shortName,
        position: 'XXXX_BOX',
        startsAtUtc: inDays(2),
        held: false,
        trainee: exampleTrainee,
        trainer: exampleTrainer,
      },
      {
        id: 42,
        kind: examplePilot.kind,
        ratingShortName: examplePilot.shortName,
        position: null,
        startsAtUtc: inDays(5),
        held: false,
        trainee: { vid: 100003, name: 'Robin Pilot' },
        trainer: exampleTrainer,
      },
    ],
  } satisfies UpcomingSessionsData,
  editorLabelKey: 'training:blocks.upcomingSessions.label',
  // Its property is the module's word, not the core's (`BlockRegistration.propertyLabels`).
  propertyLabels: 'training:blocks.upcomingSessions',
  group: 'data',
  icon: CalendarClock,
};

/** The reader's own training, ladder by ladder (§4.3): it belongs on `/me`. No property — it is the reader's —, nothing for a visitor. */
export const myTrainingBlock: BlockRegistration = {
  type: 'training.myTraining',
  version: 1,
  kind: 'Data',
  alwaysLive: true,
  schema: z.object({}),
  component: MyTrainingBlock,
  example: {},
  exampleData: {
    signedIn: true,
    vid: exampleTrainee.vid,
    name: exampleTrainee.name,
    asksTheory: true,
    theoryExamUrl: null,
    paths: [
      {
        kind: exampleAtc.kind,
        ratingShortName: 'A1',
        hours: 120,
        next: exampleAtc,
        isMockExam: false,
        asksPosition: true,
        positions: [],
        refusal: 'training:errors.requestOpen',
        bannedUntil: null,
        openTrainingId: 7,
        waitUntil: null,
        minimumHours: null,
      },
      {
        kind: examplePilot.kind,
        ratingShortName: 'P1',
        hours: 150,
        next: examplePilot,
        isMockExam: true,
        asksPosition: false,
        positions: [],
        refusal: null,
        bannedUntil: null,
        openTrainingId: null,
        waitUntil: null,
        minimumHours: null,
      },
    ],
    trainings: [
      {
        id: 7,
        kind: exampleAtc.kind,
        rating: exampleAtc.number,
        ratingShortName: exampleAtc.shortName,
        isMockExam: false,
        position: 'XXXX_BOX',
        state: 'Assigned',
        rejection: null,
        rejectionReason: null,
        availabilityText: null,
        notesText: null,
        requestedAt: '2026-09-20T18:00:00.000Z',
        decidedAt: '2026-09-21T09:00:00.000Z',
        trainer: exampleTrainer,
        slots: [
          { id: 1, startsAtUtc: inDays(2), endsAtUtc: inDays(2.1) },
          { id: 2, startsAtUtc: inDays(4), endsAtUtc: inDays(4.1) },
        ],
        scheduledStartUtc: null,
        held: false,
        completedAt: null,
        closedAt: null,
        closeReason: null,
        readyForMockExam: false,
        readyForExam: false,
        cooldownWaived: false,
        generalComment: null,
        sheet: [],
        sessions: [],
        rowVersion: '2026-09-21T09:00:00.000000Z',
      },
      {
        id: 5,
        kind: examplePilot.kind,
        rating: examplePilot.number,
        ratingShortName: examplePilot.shortName,
        isMockExam: false,
        position: null,
        state: 'Completed',
        rejection: null,
        rejectionReason: null,
        availabilityText: null,
        notesText: null,
        requestedAt: '2026-09-01T18:00:00.000Z',
        decidedAt: '2026-09-02T09:00:00.000Z',
        trainer: exampleTrainer,
        slots: [],
        scheduledStartUtc: '2026-09-15T19:00:00.000Z',
        held: true,
        completedAt: '2026-09-15T21:30:00.000Z',
        closedAt: null,
        closeReason: null,
        readyForMockExam: true,
        readyForExam: false,
        cooldownWaived: false,
        generalComment: null,
        sheet: [],
        sessions: [],
        rowVersion: '2026-09-15T21:30:00.000000Z',
      },
    ],
  } satisfies MyTrainingData,
  editorLabelKey: 'training:blocks.myTraining.label',
  group: 'data',
  icon: GraduationCap,
};

/**
 * The reader's trainings to move, as their trainer (§4.3): the choices late after the days the division gives, the dates to propose,
 * the reports to write. For `/staff` and the dashboard of the training; no property, nothing for a visitor.
 */
export const trainerQueueBlock: BlockRegistration = {
  type: 'training.trainerQueue',
  version: 1,
  kind: 'Data',
  alwaysLive: true,
  schema: z.object({}),
  component: TrainerQueueBlock,
  example: {},
  exampleData: {
    signedIn: true,
    toPropose: [exampleRow(11, 'Assigned', { trainee: { vid: 100004, name: 'Kim Trainee' } })],
    waiting: [{ training: exampleRow(12, 'Assigned'), days: 4 }],
    toReport: [
      exampleRow(13, 'Scheduled', {
        kind: examplePilot.kind,
        rating: examplePilot.number,
        ratingShortName: examplePilot.shortName,
        position: null,
        trainee: { vid: 100003, name: 'Robin Pilot' },
        scheduledStartUtc: inDays(-1),
        held: true,
      }),
    ],
  } satisfies TrainerQueueData,
  editorLabelKey: 'training:blocks.trainerQueue.label',
  group: 'data',
  icon: ListTodo,
};

/**
 * The requests to accept or refuse and the trainings to assign (§4.3), for whoever may: the dashboard of the training. No property,
 * nothing for a visitor.
 */
export const approvalQueueBlock: BlockRegistration = {
  type: 'training.approvalQueue',
  version: 1,
  kind: 'Data',
  alwaysLive: true,
  schema: z.object({}),
  component: ApprovalQueueBlock,
  example: {},
  exampleData: {
    signedIn: true,
    toApprove: {
      count: 3,
      oldest: [
        exampleRow(21, 'Requested', { trainer: null }),
        exampleRow(22, 'Requested', {
          trainer: null,
          isMockExam: true,
          trainee: { vid: 100004, name: 'Kim Trainee' },
        }),
      ],
    },
    toAssign: {
      count: 1,
      oldest: [exampleRow(23, 'Accepted', { trainer: null, trainee: { vid: 100003, name: 'Robin Pilot' } })],
    },
  } satisfies ApprovalQueueData,
  editorLabelKey: 'training:blocks.approvalQueue.label',
  group: 'data',
  icon: Inbox,
};
