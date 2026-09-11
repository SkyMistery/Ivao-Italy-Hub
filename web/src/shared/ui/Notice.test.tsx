import { QueryClient } from '@tanstack/react-query';
import { Button } from '@ivao/atmosphere-react';
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import i18next from 'i18next';
import { initReactI18next } from 'react-i18next';
import { beforeAll, expect, test } from 'vitest';

import englishCommon from '../../../../locales/en/common.json';
import { HubProviders } from '../../app/Providers';

import { Notice } from './Notice';
import { useNotice } from './notices';

/**
 * The fifth component of the closed list, in both of its shapes: a panel that stays and a
 * confirmation that appears in the corner and goes.
 *
 * ⚠️ The toast half deliberately mounts **`HubProviders`**, not a provider of its own. Atmosphere's
 * `useToast` throws without a `ToastProvider` above it, so a test carrying its own would go green
 * while the application had none — which is precisely the failure `Chrome.test.tsx` exists for: the
 * fault would not be in a component, it would be in the tree.
 */

const i18n = i18next.createInstance();

beforeAll(async () => {
  await i18n.use(initReactI18next).init({
    lng: 'en',
    fallbackLng: 'en',
    ns: ['common'],
    defaultNS: 'common',
    resources: { en: { common: englishCommon } },
    interpolation: { escapeValue: false },
  });
});

function mount(ui: React.ReactNode) {
  return render(
    <HubProviders i18n={i18n} queryClient={new QueryClient()}>
      {ui}
    </HubProviders>,
  );
}

test('a refusal interrupts and the other three do not', () => {
  mount(
    <>
      <Notice tone="error" title="This page cannot be published" />
      <Notice tone="success" title="Draft saved" />
      <Notice tone="warning" title="Two sections have moved" />
      <Notice tone="info" title="Only the staff sees a draft" />
    </>,
  );

  // A refusal is an `alert`, which a screen reader reads out where it is; the other three are a
  // `status`, read when the reader gets to them. It is the one thing about the four that is not
  // colour, and colour is the half a screenshot review would have caught anyway.
  expect(screen.getByRole('alert')).toHaveTextContent('This page cannot be published');
  expect(screen.getAllByRole('status')).toHaveLength(3);

  for (const said of ['Draft saved', 'Two sections have moved', 'Only the staff sees a draft']) {
    expect(screen.getByText(said)).toBeInTheDocument();
  }
});

test('a confirmation reaches the corner of the screen through the providers the application mounts', async () => {
  const user = userEvent.setup();

  function Screen() {
    const notice = useNotice();

    return (
      <Button type="button" onClick={() => notice({ tone: 'success', title: 'Draft saved' })}>
        Save
      </Button>
    );
  }

  mount(<Screen />);

  // Nothing before the click: a toast that were there from the start would be a panel.
  expect(screen.queryByText('Draft saved')).not.toBeInTheDocument();

  await user.click(screen.getByRole('button', { name: 'Save' }));

  expect(await screen.findByText('Draft saved')).toBeInTheDocument();
});
