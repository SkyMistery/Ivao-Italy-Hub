# I fili dei contatti nel nucleo (T14a)

**Data:** 23 settembre 2026 — apertura della fase T14 di M2
**Stato:** deciso da Carmine in apertura (tre risposte, tutte come proposte).
**Regola applicata:** `CLAUDE.md` §5, caso **(b)**: il **che cosa** è già deciso nella nota `2026-09-15-contatti-con-risposte`;
qui si scrive come si divide la fase, le tre scelte che la nota lasciava aperte e le estensioni dei meccanismi esistenti.

## 1. Le tre domande

| Domanda | Risposta di Carmine |
|---|---|
| T14 in una PR o divisa? | **Divisa**, come T11 e T13: **T14a** = il nucleo (punto 1 della fase), **T14b** = il modulo (punti 2–5: contestazione, chiarimento dalle pagine dei tour, segnalazioni su una leg, `openIssues`, giro e2e). Il confine è nucleo/modulo, lo stesso della nota (T4 poi T14). |
| Dove legge e risponde un **partecipante** (il validatore, che spesso ha solo un grant e non `Contacts.View` sul FOD)? | In **`/me/contacts`**, che elenca i fili dove si è **mittente o partecipante**. Il back office del dipartimento resta a chi ha `Contacts.View`; il muro di `/staff` per chi ha solo un grant resta un problema di T15. |
| Quando risponde un partecipante, chi riceve `threadReplied`? | **Il mittente e gli altri partecipanti.** Il partecipante sta dalla parte del dipartimento; la casella del dipartimento non riceve le sue risposte e vede lo stato nella coda. |

## 2. Com'è fatto nel codice

- **Un endpoint del filo, due schermate.** `GET /api/contacts/{id}/thread` e `POST /api/contacts/{id}/replies` servono il back
  office **e** `/me/contacts/{id}`. La regola «il pilota non vede chi risponde» è del **lettore**: se chi legge è il mittente, le
  risposte del dipartimento e dei partecipanti arrivano **senza** VID né nome; a tutti gli altri con. Due endpoint per le due
  schermate avrebbero scritto due volte la stessa conversazione, e il dettaglio del back office (`ContactDetailDto`, con
  `UpdatedBy`) non si allarga: il mittente ci entra come partecipante (§3), quindi il filo non va lì dentro.
- **La lista del membro è il motore CRUD** con un'estensione (piano §16.6, caso b): **`CrudOptions.Participating`**, l'espressione
  «questo VID prende parte a questa riga». Con quella, la lista si stringe alle righe del lettore invece che ai suoi
  dipartimenti, la lettura è aperta a chiunque abbia fatto login e una riga altrui è 404. Nessuna lista scritta a mano.
- **`participants_json`** è una stringa JSON (`[780001]`), letta in SQL con `JsonQuery.ContainsValue` (`JSON_CONTAINS`) e in
  memoria da `ParticipantVids`. Le due metà dicono la stessa cosa e un test d'integrazione le tiene insieme.
- **Da dove è nato il filo**: `source_module`, `source_id` sul messaggio (nulli per un messaggio del form), con l'indice unico
  `source_module, source_id, kind`. È la chiave del «una volta sola» di `ThreadOpeningProjection` (nota §3.3), e l'indice chiude
  la corsa di due salvataggi insieme. La nota la metteva nei riferimenti; un indice unico lì non si può scrivere.
- **I tipi di notifica** si chiamano `contact.threadOpened` e `contact.threadReplied`, non `contacts.…` come la nota: il tipo che
  c'è già è `contact.received`, e il prefisso è uno. `threadOpened` va ai **partecipanti** (non al mittente); il dipartimento
  continua a ricevere `contact.received`, con il tipo del filo. `threadReplied`: risponde il mittente → le stesse persone del
  primo messaggio (casella e staff del dipartimento) e i partecipanti; risponde il dipartimento o un partecipante → il mittente
  e gli altri partecipanti.
- **Chiuso** non blocca una risposta: il mittente che risponde lo riporta a `New`, come dice la nota.

## 3. Le estensioni

1. **`IHasParticipants`** (`DomainContracts.cs`): nell'unico handler concede a quei VID il permesso **che legge** dell'area
   (`Contacts.View`) sulla riga; rispondere chiede `Contacts.View` sulla riga, come per il dipartimento. `ContactMessage` include
   il mittente fra i partecipanti (`CreatedBy`).
2. **La rete dell'interceptor**: una riga `ISubmittedByMembers` e `IHasParticipants` la modifica un partecipante che lo era prima e
   dopo, senza cambiarle dipartimento — gemella dell'eccezione dell'interessato di T11a; e `ContactMessage` porta
   **`[AlsoWrittenWith(Contacts.View)]`**, perché un advisor che risponde sposta lo stato ad `Answered`. La `PUT` dello stato
   chiede ancora `Contacts.Edit`: la rete è sotto le policy, non al loro posto.
3. **`ThreadOpeningProjection`** in `ProjectionSnapshot` e nel writer; `ModuleDbContext` mappa `cms_contact_messages` e
   `cms_contact_references` fuori dalle sue migrazioni, come le altre tabelle delle proiezioni.
4. **`IContactReferenceResolver`**, uno per `source_module`, registrati dai moduli come i provider dei blocchi Data e composti in
   `ContactReferenceResolvers`, che rifiuta all'avvio un modulo registrato due volte. Dice etichetta, link e partecipanti di un
   riferimento, o `null` se non esiste o chi scrive non lo vede.
5. **`MessageThread`** entra nell'elenco chiuso (piano §8.3, già deciso nella nota del 15 settembre) e nella galleria.

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Il filo dentro `ContactDetailDto` | il mittente legge quel dettaglio come partecipante, e con lui i VID dello staff |
| Una lista `/api/me/contacts` scritta a mano | una lista si fa con il motore (`CLAUDE.md` §2); l'estensione è una proprietà |
| `cms_contact_participants` come tabella | la nota ha deciso `participants_json`; `JSON_CONTAINS` basta per una lista corta |
| La chiave del «una volta sola» nei riferimenti | un messaggio ha più riferimenti, e l'indice unico non si scrive su una tabella figlia |
