/**
 * How the public screen reads the days a document carries (G14): in force from, review by, retired
 * since.
 */

/**
 * A day of the row — in force from, review by, retired since — as `YYYY-MM-DD`, whatever the
 * server wrote after it.
 *
 * ⚠️ The columns are `date` and the server serialises them as a midnight with no zone,
 * `2026-10-01T00:00:00`; `new Date()` reads that as **local** midnight, and formatted in UTC it
 * becomes the day before in every zone east of Greenwich. So the day is read as text and only
 * ever compared and formatted as a day.
 */
export function dayOf(value: string): string {
  return value.slice(0, 10);
}

/** The same day as an instant a formatter can be given, at midnight UTC. */
export function dayInstant(value: string): string {
  return `${dayOf(value)}T00:00:00Z`;
}
