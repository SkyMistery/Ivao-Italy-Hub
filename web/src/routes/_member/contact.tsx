import { createFileRoute } from '@tanstack/react-router';
import { useTranslation } from 'react-i18next';

import { useSubmitContact } from '../../features/contacts/mutations';
import { ContactForm, PageShell } from '../../shared/ui';

/**
 * Writing to a department. It sits under `_member` and not under `_public` because the form is for
 * members only: the sender is the session, which is what makes a captcha, an address to verify and
 * a spam queue all unnecessary (plan §9.1, design M1 §5.1). Somebody who is not signed in is sent
 * to the login and comes back here.
 */
export const Route = createFileRoute('/_member/contact')({
  component: ContactPage,
});

function ContactPage() {
  const { t } = useTranslation();
  const submit = useSubmitContact();

  return (
    <PageShell title={t('contacts.public.title')} description={t('contacts.public.description')}>
      <div className="max-w-2xl">
        <ContactForm onSubmit={(values) => submit.mutateAsync(values)} />
      </div>
    </PageShell>
  );
}
