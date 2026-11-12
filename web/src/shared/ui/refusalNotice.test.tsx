import { Button } from '@ivao/atmosphere-react';
import { QueryClient } from '@tanstack/react-query';
import { fireEvent, render, screen } from '@testing-library/react';
import { expect, test } from 'vitest';

import { HubProviders } from '../../app/Providers';
import { ApiError } from '../api/problem';
import { createTestI18n } from '../../test/harness';

import { useRefusalNotice } from './notices';

/**
 * A write with no form to show a refusal on — a delete button, a switch, a line of a queue — said nothing when the server
 * refused it (issue #332, item 4): the dialog closed and nothing happened. Now the refusal is said in the corner, with the
 * server's own reason.
 */
test('a refusal of the server is said in the corner, with its reason', async () => {
  const i18n = createTestI18n();
  await i18n.changeLanguage('en');

  function Screen() {
    const refused = useRefusalNotice();
    return (
      <Button
        type="button"
        onClick={() =>
          refused(
            new ApiError(400, {
              title: 'One or more validation errors occurred.',
              status: 400,
              errors: { id: ['errors.awards.assigned'] },
            }),
          )
        }
      >
        delete
      </Button>
    );
  }

  render(
    <HubProviders i18n={i18n} queryClient={new QueryClient()}>
      <Screen />
    </HubProviders>,
  );

  fireEvent.click(screen.getByRole('button', { name: 'delete' }));

  expect(await screen.findByText('That was not done')).toBeInTheDocument();
  expect(screen.getByText(/Somebody holds this award/)).toBeInTheDocument();
});
