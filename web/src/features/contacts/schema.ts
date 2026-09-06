import { z } from 'zod';

import { CONTACT_STATUSES } from './queries';

/**
 * The form of the back office: where a message has got to, and nothing else.
 *
 * What is *not* here is the point of the phase. A department reads a message and moves it along; it
 * does not edit what somebody wrote to it, and the way that is said is that the payload has no
 * subject and no body — on the screen and on the server alike (`ContactStatusWriteDto`). A read
 * only field would have been the same rule written twice, in a place where only one of the two
 * copies is enforced.
 */
export const contactStatusSchema = z.object({
  status: z.enum(CONTACT_STATUSES),
  // The version the screen was loaded with. Sending back a stale one is how the server finds out
  // that somebody else moved the message first, and answers 409.
  rowVersion: z.string().meta({ hidden: true }),
});

export type ContactStatusFormValues = z.output<typeof contactStatusSchema>;
