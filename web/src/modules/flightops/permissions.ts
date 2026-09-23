/**
 * The permissions of the tours the screens ask about, spelled as `TourPermissions` spells them on the
 * server (design M2 §7.1).
 */
export const TOURS_VIEW = 'Tours.View';
export const TOURS_MANAGE_AIRCRAFT = 'Tours.ManageAircraft';
export const TOURS_MANAGE_SETTINGS = 'Tours.ManageSettings';
export const TOURS_EDIT = 'Tours.Edit';
export const TOURS_DELETE = 'Tours.Delete';
export const TOURS_MANAGE_TEMPLATES = 'Tours.ManageTemplates';
export const TOURS_MANAGE_RULES = 'Tours.ManageRules';
export const TOURS_VALIDATE = 'Tours.Validate';

/**
 * The core's permission that reads a department's contacts, and answers them (T14a): the validation page links a dispute's
 * thread into the department's queue for who holds it, and into their own threads for a validator who takes part.
 */
export const CONTACTS_VIEW = 'Contacts.View';
