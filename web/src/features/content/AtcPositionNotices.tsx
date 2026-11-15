import { Button } from '@ivao/atmosphere-react';
import { useTranslation } from 'react-i18next';

import { Notice } from '../../shared/ui';

import type { AtcPositionDrift } from './atcPositions';

/**
 * What the panel of a block that names ATC positions says above its form (design of the editor, phase 3, §5.8): that IVAO's
 * frequencies have not arrived yet, and each row that is no longer what IVAO says now, with «Realign», which copies it again.
 * Never a change by itself: the row keeps what was written until somebody presses the button.
 */
export function AtcPositionNotices({
  drifts,
  pending,
  labels,
  list,
  onRealign,
}: {
  drifts: readonly AtcPositionDrift[];
  /** IVAO's positions are there, their frequencies not yet. */
  pending: boolean;
  /** The block's prefix of labels, for the names of the fields that differ. */
  labels: string;
  /** The block's list of positions, by its property's name. */
  list: string;
  onRealign: (index: number) => void;
}) {
  const { t } = useTranslation();

  if (!pending && drifts.length === 0) {
    return null;
  }

  return (
    <div className="flex flex-col gap-2">
      {pending ? <Notice tone="info" title={t('atcPositions.pending')} /> : null}

      {drifts.map((drift) => (
        <Notice
          key={drift.index}
          tone="warning"
          title={t('atcPositions.drift', { callsign: drift.callsign, row: drift.index + 1 })}
          description={
            <div className="flex flex-col items-start gap-2">
              <ul className="list-disc ps-4">
                {drift.changes.map((change) => (
                  <li key={change.field}>
                    {t('atcPositions.change', {
                      field: t(`${labels}.fields.${list}.${change.field}`),
                      written: change.written === '' ? t('atcPositions.empty') : change.written,
                      ivao: change.ivao === '' ? t('atcPositions.empty') : change.ivao,
                    })}
                  </li>
                ))}
              </ul>
              <Button type="button" size="sm" variant="outline" onClick={() => onRealign(drift.index)}>
                {t('atcPositions.realign')}
              </Button>
            </div>
          }
        />
      ))}
    </div>
  );
}
