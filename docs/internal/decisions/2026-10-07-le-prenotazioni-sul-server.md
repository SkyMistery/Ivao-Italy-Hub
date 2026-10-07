# Le prenotazioni sul server: le letture del design (E6a)

**Data:** 7 ottobre 2026 — fase E6a di M4 (prenotare: il server), PR #233
**Stato:** **Proposta** — la domanda a Carmine è un commento sulla #233 (§4). Il ritiro del pilota, che cancella la riga, poggia sulla
fase del nucleo **E10h** (la #232, nota `2026-10-07-il-ritiro-di-chi-ha-mandato-la-riga`, §2 qui sotto): la #233 è in coda dopo la
#228 di E5 e dopo la #232.
**Regola applicata:** `CLAUDE.md` §5, casi **(a)** e **(b)**: nessun meccanismo nuovo nel modulo — `ISubmittedByMembers` e
`IHasStakeholder`, il guardiano dell'interceptor, l'unico handler, `Refusals`, `CrudSource.BackOffice`, il servizio delle notifiche, le
impostazioni del modulo —; sono letture del design M4 §1.6, §3.3, §3.5, §3.6, §7.4, §8.3, §10.1 e della nota
`2026-09-29-gli-slot-e-le-prenotazioni` §2.5–§2.7. Il pezzo che mancava al nucleo — un membro che cancella la riga che ha mandato — è un
caso (b) a sé, in una PR a sé prima del codice che lo usa (`CLAUDE.md` §0 regola 6): la fase E10h.

## 1. Le letture

1. **Il blocco del pilota, e che cosa legge chi ha aspettato.** Come dice il design (§3.5, §10.1): `SELECT … FOR UPDATE` sulla riga della
   prima prenotazione del pilota nell'evento (la più vecchia, per `id`), o su quella dell'evento se è la prima, dentro la transazione della
   prenotazione; poi le prenotazioni del pilota si rileggono, e la compatibilità si controlla con quelle già salvate. **La transazione legge
   ciò che è confermato (`READ COMMITTED`).** Con l'isolamento predefinito di MariaDB (`REPEATABLE READ`) un `FOR UPDATE` che non trova
   righe blocca il «buco» dell'indice dove starebbero: le prime prenotazioni di due piloti diversi nello stesso istante bloccherebbero lo
   stesso buco e si incastrerebbero sulla riga dell'evento (un deadlock alla sera dell'apertura, per ogni coppia). Con `READ COMMITTED` il
   buco non si blocca, e la richiesta che ha aspettato il blocco legge la prenotazione che l'altra ha appena confermato. ⚠️ **Una
   conseguenza per il server**: con il log binario in formato `STATEMENT` MariaDB rifiuta le scritture di una transazione
   `READ COMMITTED`. Il predefinito di MariaDB 11.4 è `MIXED`, che va bene, come va bene il log spento: la domanda 2 a Carmine.
2. **Uno slot, una prenotazione, nel database.** L'indice univoco su `slot_id` (§1.6) e, in più, **una chiave verso lo slot**, con
   `RESTRICT`: «uno slot prenotato non si elimina» (§1.5) lo dice il server — un rifiuto, `events:errors.slotBooked` — e lo tiene anche il
   database, come i PIREP tengono il loro tour (M2). **Nessuna chiave verso l'evento**: il controllo di una chiave prende un blocco
   condiviso sulla riga madre, e la riga dell'evento è quella che la prima prenotazione di un pilota blocca in esclusiva; ogni prenotazione
   aspetterebbe le prime, e due piloti sullo stesso slot potrebbero incastrarsi. Un evento eliminato porta via i suoi slot e uno slot
   prenotato lo ferma: un evento con prenotazioni non si elimina nemmeno direttamente nel database. Un caricamento che sostituisce gli
   slot liberi, o «elimina i liberi», mentre un pilota ne prenota uno nello stesso istante, risponde **409** («leggi di nuovo»).
3. **Le risposte.** Un rifiuto della prenotazione cade su `slotId`: `slotAlreadyYours` (è già del pilota); `slotJustTaken` — «slot appena
   preso da un altro pilota», la frase del design (§3.3) — sia quando la prenotazione dell'altro si vede già, sia quando lo dice l'indice
   univoco, o il deadlock di due insert della stessa chiave, in cui l'altro vince comunque; `bookingIncompatible`; `slotClosed`;
   `bookingNotOpen`; `bookingCancelledEvent`. Su `aircraftIcao`, `aircraftNotAllowed`. **404** per uno slot che non c'è, per uno slot di un
   evento che il pilota non vede — bozza, non ancora visibile, concluso: come la sua pagina — e per uno slot che lo staff elimina nello
   stesso istante. Uno slot privato è rifiutato (`bookingPrivateSlot`) finché E7 non porta il suo form.
4. **Fino a quando** (§3.3, §3.6). Si prenota da `booking_opens_at_utc` finché l'off block dello slot è futuro — l'EOBT, anche per un
   arrivo, che parte da un altro aeroporto —, anche a evento in corso; l'istante dell'off block è già chiuso. Un evento annullato non prende
   prenotazioni — e l'evento si rilegge sotto il blocco del pilota, così un annullamento salvato mentre la prenotazione aspettava si vede
   e nessuna prenotazione entra in un evento i cui piloti sono appena stati avvisati —; quelle che ha restano (dicono chi c'era) e si
   ritirano ancora fino all'off block.
5. **La rotazione intera** (§3.3; §17.1 n.16): si chiede da una qualunque delle sue tratte, con **un aereo solo** — una rotazione è la
   giornata di un aereo —; prende, in una transazione e nell'ordine delle tratte, quelle aperte, libere, che ammettono quell'aereo e
   compatibili con le prenotazioni del pilota, anche con quelle prese un attimo prima nella stessa richiesta. La risposta è **sempre 200**,
   con le tratte prenotate e, per ognuna delle altre, il perché (`slotJustTaken`, `slotAlreadyYours`, `slotClosed`, `bookingIncompatible`,
   `aircraftNotAllowed`); la prima lista è vuota se non c'era niente da prendere. Una tratta presa da un altro pilota nello stesso istante
   fallisce da sola e le altre vanno avanti; un deadlock riporta indietro tutta la transazione, e la risposta è **409** «riprova» (niente
   prenotato).
6. **Lo staff toglie** (§3.6): con `EventBookings.Edit` chiesto all'unico handler sulla prenotazione — con i dipartimenti e lo scope
   dell'evento —, un **motivo obbligatorio** in testo semplice, al più mille caratteri (va a un pilota in una mail, non su una pagina), **in
   qualunque momento**: il design non mette un limite. Il motivo **non si conserva**: lo porta la mail; l'audit del nucleo tiene chi ha
   tolto quale prenotazione e quando.
7. **Le mail** (§8.3): `eventCancelled` all'annullamento ed `eventChanged` quando cambia **l'inizio o la fine** di un evento pubblicato e
   non annullato — non per le altre date, né per un salvataggio che non le sposta —, a chi ha prenotato, **una volta per persona** anche
   con più prenotazioni, ognuno nella sua lingua (un intento per lingua); `bookingRemoved` al pilota, con il volo e il motivo. Mai allo
   pseudonimo di una persona cancellata. Il segnaposto del titolo dei modelli è `{{title}}`, non `{{event}}` come lo scrivevano E3a ed E3b
   senza che nessuno lo mandasse: `event` è una parola del calendario, e `EventsArchitectureTests` rifiuta la stringa nel codice del modulo.
8. **Le righe del membro** (§1.1, §7.1): `GET /api/events/mine/bookings`, le prenotazioni del pilota, anche passate, nell'ordine del loro
   off block; il filtro globale fa leggere le prenotazioni a un membro (`Members`) e l'endpoint tiene solo le sue. **Nessuna lista dello
   staff in E6a**: la scheda «Prenotazioni» — lista generata, `MapCrud` in sola lettura, con i nomi — è di E6b, con la sua pagina.
9. **La riga** (§1.6): le colonne del piano, più il dipartimento e la maschera dell'evento (§1.1: «hanno la maschera dell'evento») e la
   visibilità scritta dal getter (`Members`, come i PIREP); **lo scope dell'evento** (`IHasResourceScope`), come ogni riga dell'evento,
   così un grant su un evento solo raggiunge le sue prenotazioni come raggiunge i suoi slot; **non `IAuditable`**: `created_at` lo scrive
   il verbo, e la storia è dell'audit (`[Audited]`).
10. **Il «preso» della pagina e l'esportazione** leggono le prenotazioni chiunque chieda (`CrudSource.BackOffice`): la pagina dice solo se
    uno slot è preso, mai da chi; l'esportazione dà `booked_by` (il VID; negativo, lo pseudonimo, dopo la cancellazione dei dati di una
    persona) e `aircraft_icao` (il tipo scelto dal pilota). Dentro la versione 1, che li aveva già, vuoti
    (`docs/events-bookings-export.md`, aggiornato).
11. **Uno slot prenotato si corregge ancora** (la domanda 3): il design dice solo che non si elimina. Il form dello slot lo lascia correggere
    come prima, e la prenotazione resta com'è: niente si ricontrolla — l'aereo scelto, la compatibilità — e il pilota non riceve una mail.

**Gli endpoint scritti a mano di E6a** (per il conto del piano §16.6): cinque — quattro del **flusso di un membro**, sotto
`/api/events/mine/bookings` (leggere le sue, prenotare, prenotare la rotazione, ritirare), e un **verbo d'azione** dello staff su una
prenotazione (`POST /api/events/bookings/{id}/remove`, «togli»): i verbi che il design nomina (§7.2: «prenotare, ritirare»; la scheda
«Prenotazioni» con «togli»). Nessuna lettura nuova dello staff.

## 2. Il ritiro, e la fase del nucleo

Il design dice «ritirare cancella la riga» (§1.6, §3.6), e la prenotazione è `ISubmittedByMembers` e `IHasStakeholder`. Ma il guardiano
dell'interceptor lascia al membro **creare** una riga così e **cambiarla** finché resta sua (M2, nota `2026-09-23-il-pirep` §5), non
**cancellarla**: «cancellarla resta del dipartimento». Un pilota, che non ha `EventBookings.Edit`, riceverebbe un 403. Scrivere la
cancellazione «come il sistema» è l'aggiramento che quella nota scarta; una prenotazione che non fosse `IOwnedByDepartment` toglierebbe la
rete. Caso (b): si estende il meccanismo, nel nucleo, in una PR a sé — la fase **E10h** (la #232, nota
`2026-10-07-il-ritiro-di-chi-ha-mandato-la-riga`), scelta da `dalberone` il 7 ottobre 2026, alla domanda di questa sessione: una fase
del nucleo prima (raccomandata), la domanda a Carmine prima, o E6a senza il ritiro. E6a unisce il suo branch e mette il suo attributo sulla
prenotazione; E11a (la disponibilità di un controllore) ed E16 (l'iscrizione a un evento in presenza) lo troveranno.

## 3. Che cosa si è toccato

Solo il modulo: `EventBooking.cs`, la migrazione additiva `AddEventBookings`, `Bookings/` (`BookingRules`, `PilotBookings`,
`BookingEndpoints`, `BookingDtos`), `EventsMail.cs`, `EventsNotifications.cs` (`bookingRemoved`), `Data/DatabaseErrors.cs` (gli errori del
database che un verbo del modulo risponde da sé: era il `MetAnotherWrite` di E5, che ora lo usa), `Staff/EventSaving.cs`,
`Staff/EventEndpoints.cs`, `Staff/EventSlotEndpoints.cs`, `Staff/SlotLoading.cs`, `Public/PublicEvents.cs`, `Export/BookingsExport.cs`,
`EventsModule.cs`, i file di lingua del modulo, `docs/events-bookings-export.md`. Del nucleo solo le due righe di `ErasureTests`, con la loro
nota (`2026-10-07-le-colonne-delle-prenotazioni-in-erasuretests`); il pezzo del nucleo del §2 è della PR di E10h, non di questa.

## 4. Le domande a Carmine

> 1. Undici comportamenti delle prenotazioni che il design non dice, scritti in E6a come raccomanda questa nota (§1): (1) la transazione
>    di una prenotazione legge ciò che è confermato (`READ COMMITTED`), con il blocco del design; (2) una chiave verso lo slot, nessuna
>    verso l'evento; (3) i rifiuti su `slotId` e `aircraftIcao`, «slot appena preso da un altro pilota» anche per l'indice univoco, 404
>    per un evento che il pilota non vede; (4) si prenota e si ritira fino all'off block, anche di un arrivo; un evento annullato non
>    prende prenotazioni e tiene quelle che ha; (5) la rotazione intera con un aereo solo, sempre 200 con il perché di ogni tratta non
>    presa, 409 se il database riporta indietro tutto; (6) lo staff toglie con un motivo obbligatorio, che va nella mail e non si conserva,
>    in qualunque momento; (7) le mail una volta per persona, `eventChanged` solo per l'inizio e la fine; (8) il membro legge le sue da
>    `/api/events/mine/bookings`, la lista dello staff è di E6b; (9) la prenotazione ha lo scope dell'evento e non è `IAuditable`; (10) la
>    pagina e l'esportazione leggono le prenotazioni chiunque chieda, `booked_by` negativo dopo una cancellazione; (11) uno slot
>    prenotato si corregge ancora. Confermi?
> 2. Il log binario della MariaDB di produzione è spento, o in formato `MIXED` o `ROW`? Con `STATEMENT` ogni prenotazione sarebbe
>    rifiutata dal database.
> 3. Uno slot prenotato lo staff lo corregge ancora, e la prenotazione resta com'è (raccomandato, il punto 11), oppure lo si rifiuta
>    finché la prenotazione non è tolta?

## Da portare nel piano

- Design M4 §1.6: la chiave verso lo slot e nessuna verso l'evento; lo scope dell'evento; una prenotazione non è `IAuditable`.
- Design M4 §3.3: le risposte; la rotazione intera con un aereo solo, sempre 200 con il perché di ogni tratta, 409 su un deadlock.
- Design M4 §3.5 e §10.1: `READ COMMITTED` e perché; il formato del log binario (la domanda 2).
- Design M4 §3.6: il motivo dello staff, obbligatorio e non conservato, senza un limite di tempo; uno slot prenotato si corregge (la
  domanda 3).
- Design M4 §8.3: le mail una volta per persona; `eventChanged` per l'inizio e la fine.
- Piano §2.5, se Carmine lo conferma: il formato del log binario della MariaDB condivisa.
- Piano §16.6: il conto degli endpoint a mano di M4 (cinque di E6a).
