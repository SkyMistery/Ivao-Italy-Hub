# Le prenotazioni sul server: le letture del design (E6a)

**Data:** 7 ottobre 2026 — fase E6a di M4 (prenotare: il server), PR #233
**Stato:** **decisa** — Carmine ha risposto l'8 ottobre 2026 alle tre domande (§4) e al punto 10 del revisore, [sulla
#233](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/233#issuecomment-6069142670) (SkyMistery, su sua istruzione): le risposte
nel §6. Resta **aperto** un punto che chiude il maintainer alla consegna: il formato del log binario (risposta 2). Il ritiro del pilota, che cancella la riga, poggia sulla
fase del nucleo **E10h** (la #232, nota `2026-10-07-il-ritiro-di-chi-ha-mandato-la-riga`, **decisa** da Carmine sulla #232, §2 qui
sotto): la #233 è in coda dopo la #228 di E5 e dopo la #232.
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
   univoco; `bookingIncompatible`; `slotClosed`; `bookingNotOpen`; `bookingCancelledEvent`. Su `aircraftIcao`, `aircraftNotAllowed`.
   **Un deadlock non è «preso»** (la revisione della #233, punto 3): può incontrare anche un caricamento degli slot o «elimina i liberi», e
   lo slot può essere libero; la risposta è **409** «riprova» (`events:errors.bookingTryAgain`), niente prenotato, come per la rotazione.
   **404** per uno slot che non c'è, per uno slot di un evento che il pilota non vede — bozza, non ancora visibile, concluso: come la sua
   pagina —, anche quando smette di vedersi mentre la prenotazione aspetta il blocco (la rilettura sotto il blocco chiede quello che chiede
   la prima, `EventState.Seen`: la revisione, punto 7), e per uno slot che lo staff elimina nello stesso istante. Una prenotazione fatta è
   **201** senza un indirizzo suo: si legge nella lista del pilota (la revisione, punto 5). Uno slot privato è rifiutato
   (`bookingPrivateSlot`) finché E7 non porta il suo form.
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
   prenotato; la stessa chiave della prenotazione di uno slot solo).
6. **Lo staff toglie** (§3.6): con `EventBookings.Edit` chiesto all'unico handler sulla prenotazione — con i dipartimenti e lo scope
   dell'evento **com'è ora**: «togli» copia sulla prenotazione la cura dell'evento prima di chiedere, come ogni riga dell'evento
   (`IEventChild`, `EventChildren.AdoptAsync`), così un dipartimento entrato nell'evento dopo la prenotazione la raggiunge e uno uscito no
   (la revisione della #233, punto 1) —, un **motivo obbligatorio** in testo semplice, al più mille caratteri (va a un pilota in una mail,
   non su una pagina), **in qualunque momento**: il design non mette un limite. Il motivo **non si conserva**: lo porta la mail; l'audit
   del nucleo tiene chi ha tolto quale prenotazione e quando. **La mail parte dopo che la cancellazione è salvata, apposta** (la revisione,
   punto 6): la riga è la verità e la mail la segue, come per un annullamento; messa in coda prima, potrebbe annunciare una rimozione che il
   salvataggio poi rifiuta — ritirata dal pilota, o tolta da un altro, un attimo prima. Se la coda fallisce dopo il salvataggio, la risposta
   è un errore e la prenotazione non c'è più: l'audit dice chi l'ha tolta.
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
   il verbo, e la storia è dell'audit (`[Audited]`). ⚠️ **Raggiunge, per l'unico handler; il guardiano dell'interceptor no.** Il guardiano
   chiede `{Area}.Edit` sui dipartimenti della riga **senza il suo scope** (`RequireAny`), quindi un grant su un evento solo passa l'handler
   e il salvataggio lo rifiuta: non toglie una prenotazione di quell'evento, e non corregge nemmeno un suo slot. Misurato in E6a
   (`ForbiddenDomainException: VID … does not hold EventBookings.Edit on any of ED`, dopo che l'handler aveva detto sì). È del nucleo,
   e riguarda ogni riga di un evento: fuori da questa fase, detto a `dalberone`.
10. **Il «preso» della pagina e l'esportazione** leggono le prenotazioni chiunque chieda (`CrudSource.BackOffice`): la pagina dice solo se
    uno slot è preso, mai da chi; l'esportazione dà `booked_by` (il VID; negativo, lo pseudonimo, dopo la cancellazione dei dati di una
    persona) e `aircraft_icao` (il tipo scelto dal pilota). Dentro la versione 1, che li aveva già, vuoti
    (`docs/events-bookings-export.md`, aggiornato).
11. **Uno slot prenotato si corregge ancora** (la domanda 3): il design dice solo che non si elimina. Il form dello slot lo lascia correggere
    come prima, e la prenotazione resta com'è: niente si ricontrolla — l'aereo scelto, la compatibilità — e il pilota non riceve una mail.
    **Carmine ha deciso diversamente** (risposta 3, §6): la correzione resta permessa e la prenotazione resta, ma **il pilota riceve una
    mail** quando la correzione cambia il volo che ha prenotato.

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

**Perché la prenotazione non porta una decisione** (la regola di Carmine sul punto 6 di E10h: la nota di ogni fase che mette il segno
dice perché la sua riga non porta una decisione, così che la revisione lo controlli —
[la sua risposta sulla #232](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/232#issuecomment-6041679155)). Finché il pilota può
ritirare — fino all'off block del suo slot, mai dopo (`BookingRules.IsOpen`, design §3.6) — sulla riga non c'è nessuna decisione dello
staff. L'unica che lo staff prende sul pilota in una prenotazione è togliere a mano un «prenotato e non volato» dal suo registro
(`unflown_excused_by`, `unflown_excused_note`: E13b, design §3.7), e cade su una prenotazione che la verifica dopo l'evento (E13a, §5.1)
ha trovato non volata: dopo il volo, quindi dopo l'off block. `flown_*` lo scrive quella verifica, non lo staff. «Togli» è una decisione
dello staff, ma cancella la riga: non resta niente che un ritiro possa cancellare. Nessun permesso dell'area `EventBookings` è negato
all'interessato. ⚠️ **Per E13a ed E13b**: uno slot può partire fino a sei ore dopo la fine dell'evento (`SlotWindow.Margin`) e
`events-after` gira dopo `ends_at_utc`; la verifica e la giustificazione non toccano una prenotazione ancora ritirabile, altrimenti il
segno va riguardato.

## 3. Che cosa si è toccato

Solo il modulo: `EventBooking.cs` (e `EventChild.cs`, il suo commento), la migrazione additiva `AddEventBookings`, `Bookings/`
(`BookingRules`, `PilotBookings`, `BookingEndpoints`, `BookingDtos`), `EventsMail.cs`, `EventsNotifications.cs` (`bookingRemoved`, e
`bookingChanged` dalla risposta 3),
`Data/DatabaseErrors.cs` (gli errori del database che un verbo del modulo risponde da sé: era il `MetAnotherWrite` di E5, che ora lo
usa), `Staff/EventSaving.cs`,
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

## 5. Dopo la revisione

Il master ha letto la #233 ([i rilievi](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/233#issuecomment-6039678626)): approvabile sul
codice dopo quattro correzioni, che sono nella stessa PR e qui sopra nelle letture 3, 5 e 6 — la cura dell'evento com'è ora su «togli»,
i 403 provati per chi non ha il permesso sulla riga, un deadlock che è «riprova» e non «preso», due test che provano `READ COMMITTED` (la
lettura 1: con l'isolamento predefinito cadono tutti e due) —, e quattro punti minori: il 201 senza un indirizzo, la mail dopo la
cancellazione (voluta, lettura 6), la rilettura che chiede `EventState.Seen`, e due 500 che restano (un'attesa di blocco scaduta in una
prenotazione, un deadlock in «elimina i liberi»: scritti in `10`, «Com'è andata»). Nessuna decisione nuova: le domande del §4 restano
quelle; il grant su un evento solo che il guardiano non lascia scrivere (lettura 9, ⚠️) è del nucleo.

## 6. Le risposte di Carmine

L'8 ottobre 2026, date in chat alla sessione master e pubblicate su sua istruzione
([sulla #233](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/233#issuecomment-6069142670)):

1. **Sì alle prime dieci letture, come scritte**; l'undicesima è la risposta 3. La lettura 10 vuol dire che
   `docs/events-bookings-export.md` dice che `booked_by` è negativo dopo la cancellazione dei dati di una persona: il Gate Manager è il
   programma del maintainer e lo leggerà così. Il documento lo diceva già dalla prima testa (la riga di `booked_by`).
2. **Il log binario della MariaDB di produzione non è verificato**: il maintainer non vede le variabili del server. Si prova **con la prima
   prenotazione sull'installazione di prova, alla prossima consegna, prima di aprire le prenotazioni di un evento vero**: con `STATEMENT`
   quella prenotazione fallisce subito, e la lettura 1 si riapre. **Punto aperto: lo chiude il maintainer, alla consegna** (scritto anche
   in `HANDOFF-M4.md`).
3. **Uno slot prenotato che lo staff corregge: la correzione si fa, e il pilota è avvisato quando cambia quello che ha prenotato.** Né «niente
   si ricontrolla e nessuna mail», come raccomandava la lettura 11, né un rifiuto: un nuovo orario di molti slot non deve costringere lo
   staff a togliere le prenotazioni una per una. Fatto su questa PR, con il suo test:
   - lo staff corregge ancora lo slot dal suo form, e la prenotazione resta, con l'aereo scelto;
   - **quando cambiano gli orari, gli aeroporti o i tipi di aereo ammessi**, il pilota riceve **`events.bookingChanged`**, un intento suo nel
     servizio delle notifiche del nucleo, come `bookingRemoved`: il volo com'è ora — callsign, aeroporti, off block e on block in UTC, i
     tipi ammessi — e l'aereo che ha scelto, con l'invito a ritirarla fino all'off block se non gli va più bene (`EventsMail.BookingChangedAsync`,
     `SlotSaving.AfterSaveAsync`, dopo il salvataggio);
   - **anche il callsign** (lettura di questa fase, da controllare in revisione): la risposta nomina orari, aeroporti e tipi, e il suo titolo
     dice «quando cambia quello che ha prenotato»; il callsign è il volo con cui il pilota si collega, quindi una sua correzione avvisa;
   - **lo stand o il numero di volo da soli non avvisano nessuno**, e nemmeno la rotazione o il solo tipo principale: i tipi ammessi si
     confrontano come insieme;
   - **la correzione non si ricontrolla** con le altre prenotazioni del pilota (`bookingGapMinutes`) né con l'aereo che ha scelto: la
     prenotazione resta com'è anche se ora è troppo vicina a un'altra sua, o se il suo aereo non è più fra quelli ammessi. La mail gli dice il
     volo com'è ora, e decide lui: ritirarla fino all'off block, e prenotare di nuovo. La catena della rotazione dello slot si controlla come
     prima, perché è dello slot e non del pilota.
4. **L'eraser degli eventi lasciato a E8b (il punto 10 del revisore): accettato, con una regola.** Finché E8b non è unita **le prenotazioni
   non si aprono su un'installazione vera**; l'installazione di prova non è vincolata. Scritto in `HANDOFF-M4.md`, dove si legge la
   consegna.

## Da portare nel piano

- Design M4 §1.6: la chiave verso lo slot e nessuna verso l'evento; lo scope dell'evento; una prenotazione non è `IAuditable`.
- Design M4 §3.3: le risposte; un deadlock è 409 «riprova», mai «preso»; la rotazione intera con un aereo solo, sempre 200 con il perché
  di ogni tratta, 409 su un deadlock.
- Design M4 §3.5 e §10.1: `READ COMMITTED` e perché; il formato del log binario, da provare alla prossima consegna (risposta 2).
- Design M4 §3.6: il motivo dello staff, obbligatorio e non conservato, senza un limite di tempo; «togli» nella cura dell'evento com'è
  ora; la mail dopo la cancellazione; uno slot prenotato si corregge, la prenotazione resta e il pilota è avvisato quando cambia il volo
  — callsign, orari, aeroporti, tipi ammessi —, senza ricontrollare la compatibilità (risposta 3); perché la prenotazione, che il pilota
  ritira cancellandola, non porta una decisione (§2, la regola di Carmine sulla #232).
- Design M4 §8.3: le mail una volta per persona; `eventChanged` per l'inizio e la fine; **`bookingChanged`**, nuova (risposta 3).
- Piano §2.5: il formato del log binario della MariaDB condivisa, quando la consegna lo prova (risposta 2).
- Piano §13 (M4a) o la consegna: nessuna prenotazione aperta su un'installazione vera finché E8b non è unita (risposta 4).
- Piano §16.6: il conto degli endpoint a mano di M4 (cinque di E6a).
