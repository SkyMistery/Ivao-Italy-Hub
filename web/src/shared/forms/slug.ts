/**
 * A title turned into the address it proposes.
 *
 * The rule it aims at is the server's — `^[a-z0-9]+(?:-[a-z0-9]+)*$`, in `ContentWriteDtoValidator`
 * — and the comment there has said from the start that "the editor proposes one from the title".
 * This is that half, arriving late: until the demo of M1, an address had to be typed from scratch
 * next to a title that had just been written.
 *
 * It is a **proposal** and not a validation: nothing here refuses anything, the server does that
 * and answers, and what it answers reaches the field through `useProblemDetails` like every other
 * refusal (design M0 §7.5). So the length limit of the column is deliberately not repeated here —
 * a rule written twice is a rule that changes in one place.
 *
 * Accents are folded rather than dropped: "Città" gives `citta`, which is what somebody typing the
 * address by hand would write, while dropping the letter would give `citt`.
 */
export function slugify(title: string): string {
  return (
    title
      .normalize('NFD')
      // Everything Unicode files as a combining mark: the accents that NFD has just separated.
      .replace(/\p{Mn}/gu, '')
      .toLowerCase()
      .replace(/[^a-z0-9]+/g, '-')
      .replace(/^-+|-+$/g, '')
  );
}
