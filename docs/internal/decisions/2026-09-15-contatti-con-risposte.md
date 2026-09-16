# I contatti con le risposte: contestazioni e chiarimenti

**Data:** 15 settembre 2026 — fase T0 di M2
**Stato:** il **che cosa** è deciso da Carmine nel design dei tour (`05-design-m2.md` §3.8, §3.10, §7.2); la **forma nel codice**
qui sotto è di Claude, da confermare nella revisione della PR di T0.
**Regola applicata:** `CLAUDE.md` §5, caso **(c)**: una funzione nuova di un servizio del nucleo (i contatti, piano §9.1), un'estensione
di `IProjectable` e una del handler.
**Estensione del nucleo** n.2 di `05-design-m2.md` §11. Fase **T14** (il nucleo) dopo **T13** (la validazione).

## 1. Che cosa serve

- **La contestazione**: il pilota contesta un PIREP rifiutato entro `disputeWindowDays`. Diventa un **filo** nei contatti del FOD,
  legato al PIREP, con il validatore che ha deciso fra chi legge e risponde. Rispondono chi ha `Contacts.View` sul FOD (advisor
  compresi) e il validatore; il pilota riceve le risposte per mail e risponde dall'hub. **Aperta la contestazione, la leg non blocca
  più le successive.**
- **Il chiarimento**: su un PIREP deciso, una leg o una regola, anche **più riferimenti in un messaggio**. Non cambia niente del PIREP.
- Gli eventi (M4) e il training (M3) avranno lo stesso bisogno: una conversazione del membro con un dipartimento **su qualcosa**.

## 2. Che cosa c'è oggi, letto nel codice

- `ContactMessage` (`Core/Content/ContactMessage.cs`), tabella **`cms_contact_messages`** (il design scriveva `hub_contact_messages`:
  si corregge): `OwnerDepartment`, `Subject`, `Body`, `Status` (`New`, `Read`, `Answered`, `Closed`), mittente in `CreatedBy`.
  `IOwnedByDepartment`, `IAuditable`, `ISubmittedByMembers`.
- **Nessuna risposta**: il back office cambia solo lo stato, e al mittente non parte niente. Una risposta oggi si dà «con qualunque
  mezzo il dipartimento risponda».
- Il form pubblico `POST /api/contacts` e l'intento `ContactReceived` alla casella del dipartimento.

## 3. La decisione

### 3.1 Il messaggio diventa un filo

- **`cms_contact_messages` guadagna** `kind` (`General` di default, `Dispute`, `Clarification`; un modulo può aggiungerne con una
  chiave i18n) e `participants_json` (VID in più che leggono e rispondono). Migrazione additiva.
- **`cms_contact_replies`**: `message_id`, `author_vid`, `side` (`Sender`, `Department`, `Participant`), `body`, `created_at`.
  Solo in aggiunta: una risposta non si modifica e non si cancella (è parte di un registro che una contestazione o un ban citano).
- **`cms_contact_references`**: `message_id`, `source_module`, `source_id`, `label` (`Localized<string>` fotografata
  all'apertura, così il riferimento resta leggibile quando la conservazione toglie il dettaglio). Un messaggio cita **zero o più**
  oggetti. Il nucleo non sa che cosa sia un `pirep:123`: un modulo registra un risolutore «dammi il link di questo riferimento»
  per il suo `source_module`, come fa già con i blocchi Data.
- **Lo stato si muove da solo**: una risposta del dipartimento o di un partecipante porta a `Answered`, una del mittente riporta a
  `New` (torna in cima alla coda). `Closed` lo mette lo staff come oggi.
- **Le mail** sono intenti del servizio notifiche, come sempre: `contacts.threadReplied` all'altra parte (al mittente se risponde il
  dipartimento, alla casella del dipartimento e ai partecipanti se risponde il mittente), con il testo e il link. **Niente risposte
  per mail in ingresso**: si risponde dal link, dopo il login (niente casella da leggere, niente parser, niente spoofing).
- **Una schermata per il membro**: `/me/contacts` e `/me/contacts/{id}` (lista e filo), costruite con la lista e il form generati;
  il filo è l'unico pezzo a mano, **un componente nuovo nell'elenco chiuso** (`MessageThread`), usato anche nel back office.

### 3.2 Chi legge e chi risponde

- **Il dipartimento**: `Contacts.View` legge e **risponde** (è ciò che il design chiede per gli advisor), `Contacts.Edit` chiude.
  Nessun permesso nuovo.
- **Il mittente** legge e risponde ai **suoi** fili; **un partecipante** a quelli dove compare. Nel handler è una relazione della
  riga, gemella di quella della nota `2026-09-15-permessi-su-una-riga-e-chi-ha-interesse` ma di segno opposto:
  `IHasParticipants { IReadOnlyCollection<int> ParticipantVids { get; } }` concede la lettura e la risposta a quei VID, e il filtro
  globale li lascia passare. Il mittente vi entra con `CreatedBy`. Nessun ramo che nomina i contatti.
- **Il pilota non vede il nome di chi risponde**: vale la regola del design §3.5, e il pilota vede il dipartimento come autore delle
  risposte del dipartimento **e dei partecipanti**; lo staff vede i nomi. È una proprietà di resa del filo (`side`), non un permesso.

### 3.3 La contestazione si apre nella stessa transazione del PIREP

Il PIREP e il filo stanno in due contesti diversi. Scriverli uno dopo l'altro lascia una finestra in cui il PIREP è contestato e il
filo non c'è (o il contrario). **Si apre con una proiezione**, come la segnalazione di un award:

- `ProjectionSnapshot` guadagna `ThreadOpenings`: `ThreadOpeningProjection(kind, department, subject, body, senderVid,
  participantVids, references)`. Semantica **«una volta sola»**, come `AwardSignalProjection`: il writer crea il filo se per quella
  `source_module` + `source_id` + `kind` non esiste, e **non lo riscrive mai** dopo (ha risposte che non sono del modulo).
- Il PIREP la proietta quando passa a `dispute_status = Open`, con il testo della contestazione salvato sulla riga
  (`fo_pireps.dispute_text`) e il validatore che ha deciso fra i partecipanti. **L'esito** della contestazione (accolta, respinta)
  resta del modulo, sulla riga del PIREP: il filo è la conversazione, non la decisione.
- Il **chiarimento** non ha stato del modulo, quindi si apre dal nucleo (`POST /api/contacts` con `kind` e riferimenti), con il
  riferimento verificato dal risolutore del modulo (esiste, e il mittente può vederlo).

⚠️ **Trovato leggendo il codice, e riguarda più di questa nota**: oggi `IProjectable` **non funziona sulle righe di un modulo**.
L'interceptor proietta solo se il contesto contiene le tabelle delle proiezioni (`ProjectionWriter.CanProject`), e `ModuleDbContext`
non le contiene: la riga di un modulo salta la proiezione **in silenzio**. Nessun modulo proietta ancora, quindi nessun test l'ha
visto. La correzione è di **T4**: `ModuleDbContext` mappa le tabelle delle proiezioni **escluse dalle migrazioni del modulo**
(`ExcludeFromMigrations`), così il writer scrive sulla stessa connessione e nella stessa transazione; un test d'integrazione sul
modulo di prova lo prova. Serve alla ricerca, al calendario, all'award, agli usi dei file (nota `2026-09-15-file-con-scadenza`) e qui.

## 4. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Una tabella dei fili nel modulo (`fo_disputes`) | tre moduli la riscriverebbero; il FOD avrebbe due code di messaggi |
| Risposte per mail in ingresso | una casella da leggere, un parser, e un indirizzo falsificabile che scrive nel registro |
| Il filo aperto dall'endpoint del modulo in due salvataggi | la finestra fra i due; un job di riconciliazione, che il piano §16.4 esclude |
| Il riferimento come colonna del messaggio (`source_module`, `source_id`) | il chiarimento chiede più oggetti in un messaggio |
| Un permesso `Contacts.Reply` | il design dà la risposta a chi ha `Contacts.View`; un nome in più per la stessa persona |

## 5. Che cosa si tocca

- **Codice (T4 per le proiezioni nei moduli, T14 per il resto)**: `ContactMessage` + `kind`, `participants_json`; `ContactReply`,
  `ContactReference`; migrazione `AddContactThreads`; `IHasParticipants` nel handler e nel filtro; `ThreadOpeningProjection` in
  `Projections.cs` e nel writer; il registro dei risolutori dei riferimenti; gli intenti `contacts.threadReplied` e
  `contacts.threadOpened`; `/me/contacts`; `MessageThread` nell'elenco chiuso (piano §8.3) e nella galleria.
- **Test**: il filo aperto da una proiezione una volta sola e mai riscritto; il mittente e il partecipante leggono e rispondono, un
  altro membro no, il dipartimento sì; lo stato che si muove con le risposte; il pilota non vede il nome del validatore; il
  riferimento a un oggetto che il mittente non vede rifiutato.
- **Piano** 0.79: §8.3 (un componente), §9.1 (riga Contatti), §16.4. **Design** `05-design-m2.md` §3.8, §3.10, §12 (nomi delle tabelle).
