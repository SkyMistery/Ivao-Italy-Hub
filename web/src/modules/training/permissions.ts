/**
 * The permissions of the training the screens ask about, spelled as `TrainingPermissions` spells them on the server
 * (design M3 §3.1). Which verbs a reader may use on one training is never asked here: the page draws what the server's
 * handler answered on the row (`actions`).
 */
export const TRAINING_VIEW = 'Training.View';

export const TRAINING_MANAGE_SETTINGS = 'Training.ManageSettings';

export const TRAINING_MANAGE_SHEETS = 'Training.ManageSheets';

/**
 * Banning a trainee and lifting a ban (§2.9, A10a): the list of the bans offers «ban» to whoever holds it somewhere. Whether the
 * reader may ban one member — never themselves — is the server's answer on the path (`canBan`), and on every write.
 */
export const TRAINING_BAN = 'Training.Ban';

/**
 * Putting exams in the calendar (§2.8, A10c): the list of the exams offers «new exam» to whoever holds it somewhere. Which exams the
 * reader may change — their own, or every one for whoever edits the area — is the server's answer on each row (`mayEdit`).
 */
export const TRAINING_MANAGE_EXAMS = 'Training.ManageExams';
