# Un campo `Localized<T>?` che nessuno ha scritto è `null` sul filo, non `{}`

**Data:** 3 settembre 2026 — fase F5
**Stato:** **confermata** il 3 settembre 2026. `01-design-m0.md` §3.1 precisata (design v1.4).

## Il design

§3.1, parte API: «serializzato come oggetto `{ "it": "...", "en": "..." }`»; e F4 ha implementato
`LocalizedJsonConverter` con `HandleNull = true` e la regola scritta nel commento del file: «un
campo assente torna **vuoto e mai null**».

## Il problema

Quella regola è nata leggendo: dentro un `Localized<T>`, **una lingua** che manca torna stringa
vuota, così nessun chiamante deve distinguere fra «assente» e «vuota». È giusta e resta.

Ma F5 è la prima fase che manda in giro un campo **il cui tipo è nullable**: `Link.Description` è
`Localized<string>?`, perché «questo link non ha una descrizione» è un'informazione, non una
descrizione vuota. Con `HandleNull = true` il converter viene chiamato **anche per il riferimento
nullo**, e la sua prima riga era `ArgumentNullException.ThrowIfNull(value)`: il primo `GET
/api/links/{id}` su un link senza descrizione ha risposto **500**.

Bug reale, trovato dal test `ACoordinatorCreatesReadsUpdatesAndDeletesInTheirOwnDepartment`.

## La decisione

Il converter scrive `null` quando il riferimento è nullo. Le due regole convivono e riguardano cose
diverse:

| Cosa manca | Cosa esce |
|---|---|
| Una **lingua** dentro un `Localized<T>` | stringa vuota (regola di F4, intatta) |
| L'**intero campo**, dichiarato `Localized<T>?` | `null` |

Il motivo per cui non esce `{}`: lo schema OpenAPI generato dichiara già quel campo
`null | LocalizedString`, quindi `{}` mentirebbe al client generato; e soprattutto «nessuno ha mai
scritto una descrizione» e «la descrizione è stata svuotata» diventerebbero indistinguibili, che è
la stessa ragione per cui la colonna è nullable a database.

Un campo **non** nullable continua a non poter essere null: `Title` è `Localized<string>` e, se
nessuno lo ha compilato, esce `{}` — perché lì la lettura in lingua è comunque possibile e vale la
regola di F4.

## Correzione portata nel design

§3.1, parte API: dopo «serializzato come oggetto `{ "it": …, "en": … }`» aggiungere: «una lingua
assente torna vuota e mai null; un campo **dichiarato** `Localized<T>?` e non valorizzato torna
`null`, che è quello che lo schema OpenAPI dichiara».
