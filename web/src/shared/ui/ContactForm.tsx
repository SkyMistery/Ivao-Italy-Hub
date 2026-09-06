import { Subtle } from '@ivao/atmosphere-react';
import { useState } from 'react';
import { useTranslation } from 'react-i18next';

import { DEPARTMENTS } from '../api/department';
import type { Department } from '../api/bootstrap';
import { SchemaForm, type ChoiceOption } from '../forms';

import { contactSchema, type ContactFormValues } from './contact';

/**
 * Writing to a department. It is a custom component of the closed list (design M1 §12, `docs/UI-GUIDELINES.md`
 * §3) because it is mounted from more than one place: the contact page, and — whenever the site
 * grows one — a section of a department's own page.
 *
 * There is no field in this file. The form is generated from the schema next to it, like every
 * other form of the hub; what this component adds is the two things a generated form has no opinion
 * about: which departments can be written to, with their names in the language on screen, and what
 * a sent message looks like.
 *
 * ⚠️ No sender field, on purpose. The endpoint is behind `SignedIn` and takes the VID from the
 * session, so there is no address to verify, nothing to forge, and no captcha to draw (design M1
 * §5.1).
 */
export function ContactForm({
  onSubmit,
  departments = DEPARTMENTS,
  defaultDepartment,
}: {
  /** Rejecting with an `ApiError` is how the server's refusal reaches the fields. */
  onSubmit: (values: ContactFormValues) => Promise<unknown>;
  /** Who can be written to. All of them, unless a screen has a reason to narrow it. */
  departments?: readonly Department[];
  /** Preselected, when the screen already knows who the message is for. */
  defaultDepartment?: Department;
}) {
  const { t } = useTranslation();
  const [sent, setSent] = useState(false);

  const choices: ChoiceOption[] = departments.map((department) => ({
    value: department,
    label: t(`departments.${department}`),
  }));

  const submit = async (values: ContactFormValues) => {
    await onSubmit(values);
    setSent(true);
  };

  if (sent) {
    // Said and then got out of the way. A form that stays on screen with the words still in it
    // reads as a form that did not go through, and the second press is a second message.
    return (
      <div className="border-border bg-card rounded-md border p-6">
        <p className="font-medium">{t('contacts.sent.title')}</p>
        <Subtle>{t('contacts.sent.description')}</Subtle>
      </div>
    );
  }

  return (
    <SchemaForm
      schema={contactSchema(choices)}
      defaults={{
        department: defaultDepartment ?? departments[0] ?? '',
        subject: '',
        body: '',
      }}
      // A message is written in one language — the sender's — so the form has no language tabs and
      // needs no list of them.
      locales={[]}
      labels="contacts"
      onSubmit={submit}
      submitLabel={t('contacts.send')}
    />
  );
}
