import { screen } from '@testing-library/react';
import { expect, test } from 'vitest';

import { SchemaForm } from '../../../shared/forms';
import { renderWithProviders } from '../../../test/harness';
import { activitySchema, emptyActivity } from '../schemas';

/**
 * The form of a parallel activity (E17) as the page draws it: its window is two instants, which `SchemaForm` draws only with the
 * division's zone beside them (E10p) — the round of M4c found the page breaking without it, and no other test drew the form.
 */
test('the form of a new activity draws its fields, the window with the division in it', () => {
  renderWithProviders(
    <SchemaForm
      schema={activitySchema}
      defaults={emptyActivity(3, '2026-11-05T09:00:00Z', '2026-11-05T18:00:00Z', ['it', 'en'])}
      locales={['it', 'en']}
      labels="events:activities"
      division={{ defaultLocale: 'it', timezone: 'Europe/Rome' }}
      submitLabel="Save"
      onSubmit={() => Promise.resolve()}
    />,
  );

  for (const field of ['fromUtc', 'toUtc', 'slotMinutes', 'seatsPerSlot']) {
    expect(document.getElementById(field)).not.toBeNull();
  }
  expect(screen.getByRole('button', { name: 'Save' })).toBeTruthy();
});
