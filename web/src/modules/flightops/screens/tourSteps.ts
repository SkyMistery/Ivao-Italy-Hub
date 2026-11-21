import { TOUR_RULE_FIELDS, TOUR_STEPS, type TOUR_KINDS, type TourStep } from '../schemas';

/**
 * The steps of a tour's editor as plain facts (design 11 §5), apart from what draws them, so that they are tested on
 * their own: which steps a tour has, which step a problem of "ready" sends the staff to, and which step follows which.
 */

type TourKind = (typeof TOUR_KINDS)[number];

/** The kinds whose route is a set of legs: an Open tour is flown anywhere, a container through its subtours (§2.6, §2.7). */
const KINDS_WITHOUT_LEGS: readonly TourKind[] = ['Open', 'Container'];

/**
 * The steps a tour has, in their order. A template has no route but an Open one's goal — it carries no legs, no hubs and
 * no subtours (design M2 §1.10) — and is never marked ready, so it has no check.
 */
export function tourSteps({ kind, isTemplate }: { kind: TourKind; isTemplate: boolean }): TourStep[] {
  return TOUR_STEPS.filter((step) => {
    if (step === 'route') {
      return !isTemplate || kind === 'Open';
    }

    return step !== 'check' || !isTemplate;
  });
}

/** What the route step of a tour of this kind holds, in the order it draws them. */
export function routeParts({
  kind,
  isTemplate,
}: {
  kind: TourKind;
  isTemplate: boolean;
}): ('legs' | 'hubs' | 'subtours' | 'goal')[] {
  if (kind === 'Open') {
    return ['goal'];
  }

  if (isTemplate) {
    return [];
  }

  return [
    ...(kind === 'Hub' ? (['hubs'] as const) : []),
    ...(KINDS_WITHOUT_LEGS.includes(kind) ? [] : (['legs'] as const)),
    ...(kind === 'Container' ? (['subtours'] as const) : []),
  ];
}

/** The first part of a problem's field: `legs.3` is about the legs, `briefing.sections[0]…` about the briefing. */
function head(field: string): string {
  return field.split(/[.[]/)[0] ?? field;
}

const ROUTE_FIELDS: readonly string[] = [
  'legs',
  'hubs',
  'rotations',
  'subtours',
  'openGoal',
  'openGoalParameters',
];

/** The rows of a tour's own that are rules of how it is flown, besides the fields of its form that are (#369). */
const RULE_ROWS: readonly string[] = ['constraints', 'callsigns', 'rules'];

/**
 * The step a problem of "ready" is fixed in, read off the field the server files it under (`TourReadiness`, `TourShape`):
 * where that field is drawn. The kind is its step; the legs, the hubs and their rotations, the subtours and the goal are
 * the route; the fields of the form that are rules and the rows of rules, constraints and callsigns are the rules; the
 * briefing is its own; "a template is never ready" is the check's. Everything else is the tour's name, dates and pictures.
 */
export function stepOfField(field: string): TourStep {
  const first = head(field);

  if (first === 'kind') {
    return 'kind';
  }

  if (ROUTE_FIELDS.includes(first)) {
    return 'route';
  }

  if ((TOUR_RULE_FIELDS as readonly string[]).includes(first) || RULE_ROWS.includes(first)) {
    return 'rules';
  }

  if (first === 'briefing') {
    return 'briefing';
  }

  return first === 'status' ? 'check' : 'identity';
}

/**
 * The step a problem is shown at among the steps this tour has: one filed under a step the tour lacks — a template has no
 * route — is shown at the check, where every problem is listed anyway.
 */
export function stepOfProblem(field: string, steps: readonly TourStep[]): TourStep {
  const step = stepOfField(field);

  return steps.includes(step) ? step : 'check';
}

/** The steps that have something to fix before "ready": those any problem of the server is filed at. */
export function stepsToFix(fields: readonly string[], steps: readonly TourStep[]): Set<TourStep> {
  return new Set(fields.map((field) => stepOfProblem(field, steps)));
}

/** The step before and the step after this one, among the steps the tour has; null at either end. */
export function neighbours(
  steps: readonly TourStep[],
  step: TourStep,
): { previous: TourStep | null; next: TourStep | null } {
  const at = steps.indexOf(step);

  return {
    previous: at > 0 ? (steps[at - 1] ?? null) : null,
    next: at >= 0 && at < steps.length - 1 ? (steps[at + 1] ?? null) : null,
  };
}

/**
 * The step a tour's screen opens at when the address names none: a new tour at its kind, or at its name once the kind is
 * chosen; one that exists at its name, dates and pictures — what the settings tab held before the steps. A step the tour
 * does not have — `?tab=route` on a template — opens the same way.
 */
export function openStep({
  asked,
  steps,
  isNew,
  kindChosen,
}: {
  asked: TourStep | undefined;
  steps: readonly TourStep[];
  isNew: boolean;
  kindChosen: boolean;
}): TourStep {
  if (asked !== undefined && steps.includes(asked)) {
    return asked;
  }

  return isNew && !kindChosen ? 'kind' : 'identity';
}
