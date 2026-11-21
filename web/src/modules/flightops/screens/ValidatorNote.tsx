import { useTranslation } from 'react-i18next';

import { useLocalized } from '../../../shared/i18n/useLocalized';
import { Notice, type NoticeTone } from '../../../shared/ui';
import type { PirepDto } from '../api';
import { type DecidedStatus, isDecided } from './reporting';

/**
 * The tone of a decision's note, one for every decided status (`DECIDED_STATUSES`, so a new one does not compile without
 * its tone): what to correct is worth attention, a rejection went wrong, an acceptance is to know. A status that is not
 * decided has no note to show.
 */
const TONES: Record<DecidedStatus, NoticeTone> = {
  ToModify: 'warning',
  Rejected: 'error',
  Accepted: 'info',
};

/**
 * What the validator told the pilot about a decided report (issue #387): the note to the pilot and the rules the decision
 * says were broken — the same the mail says, which the decision form promises the pilot also reads «on their report».
 * Nothing when the report is not decided (a corrected report back in the queue keeps the old note on the row, and it is no
 * longer the word on it), nor when the decision said nothing. The server sends it only on the pilot's own reports.
 */
export function ValidatorNote({
  report,
}: {
  report: Pick<PirepDto, 'status' | 'noteToPilot' | 'violatedRules'>;
}) {
  const { t } = useTranslation();
  const read = useLocalized();
  const tone = isDecided(report.status) ? TONES[report.status] : undefined;
  const note = report.noteToPilot?.trim() ?? '';

  if (tone === undefined || (note === '' && report.violatedRules.length === 0)) {
    return null;
  }

  return (
    <Notice
      tone={tone}
      title={t('flightops:public.validatorNote')}
      description={
        <div className="flex flex-col gap-2">
          {note === '' ? null : <p className="whitespace-pre-line">{note}</p>}
          {report.violatedRules.length === 0 ? null : (
            <div className="flex flex-col gap-1">
              <span className="font-semibold">{t('flightops:public.violatedRules')}</span>
              <ul className="list-inside list-disc">
                {report.violatedRules.map((rule) => (
                  <li key={rule.code}>
                    <span className="font-mono">{rule.code}</span> · {read(rule.title)}
                  </li>
                ))}
              </ul>
            </div>
          )}
        </div>
      }
    />
  );
}
