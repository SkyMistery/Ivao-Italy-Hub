import type { TFunction } from 'i18next';

import type { ChoiceOption } from '../../../shared/forms';
import type { TrainingRatingDto } from '../api';
import { ratingChoice } from '../schemas';

/**
 * The ratings the division trains, as the choices of a field or of a filter: the ladder and the number in the value, and on
 * screen the ladder, the short name and the name — all three from the server, which asks the core's vocabulary. Written once
 * for every screen of the module that chooses a rating: the settings (A4) and the sheet (A5).
 */
export function ratingOptions(ratings: readonly TrainingRatingDto[], t: TFunction): ChoiceOption[] {
  return ratings.map((rating) => ({
    value: ratingChoice(rating.kind, rating.number),
    label: t('training:ratingChoice', {
      kind: t(`training:kinds.${rating.kind}`),
      shortName: rating.shortName,
      name: t(rating.nameKey),
    }),
  }));
}
