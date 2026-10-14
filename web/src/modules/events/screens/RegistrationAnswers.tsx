import { Subtle } from '@ivao/atmosphere-react';
import { useTranslation } from 'react-i18next';

import { useLocalized } from '../../../shared/i18n/useLocalized';
import type { MyRegistrationDto } from '../api';

import { answerLines, useAnswerWords } from './answers';

/**
 * A member's answers, one line per question, in words of the language on screen (Carmine's answer 6 on #255): on `/events/mine` and on
 * the event's page once the registration can no longer be changed. Two lines may read the same — two questions worded alike —, so each is
 * keyed by its place.
 */
export function RegistrationAnswers({ registration }: { registration: MyRegistrationDto }) {
  const { t } = useTranslation();
  const read = useLocalized();
  const words = useAnswerWords();
  const lines = answerLines(registration.questions, registration.answers, read, words);

  return lines.length === 0 ? (
    <Subtle className="text-sm">{t('events:mine.noAnswers')}</Subtle>
  ) : (
    <ul className="flex flex-col gap-1 text-sm">
      {lines.map((line, index) => (
        <li key={index}>{line}</li>
      ))}
    </ul>
  );
}
