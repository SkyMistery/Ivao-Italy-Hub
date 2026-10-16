# I no-show, i registri e i limiti di chi prenota e non vola (E13b)

**Data:** 16 ottobre 2026 — fase E13b di M4 (M4b), PR in coda dopo la #278 di E13a
**Stato:** **Proposta**. Le letture del §2 e le scelte del §4 aspettano Carmine; il codice segue le risposte raccomandate.
**Regola applicata:** `CLAUDE.md` §5. Che lo staff confermi o giustifichi un no-show proposto con `EventAtc.Edit` e mai sul proprio turno,
che tolga a mano un no-show con una nota anche dopo, il registro dei controllori con la penalità `noShowWeight × no-show ÷ (fatti + no-show
+ 2)`, il registro dei piloti con «togli» e una nota, i limiti di chi supera `unflownThreshold` sotto il blocco del pilota con i limiti
dell'evento se li ha, le cinque impostazioni e il blocco `events.staffQueue` sono **decisi** (design `09-design-m4.md` §1.6, §1.7, §1.12,
§3.7, §4.5, §6.1, §7.2, §7.3; note `2026-09-29-il-roster-atc`, punti 7 e 8, e `2026-09-29-gli-slot-e-le-prenotazioni`, punto 8). Questa nota
registra **le letture** che la fase ha dovuto fare dove il design non dice: casi **(a)** e **(b)**, **nessuna colonna nuova** (gli esiti dei
turni ci sono da E11b, `unflown_excused_*` da E6a, `restricted_*` dell'evento dallo scheletro), **tre verbi** e **tre letture scritte a mano**.
Si usano l'unico handler con `DeniedToStakeholder`, il motore CRUD, `Refusals`, `MemberNames` e il blocco del pilota di E6a così come sono.

## 1. Che cosa serviva decidere

Il design dice che lo staff conferma (`NoShow`) o giustifica (`Excused`) un no-show proposto, segna a mano i turni rimasti `Unknown`, toglie
un no-show anche mesi dopo; che il registro di un controllore sono i turni fatti, i no-show e i giustificati per evento, visti da lui e
dallo staff; che il registro di un pilota sono le prenotazioni non volate negli ultimi `pilotRetentionMonths` meno quelle tolte dallo staff;
che oltre la soglia il pilota prenota al più K slot «in ogni fascia di H ore della finestra dell'evento» e M nell'evento, e che il rifiuto
dice perché e fino a quando. Non dice: quali esiti si passano in quali, chi vede quale registro e da quale lettura, come si conta una
fascia, quale orario di una prenotazione cade in una fascia, che cosa vuol dire «fino a quando», dove lo staff decide, che cosa conta il
blocco della coda.

## 2. Le letture

1. **Gli esiti che lo staff scrive** (`POST /api/events/atc/shifts/{id}/attendance`, con l'esito e una nota): su un turno di un evento
   **finito e non annullato** — anche prima che `events-after` l'abbia guardato, e dopo che l'ha chiuso con le righe da verificare (la
   risposta di Carmine sulla #272) —, un turno `Unknown` o `NoShowProposed` diventa `Attended`, `NoShow` o `Excused`; un `NoShow` diventa
   solo `Excused` («togli il no-show», lettura 2); `Attended` ed `Excused` sono **definitivi**. La nota è **obbligatoria per `Excused`**,
   facoltativa altrimenti, al più 500 caratteri (`EventAtcShift.MaxNoteLength`, di E11b), ed è dei dati del controllore (`[ErasedWith]`,
   c'è da E11b). Il verbo scrive `attendance_by` e `attendance_at`; l'audit tiene chi e quando, e l'esito di prima. Nessuna mail: il design
   non ne nomina, e il controllore legge il suo registro. **Il permesso** è `EventAtc.Edit` chiesto all'unico handler **sul turno**, nella
   cura dell'evento com'è ora: negato al controllore della riga, il superadmin compreso (`DeniedToStakeholder`, c'è da E2), e con il FIR
   della postazione per lo staff di un FIR. Due decisioni insieme sullo stesso turno, o una decisione e il giro di `events-after`, si
   incontrano sulla `row_version` del turno: la seconda riceve **409** «leggi di nuovo». **Le statistiche** dell'evento, se le verifiche
   sono finite (`after_done_at`), si riscrivono nello stesso salvataggio con la funzione di E13a (`EventStatistics`), così no-show e
   giustificati tornano senza «Ricalcola».
2. **Togliere un no-show** è la stessa chiamata con `Excused` su un turno `NoShow`, **in qualunque momento** dopo l'evento, con la nota
   obbligatoria; resta tolto (lettura 1: `Excused` è definitivo). Una giustificazione decisa dopo l'evento è il «giustificato» del design
   §4.5; un turno ceduto e approvato prima non è mai un no-show (E12).
3. **La penalità** (§4.5) conta, degli **altri** eventi, i turni `Attended` e `NoShow` del controllore, con `noShowWeight` dalle
   impostazioni (`RosterData.PenaltiesAsync` di E11b, che già leggeva solo i confermati, ora con il peso): un no-show proposto non pesa
   finché lo staff non lo conferma, un giustificato e un `Unknown` non contano né come fatti né come no-show. Il registro la mostra con due
   decimali, la stessa cifra che il perché di un turno proposto dice.
4. **Il registro dei controllori**: `/staff/events/controllers`, con `EventAtc.View`. **Una lettura scritta a mano** (`GET
   /api/events/atc/register`, una pagina di righe, una per controllore con almeno un turno di un evento finito): fatti, no-show, giustificati,
   **da decidere** (proposti e `Unknown` di eventi finiti), la penalità. **Conta tutti i turni del controllore**, di ogni FIR: il registro e la
   penalità sono suoi, e lo staff di un FIR che legge un numero diverso da quello che la proposta usa leggerebbe un altro registro. Sotto, il
   **dettaglio** di un controllore è la lista del motore dei turni (`/api/events/atc/shifts` con `filter[controllerVid]`), che il motore
   restringe già alle righe che il lettore legge — lo staff di un FIR vede i turni dei suoi FIR —, con l'esito, l'evento e i verbi della
   lettura 1 sulle righe che può scrivere (`Editable`, l'unico handler, come il roster di E11b).
5. **Il registro dei piloti**: `/staff/events/pilots`, con `EventBookings.View` — **una pagina che il design §7.2 non nomina**, accanto a
   quella dei controllori: il design dice che lo staff lo vede (§3.7), non dove. **Conta** le prenotazioni verificate (`flown_checked_at`)
   senza volo (`flown_at` vuoto) e non tolte (`unflown_excused_by` vuoto) di eventi **finiti negli ultimi `pilotRetentionMonths`** (dalla
   fine dell'evento: oltre, la conservazione di E15b le porta via). **Una lettura scritta a mano** (`GET /api/events/bookings/register`): una
   riga per pilota con almeno una prenotazione non volata, contata o tolta: contate, tolte, **se ha i limiti** e fino a quando (lettura 8).
   Il dettaglio è la lista del motore delle prenotazioni (`/api/events/bookings` con `filter[bookerVid]`), con «Togli» sulle non volate
   ancora contate.
6. **«Togli» di una prenotazione non volata** (`POST /api/events/bookings/{id}/excuse`, con la nota obbligatoria, al più 1000 caratteri,
   `EventBooking.MaxNoteLength` di E6a): `EventBookings.Edit` chiesto all'unico handler sulla prenotazione, nella cura dell'evento com'è ora
   (come «togli» di E6a); solo su una prenotazione verificata e non volata, una volta (definitivo); scrive `unflown_excused_by` e
   `unflown_excused_note`, l'audit tiene quando. **La prenotazione tiene `[WithdrawnByStakeholder]`** (la regola di Carmine sulla #232, punto
   6): la decisione cade dopo il volo, quando il pilota non può più ritirarla — una prenotazione è verificata solo dopo `events-after`, cioè
   dopo la fine dell'evento più sei ore, dopo l'off block di ogni suo slot. Le statistiche non cambiano: un volo tolto dal registro resta
   un volo non volato dell'evento.
7. **Le fasce dei limiti**: dall'**inizio dell'evento**, ogni `H` ore (`restrictedWindowHours`), fasce fisse — `[inizio + k·H, inizio +
   (k+1)·H)`, anche prima dell'inizio per uno slot nel margine di sei ore di E5 —; una prenotazione cade nella fascia del suo **orario allo
   scalo dell'evento** (l'off block di una partenza, l'on block di un arrivo: l'orario dello slot, quello con cui E5 tiene la finestra e E7
   le ore). Il pilota con i limiti tiene al più `K` prenotazioni per fascia e `M` nell'evento, contate con quelle già salvate **e con quelle
   della stessa richiesta**: le tratte già prese di «prenota tutta la rotazione», e la partenza collegata di un privato (due prenotazioni). Si
   controlla sotto il blocco del pilota (§3.5, `PilotBookings.LockThePilotAsync`), dopo la compatibilità, così due richieste insieme dello
   stesso pilota non passano tutte e due. Le tratte di una rotazione oltre il limite vanno fra quelle non prenotate, con il perché.
   **I limiti dell'evento** (`restricted_window_hours`, `restricted_max_per_window`, `restricted_max_per_event`, nello scheletro da E2)
   sostituiscono uno a uno quelli delle impostazioni, vuoti = le impostazioni; si scrivono nel form generato dell'evento (`Events.Edit`),
   nei limiti delle impostazioni (lettura 9). La soglia (`unflownThreshold`) è solo delle impostazioni: il registro è del pilota, non
   dell'evento.
8. **Il rifiuto dice perché e fino a quando**: due chiavi sul campo `slotId`, `events:errors.bookingRestrictedWindow` e
   `events:errors.bookingRestrictedEvent`. I rifiuti del nucleo portano solo chiavi (`Refusals`), e i numeri e la data li legge la pagina
   da **una lettura del membro**, `GET /api/events/mine/record?eventId=` (la terza scritta a mano, come `/mine/shifts` di E12): il proprio
   registro di pilota — contate, la soglia, se ha i limiti, **fino a quando**, e i tre numeri che valgono in quell'evento — e quello di
   controllore — fatti, no-show, giustificati, la penalità, i turni per evento —. È la stessa lettura che `/events/mine` mostra. **Fino a
   quando** è il momento in cui le prenotazioni contate scendono sotto la soglia: con `n` contate e soglia `s`, la fine dell'evento della
   `(n − s + 1)`-esima più vecchia più `pilotRetentionMonths`. Una prenotazione tolta dallo staff prima di allora lo anticipa.
9. **Le impostazioni**: `noShowWeight` 1 (da 0 a 10, un numero con decimali), `unflownThreshold` 3 (da 1 a 100: zero darebbe i limiti a
   tutti), `restrictedWindowHours` 2 (da 1 a 48), `restrictedMaxPerWindow` 1 (da 1 a 50), `restrictedMaxPerEvent` 2 (da 1 a 200; meno di
   `restrictedMaxPerWindow` è ammesso e vince il più stretto). Gli stessi limiti per i tre dell'evento.
10. **Dove lo staff decide un no-show**: nella pagina dell'ATC di un evento finito (`/staff/events/atc/{id}`, la scheda «ATC» per chi legge
    gli eventi), una sezione **«Esiti»** con la lista generata dei turni dell'evento — la lista di E13a con il nominativo, il controllore,
    l'esito e chi ha coperto — e i verbi della lettura 1 nella riga; e nel dettaglio del registro (lettura 4). La scheda «Statistiche» di
    E13a resta in sola lettura.
11. **Il blocco `events.staffQueue`** (§7.3), sempre dal vivo e del lettore, una riga per evento con qualcosa da decidere, al più 50, dalla
    fine più vecchia: **no-show da confermare** (`NoShowProposed`), **turni da segnare** (`Unknown` di un evento che `events-after` ha
    chiuso, `after_done_at` scritto: prima il job li sta ancora guardando), **cessioni da decidere** (le richieste in attesa, come
    `events.atcCoverage` le conta di E12) — ognuna contata solo se l'unico handler dà al lettore `EventAtc.Edit` sulla riga, quindi mai le
    proprie. Il link porta alla pagina dell'ATC dell'evento. Nessuna proprietà del blocco. Un visitatore riceve `signedIn: false`. I PIREP
    da validare entrano con E14a.

## 3. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Le fasce come finestra scorrevole (nessun intervallo di H ore con più di K) | più stretta del design, che dice «fascia»; e il pilota non saprebbe quali slot può ancora prendere |
| L'orario di un privato dal volo del pilota (`BookedFlight`) | il pilota sposterebbe una prenotazione da una fascia all'altra scrivendo un altro orario all'altro aeroporto |
| I limiti dell'evento anche sulla soglia | il registro è del pilota: un evento non decide chi ha prenotato e non volato altrove |
| Il registro di un controllore contato solo sui FIR del lettore | due numeri per la stessa persona, e la proposta ne usa un terzo |
| Il registro dei piloti dentro la scheda «Prenotazioni» di ogni evento | il registro attraversa gli eventi; la scheda è di uno |
| Un no-show confermato che lo staff può riportare a «presente» | lo staff lo toglie con `Excused` e una nota: l'audit racconta che cosa è successo, il registro non torna indietro di nascosto |
| Una mail al controllore a ogni decisione | il design non ne nomina; si aggiunge con un tipo nuovo se lo staff la vuole |

## 4. Le domande per Carmine

Le undici letture del §2, con le tre in cui c'è una scelta vera:

- **le letture 4, 5 e 8** — (A) **tre letture scritte a mano** (il registro dei controllori, quello dei piloti, il registro del membro),
  con i dettagli nelle liste del motore, e la pagina `/staff/events/pilots` che il design non nomina; (B) solo le liste del motore, senza i
  conti per persona né la penalità sulla pagina. **Raccomandata (A)**: un registro è un conto per persona, che una lista di righe non dà, e
  «fino a quando» deve arrivare alla pagina che rifiuta.
- **la lettura 7** — (A) fasce fisse dall'inizio dell'evento, sull'orario dello slot allo scalo dell'evento; (B) una finestra scorrevole.
  **Raccomandata (A)**.
- **la lettura 6, chi toglie la propria** — `EventBookings.Edit` **non** è negato all'interessato (§6.1): oggi chi dello staff ha prenotato
  e non volato può togliere la propria voce. (A) `EventBookings.Edit` diventa `DeniedToStakeholder` nel catalogo: nessuno decide sulle
  proprie prenotazioni, iscrizioni e posti (togliere la propria prenotazione, iscrizione o posto lo fa già da membro, con il ritiro); (B)
  resta com'è, un limite detto. **Raccomandata (A)**: è la regola «chi ha interesse non decide» (nota
  `2026-09-15-permessi-su-una-riga-e-chi-ha-interesse`), una riga del catalogo e nessun `if` nel modulo.

## Da portare nel piano

§9.2 riga Events (i no-show decisi dallo staff, i registri dei controllori e dei piloti, i limiti per fascia e per evento), §9.7 «Privacy
dei membri» (il registro dei piloti: che cosa conta e chi lo vede). Il design `09-design-m4.md` §3.7 (le letture 5–9), §4.5 (le letture
1–4), §6.1 (la scelta della lettura 6), §7.2 (`/staff/events/pilots`, la sezione «Esiti», le tre letture scritte a mano), §7.3 (il blocco,
lettura 11).
