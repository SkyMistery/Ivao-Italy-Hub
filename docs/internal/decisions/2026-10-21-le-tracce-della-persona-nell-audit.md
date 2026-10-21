# Le tracce della persona nell'audit (#323)

**Data:** 21 ottobre 2026 — revisione del codice del master (nota `2026-10-20-la-revisione-del-codice`, lotto 5), issue #323, PR del
nucleo `fix/erasure-audit-traces`
**Stato:** §2 **scelta tecnica**, che applica una regola già decisa (nota `2026-09-25-la-cancellazione-dei-dati-di-una-persona` §4
punto 2); §3 **decisa** da Carmine, in chat al master il 21 ottobre 2026 («Tutte come suggerisci tu»), pubblicata su sua istruzione
([risposta][a323]): l'opzione (A). Cambia il §2 della nota `2026-09-25-le-righe-che-restano-con-il-vid`.

[a323]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/323#issuecomment-6310356156
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: si corregge `AuditRedaction`, nessun meccanismo nuovo.

## 1. Che cosa non va

Dopo una cancellazione, due cose nell'audit riportano ancora alla persona (leggibili solo con `Audit.View`, ma la nota del 25 settembre,
risposta 1, vuole che nessuna tabella la ricolleghi al VID):

1. **Gli indirizzi dei suoi accessi.** L'accesso scrive le sue righe d'audit (la riga di `hub_users`, i grant che sospende o riattiva)
   nel callback OIDC, prima che esista il cookie: autore `0`, indirizzo del membro. La cancellazione toglieva l'indirizzo solo dove la
   persona era l'autore. Restava «-N, da questo indirizzo, a t1, t2, …», una riga per accesso.
2. **Un ban tenuto in vigore lega lo pseudonimo al VID.** `ErasureRequest.Keep` lascia la riga del ban con il suo VID, ma
   `AuditRedaction.ReplaceAsync` riscrive nelle sue copie d'audit il VID con lo pseudonimo: la riga dice `Vid = 780095`, la sua storia
   `"Vid": -3`. Da lì, filtrare l'audit per `-3` trova tutto ciò che la persona ha fatto.

## 2. Gli indirizzi (scelta tecnica)

Verificato eseguendo: nel callback l'autore è davvero `0` e l'indirizzo è quello del membro (test
`ErasureTests.NoAuditRowAboutThePersonKeepsTheAddressOfTheirSignIns`, due accessi da due indirizzi, contro il codice di prima: quattro
righe con l'indirizzo dopo la cancellazione — due di `hub_users`, due di `hub_user_grants`).

La regola della nota (§4 punto 2: dove la persona ha agito, niente `ip`) vale anche per le righe che ha scritto **prima di essere
qualcuno**: una riga d'audit **sulla persona** con autore `0` (`AuditLogEntry.Nobody`) perde l'indirizzo. «Sulla persona» è ciò che la
cancellazione già riconosce: la riga del suo utente, una riga cancellata con lei (`EmptyAsync`), una riga le cui copie la nominano —
cercata **per chiave**, perché una copia `updated` contiene solo le colonne cambiate e la sospensione di un grant revocato non dice di chi
era. Una riga sulla persona scritta da qualcuno che era entrato tiene il suo indirizzo, che è di quell'autore; un job non ne ha.

## 3. Le copie di una riga tenuta (decisa: A)

La nota del 25 settembre (§2) ha scelto «le sue righe d'audit prendono lo pseudonimo come tutte le altre» senza vedere che la riga viva
e la sua storia, una accanto all'altra, sono la chiave dallo pseudonimo al VID.

- **(A) Le copie restano con il VID, come la riga.** Non dicono nulla che la riga non dica già, e la storia del ban (chi l'ha scritto,
  quando, i cambi di motivo e di scadenza) resta leggibile per lo staff che lo fa rispettare. Quando il ban scade, rilanciare la
  cancellazione anonimizza la riga e le sue copie insieme, con uno pseudonimo nuovo (ogni passata ne prende uno).
- **(B) Le copie si svuotano** (`before_json`/`after_json` a null, come per una riga cancellata). Nessun legame, ma il ban perde la sua
  storia proprio mentre è in vigore, e la riga tiene comunque il VID.

**Raccomandazione (del master): (A).**

**Risposta di Carmine: (A)** ([risposta][a323]): le copie restano sul VID vero, come la riga, e non prendono mai lo pseudonimo.

Nel codice: dopo l'eraser del modulo, `AuditRedaction.Kept` legge dal contesto le righe tenute (`ErasureRequest.IsKept`) come tabella
e chiave, come `TakeErased` per quelle cancellate; `ReplaceAsync` le riceve e sulle loro copie lascia il VID **anche come autore**
(lo pseudonimo nella colonna `vid` accanto al VID del JSON sarebbe lo stesso legame) e toglie solo l'indirizzo, che la riga non
contiene. Test: `ErasureTests` sulla riga tenuta del `SampleEraser` — le sue copie dicono il VID e nessuna lo pseudonimo, e cercando
nell'audit lo pseudonimo (autore o JSON) il ban non esce.

## Da portare nel piano

- `00-piano-di-progettazione.md` §16 punto 16 (la cancellazione): l'audit di una riga sulla persona scritta senza nessuno entrato (un
  accesso) perde l'indirizzo; le copie d'audit di una riga tenuta seguono la risposta del §3. Versione e changelog.
- La nota `2026-09-25-le-righe-che-restano-con-il-vid` §2 resta com'è (le note non si toccano): il piano dice che questa la corregge.
