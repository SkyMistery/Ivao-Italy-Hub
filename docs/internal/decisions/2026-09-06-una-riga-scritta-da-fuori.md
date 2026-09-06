# Una riga scritta da fuori

**Data:** 6 settembre 2026 — trovata aprendo G7
**Stato:** **decisa il 6 settembre 2026 da Carmine: 1, e la correzione del nome**
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: il caso non è coperto da nessun meccanismo
esistente e la via d'uscita tocca la spina dorsale. Ci si ferma, si scrive, si decide, poi si
codifica.

## Cosa serve

Un messaggio di contatto è **scritto da un membro qualunque dentro lo spazio di un dipartimento**.
Il design lo dice così (§5.1): `OwnerDepartment` **è** il dipartimento destinatario, «così la coda
del back-office e la policy di scrittura escono gratis dall'handler che esiste già». È la scelta
giusta per la lettura. Per la scrittura non funziona, e il motivo è nella spina dorsale.

## Che cosa esiste oggi, verificato

`HubSaveChangesInterceptor.EnsureWriteIsAllowed` è la rete sotto le policy: chiunque sia
autenticato e scriva una riga `IOwnedByDepartment` deve tenere `<Area>.Edit` **su quel
dipartimento**, o la `SaveChanges` lancia `ForbiddenDomainException`. L'area la ricava dal nome del
`DbSet` (o da `[PermissionArea]`), e ci appende sempre `.Edit`.

Un membro che manda un messaggio all'AOD non tiene `Contacts.Edit` sull'AOD — non tiene niente. La
riga verrebbe rifiutata **dall'interceptor**, non dalla policy dell'endpoint: la guardia esiste
apposta perché nessun endpoint possa dimenticarsene.

Le vie d'uscita che **non** sono vie d'uscita, verificate:

- **Togliere `IOwnedByDepartment` a `ContactMessage`**: `MapCrud` passerebbe in modalità globale, e
  chiunque tenga il permesso vedrebbe la coda di **tutti** i dipartimenti. È esattamente ciò che il
  design non vuole.
- **Scrivere la riga da anonimo** (la guardia lascia stare chi non è autenticato, perché quello è
  l'applicazione stessa): si perderebbe il VID del mittente, che è tutto il punto di un form senza
  captcha.
- **Un permesso che il membro tiene davvero**: sarebbe `Contacts.Edit` a chiunque su qualunque
  dipartimento, cioè la guardia spenta con un nome gentile.

## Il bivio

| | 1 — l'entità dichiara che accetta invii (raccomandata) | 2 — un secondo authorization handler / un caso speciale nell'endpoint |
|---|---|---|
| Che cos'è | un'interfaccia accanto a `ISharedForReading`: `ISubmittedByMembers`. La guardia, per una riga **appena creata** di un tipo che la dichiara, non chiede `.Edit` | codice che aggira la guardia per questo caso |
| Quanto costa | tre righe in `EnsureWriteIsAllowed` e una riga sull'entità | un secondo posto che ragiona sui permessi |
| Che cosa allarga | **solo la creazione**, e solo per i tipi che la dichiarano. Cambiare la riga dopo resta una scrittura ordinaria: lo stato lo muove chi tiene `Contacts.Edit` su quel dipartimento | tutto quello che quel codice decide di allargare |
| Ha già altri clienti | sì: M2 (iscrizione a un evento) e M4 (richiesta di esame) sono la stessa forma — un membro crea una riga nello spazio di un dipartimento | no |
| Contro | è un meccanismo nuovo nella spina dorsale, e la spina dorsale è la cosa che non si tocca a cuor leggero | `CLAUDE.md` §2 e §5: è la copia locale che il progetto rifiuta dal primo giorno |

**Raccomandazione: 1.** È la stessa mossa già fatta due volte e con lo stesso stile: `ISharedForReading`
allarga la **lettura** («questa risorsa condivide alcune righe») e `ReadOnlyRows` restringe la
**scrittura** («questa risorsa ha righe che scrive qualcun altro»); questa allarga la **creazione**
(«questa risorsa accetta invii»). Il motore continua a non sapere che cosa sia un contatto, e
l'entità resta l'unico posto che decide.

La guardia resta intera dove conta: **solo `EntityState.Added`**, solo per i tipi che la dichiarano,
e il resto — leggere la coda, muovere lo stato, cancellare — continua a chiedere il permesso sul
dipartimento.

## E una correzione al design, nello stesso punto

Design M1 §10.1 chiama i due permessi **`Contacts.View`** e **`Contacts.Manage`**. Con `.Manage` la
riga non è scrivibile **da nessuno**: la guardia dell'interceptor chiede `<Area>.Edit`, e
`Contacts.Edit` non esisterebbe. Anche `MapCrud` deriva `.Edit` come policy di scrittura, e
`CorePermissions` dichiara la regola per esteso — «every departmental area declares both `View` and
`Edit`».

Quindi i due nomi sono **`Contacts.View`** e **`Contacts.Edit`**, e il design va corretto. Non è un
permesso in più: è lo stesso permesso con il nome che il resto del codice si aspetta.

## La decisione (6 settembre 2026)

**1**, e **sì** alla correzione: i permessi si chiamano `Contacts.View` e `Contacts.Edit`, e il
design M1 §10.1 va corretto nello stesso cambio.
