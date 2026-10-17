# Il PIREP di supporto: l'invio, la verifica, la validazione (E14a)

**Data:** 17 ottobre 2026 — fase E14a di M4 (M4b), PR in coda dopo la #283 di E13b
**Stato:** **Proposta** per le quattordici letture del §2, che Carmine decide dopo la revisione della PR; **decise** le tre scelte del §4. La
domanda è l'[issue #287][q], scelta di dalberone. Carmine ha risposto ([la risposta][a287], pubblicata su sua istruzione): **(A) a tutte e
tre**; e poi [un limite sulla scelta 1][l287]: **una volta assegnato un award dal segnale di un PIREP, la sua decisione non cambia più** —
il rifiuto con una chiave sua e la scheda che non lo offre li scrive **E14b**, che proietta i segnali e sa quando uno è diventato un award;
E14a scrive la decisione cambiabile. Il codice segue le risposte.

[q]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/287
[a287]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/287#issuecomment-6234209832
[l287]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/287#issuecomment-6234287053
**Regola applicata:** `CLAUDE.md` §5. Che il PIREP di supporto si mandi da `/events/mine` entro `reportDays` dalla fine, pilota con i voli
e ATC con i turni, uno per tipo per evento, correggibile finché non è deciso; che il sistema lo verifichi a ogni invio con `Valid`,
`Partial`, `Invalid` o `Unavailable`; che lo validi una persona con `EventReports.Edit`, mai il proprio, accettando o rifiutando con una
nota e con la mail `reportDecided`; che i PIREP da validare entrino in `events.staffQueue`, sono **decisi** (design `09-design-m4.md` §1.1,
§1.8, §1.12, §5.2, §5.3, §6.1, §6.2, §7.1, §7.2, §7.3, §8.3, §11; nota `2026-09-29-dopo-l-evento-e-gli-award`, punto 2). Questa nota
registra **le letture** che la fase ha dovuto fare dove il design non dice: casi **(a)** e **(b)**, **due tabelle nuove** (`evt_reports`,
`evt_report_items`, la migrazione `AddSupportReports` dopo `AddAfterTheEventColumns` di E13a), **due verbi** (mandare, decidere) e **una lettura scritta a
mano**. Si usano così come sono l'unico handler con `DeniedToStakeholder` (c'è da E2 per `EventReports.Edit`), il guardiano delle righe dei
membri (`ISubmittedByMembers`, `[WithdrawnByStakeholder]` di E10h), il motore CRUD in sola lettura, `Refusals`, `MemberNames`,
`INetworkSessionSource` di E10t, `IAtcActivitySource` di E10b, `GreatCircle` di E10e con `IAirportDirectory`, le regole dei minuti di E13a
(`AfterRules.MinutesWithin`, `Threshold`), le notifiche del nucleo e `[ErasedWith]` di E10n. **Niente del nucleo cambia.**

## 1. Che cosa serviva decidere

Il design dice le colonne di un PIREP (`event_id`, `vid`, `kind`, `source`, `state`, `verification`, `decided_by`, `decided_at`,
`decision_note`, `submitted_at`) e delle sue voci (un volo con callsign, partenza, arrivo e giorno, o un turno con postazione, da e a, con
quello che la verifica ha trovato: la sessione, i minuti, la distanza, `verified`). Non dice: chi manda per quali eventi, quali regole ha una
voce, come si cerca la sessione di una voce e quando una voce è verificata, come quattro esiti nascono dalle voci, che cosa vuol dire
«correggibile», se una decisione si cambia, chi può ritirare che cosa, dove lo staff decide, che cosa resta di un PIREP quando i dati di una
persona si cancellano.

## 2. Le letture

1. **Le due tabelle.** `evt_reports` (`EventReport`): le colonne del design, più `checked_at` (quando la verifica ha guardato le voci
   l'ultima volta), `created_at`, `updated_at` e la cura dell'evento; **univoco `(event_id, vid, kind)`**: due invii dello stesso membro nello
   stesso istante si incontrano lì, e il secondo diventa una correzione del primo, come un'iscrizione (E16). È `ISubmittedByMembers` e
   `IHasStakeholder` (il membro), area **`EventReports`**, e un **`IEventChild`** che adotta la cura dell'evento prima che lo staff decida,
   con lo scope dell'evento (`IHasResourceScope`): un grant su un evento solo valida i suoi PIREP. `evt_report_items` (`EventReportItem`): il
   PIREP, l'evento, **il VID del membro** e l'ordine; per un volo `callsign`, `departure_icao`, `arrival_icao`, `flight_day`; per un turno
   `callsign` (la postazione), `from_utc`, `to_utc`; e la verifica: `session_id` (solo un volo: la sessione del tracker), `minutes`,
   `distance_nm` (solo un volo, al decimo), `verified` (vuoto: non chiesto). Anche la voce è `ISubmittedByMembers`, `IHasStakeholder` e
   `IEventChild`, nella stessa area, **con il suo VID**: il guardiano legge il membro dalla riga che scrive (E10h), e `[ErasedWith]` copre solo
   un dato nominato dalla stessa riga (il limite di E10n).
2. **`[WithdrawnByStakeholder]` sulla voce, non sul PIREP.** Il PIREP **porta una decisione dello staff** (`state`, `decided_by`,
   `decision_note`): cancellarlo cancellerebbe anche quella, quindi **un membro non lo ritira** — il design non lo dice, e la regola di Carmine
   sulla #232 (punto 6) tiene il segno solo dove una riga non porta decisioni. **La voce non ne porta** (la verifica è del sistema, non dello
   staff): il membro che corregge il suo PIREP ne toglie le voci, e il guardiano glielo lascia fare perché l'entità lo dice; il verbo lo fa
   solo finché il PIREP non è deciso, sotto il blocco della riga (lettura 6).
3. **Chi manda, per quali eventi** (scelta 2 del §4): **qualunque membro entrato**, su un evento **pubblicato, non annullato, finito, non in
   presenza**, fino a `reportDays` giorni dopo la fine (`reportDays` 14, da 1 a 60, nelle impostazioni generate). **Non serve** avere
   prenotato o avuto un turno: il design dice «chiunque abbia volato o controllato» (c2), e l'online day non ha prenotazioni. Uno per tipo
   per evento: il pilota manda un PIREP pilota, il controllore uno ATC, chi ha fatto tutte e due ne manda due. `/events/mine` elenca gli
   eventi ancora aperti a un PIREP con «Manda il PIREP pilota» e «Manda il PIREP ATC», e i PIREP del membro.
4. **Le voci** (da 1 a 20 per PIREP, mai vuoto). **Un volo**: callsign (al più 16 caratteri, in maiuscolo), partenza e arrivo (due codici
   ICAO che l'hub conosce, diversi), il giorno UTC, che deve cadere nella finestra dei voli dell'evento (quella di E13a, `AfterRules.FlightWindow`:
   dall'inizio meno il margine degli slot alla fine più il margine). **Un volo tocca uno scalo dell'evento** — partenza o arrivo —, su un
   evento che ne ha e non è di tutta la divisione (scelta 3). **Un turno**: la postazione (un nominativo, al più 16 caratteri, in maiuscolo:
   **non** solo le postazioni dell'evento, che un evento senza roster non ha), da e a, con `a` dopo `da`, al più dodici ore (il turno più
   lungo delle impostazioni), dentro la stessa finestra; due turni dello stesso PIREP non si sovrappongono. Il server rifiuta campo per
   campo (`items[2].arrivalIcao`), con `Refusals`.
5. **La verifica**, a ogni invio, **prima** di prendere il blocco (la rete può metterci secondi; il blocco no) e scritta sotto il blocco
   insieme alle voci:
   - **un volo**: le sessioni del membro (`INetworkSessionSource`, per VID, `Pilot`) iniziate nel suo giorno dentro la finestra dei voli,
     fra la sua partenza e il suo arrivo; fra quelle che nessun'altra voce dello stesso PIREP ha già preso, prima quella con il callsign
     dichiarato, poi la prima iniziata. Trovata: `session_id`, i **minuti** (la durata dichiarata della sessione) e `verified`. **La distanza**
     è sempre quella fra i due aeroporti (`GreatCircle.DistanceNmRounded` con le coordinate di `IAirportDirectory`), anche senza sessione:
     è un dato della tratta, non del volo, come `fo_legs.distance_nm`;
   - **un turno**: come `events-after` di E13a — l'archivio delle sessioni ATC prima (`SessionsOfAsync`), il tracker per VID (`Atc`, da dodici
     ore prima) dove l'archivio non risponde o non copre il turno —, i **minuti** sulla postazione dentro il turno (`AfterRules.MinutesWithin`),
     **verificato** se arrivano a `attendanceMinimumMinutes` (al più la durata del turno, `AfterRules.Threshold`): la stessa soglia che dice
     «c'era» a un turno del roster;
   - **l'esito del PIREP**: una voce non chiesta (la sorgente non ha risposto) → **`Unavailable`**, mai un'ipotesi; altrimenti tutte
     verificate → `Valid`, nessuna → `Invalid`, alcune → `Partial`. Un PIREP `Unavailable` resta così finché il membro non lo manda di nuovo,
     e chi valida decide su quello che vede: **nessun job lo riverifica** (il design dice «a ogni invio»).
6. **«Correggibile finché non è deciso»**: mandare di nuovo **sostituisce tutte le voci** e rifà la verifica, solo mentre il PIREP è
   `Submitted` ed entro `reportDays`; `submitted_at` dice l'ultimo invio. Il verbo è uno, `PUT /api/events/mine/reports/{eventId}/{kind}`, e
   lavora **sotto il blocco della riga del PIREP** (`SELECT … FOR UPDATE`): una decisione nello stesso istante passa prima o dopo, mai in
   mezzo — dopo, la correzione è rifiutata (`reportDecided`). Il membro legge i suoi con `GET /api/events/mine/reports`.
7. **La validazione** (`POST /api/events/reports/{id}/decision`, con `Accepted` o `Rejected` e una nota): **`EventReports.Edit` chiesto
   all'unico handler sul PIREP**, nella cura dell'evento com'è ora — negato al membro della riga, **il superadmin compreso**
   (`DeniedToStakeholder`, c'è da E2), **nessun `if` del modulo**. ⚠️ Il guardiano dell'interceptor lascerebbe passare il membro che scrive la
   sua riga (la regola di T11): per questo il verbo chiede l'handler **prima** di scrivere, e un test lo prova anche per il superadmin.
   **La nota è obbligatoria per rifiutare**, facoltativa per accettare, al più 1000 caratteri. Sotto il blocco della riga del PIREP; scrive
   `decided_by`, `decided_at`, `decision_note`; poi, dopo il commit, la mail **`reportDecided`** al membro (nella sua lingua, con l'evento,
   il tipo, l'esito e la nota), dalle notifiche del nucleo, spegnibile dal profilo. **Una decisione si cambia** (scelta 1): chi può decidere
   il PIREP passa `Accepted` a `Rejected` e viceversa, con la stessa regola della nota, e il membro riceve di nuovo la mail; l'audit tiene
   ogni passo. Due decisioni insieme: la seconda aspetta il blocco e decide sul PIREP come l'ha lasciato la prima.
8. **L'MD valida e non tocca altro**: i suoi permessi sono `Events.View`, `EventReports.View` ed `EventReports.Edit` con `scope: ED`
   (§6.2, `division.json`); il verbo scrive solo le colonne della decisione del PIREP, e il guardiano chiede `EventReports.Edit` su quella
   riga — un MD che prova a scrivere un evento, una prenotazione o un turno riceve un 403. Un test lo prova.
9. **La scheda dello staff «PIREP di supporto»** in `/staff/events/{id}`, su un evento finito, a chi ha `EventReports.View` sulla sua cura: la
   lista generata della risorsa del motore **in sola lettura** `/api/events/reports` (`filter[eventId]`, `filter[state]`, `filter[kind]`), con
   il membro (`col.person`), il tipo, lo stato, l'esito della verifica, quando è stato mandato, chi ha deciso e la nota; **le voci con la
   verifica di ognuna** in una cella della lista, una riga per voce (il volo o il turno, la sessione trovata o no, i minuti, la distanza) —
   dati, che la pagina scrive nella lingua sullo schermo —; «Decidi» («Cambia la decisione» su uno deciso) dove il server dice che il lettore
   può (`Decidable`: l'unico handler sulla riga, come `Decidable` dei turni di E13b). Le voci arrivano con la pagina, una query per pagina, mai
   una per riga. La decisione manda `Accepted` o `Rejected`; una decisione senza stato arriva come `Submitted` ed è rifiutata
   (`reportStateNotAllowed`).
10. **`events.staffQueue`**: un numero in più per evento, **i PIREP da validare** (`Submitted`), contati solo dove l'unico handler dà al lettore
    `EventReports.Edit` sulla riga — quindi non i propri —, letti già filtrati per stato nella query. Il blocco è nella dashboard dell'MD (§7.3).
11. **Le statistiche** (`EventStatistics.WriteAsync` di E13a): `reports_submitted` conta i PIREP dell'evento, qualunque stato, e
    `reports_accepted` quelli accettati; la scheda «Statistiche» li mostra già. Una decisione non riscrive le statistiche (la stessa regola
    di E13b): «Ricalcola».
12. **La cancellazione dei dati di una persona** (§11): i PIREP e le voci **restano** con lo pseudonimo (il nucleo lo scrive in `vid` e in
    `decided_by`): sono di eventi finiti, e le statistiche e le regole di award contano le voci. Perdono la **nota della decisione**, che
    parla del membro, e delle voci il **callsign** e la **sessione** (che legano la riga alla persona sul tracker): `[ErasedWith(nameof(Vid))]`
    su tutte e tre, così anche un PIREP o una voce che non ci sono più non le lasciano nell'audit (E10n). Restano gli aeroporti, il giorno, le
    ore, i minuti, la distanza, l'esito. L'anteprima dice una riga in più, i PIREP tenuti. **La conservazione** (i PIREP dei piloti dopo
    `pilotRetentionMonths`, §11) è di E15b.
13. **Un evento con un PIREP non si elimina** (`eventHasReports`), come uno con prenotazioni, iscrizioni, disponibilità o turni: il PIREP è
    una riga di un membro con una decisione dello staff sopra, e senza l'evento resterebbe di nessuno. L'evento si tiene (è finito).
14. **Che cosa la fase non fa**: i PIREP `Auto`, le regole di award, i segnali e il riepilogo `reportsToValidate` (E14b; `source` nasce con
    `Manual` solo); un PIREP per un evento in presenza (lettura 3).

### Che cosa tocca del nucleo

Niente di un meccanismo: `ErasureTests` ha le righe delle tre colonne di persona nuove (`evt_reports.vid`, `evt_reports.decided_by`,
`evt_report_items.vid`), e i conteggi dei blocchi non cambiano (nessun blocco nuovo). `schema.d.ts` è generato.

## 3. Alternative scartate

| Alternativa | Perché no |
|---|---|
| Le voci come JSON nel PIREP | il design vuole due tabelle; e una regola di award (E14b) conta voci, non un testo |
| `[WithdrawnByStakeholder]` sul PIREP, e il ritiro | porta la decisione dello staff (lettura 2); il design dice «si corregge», non «si ritira» |
| La voce senza il suo VID | il guardiano leggerebbe un membro che la riga non dice, e `[ErasedWith]` non coprirebbe il callsign (limite di E10n) |
| La verifica sotto il blocco | una chiamata alla rete di secondi terrebbe il blocco della riga mentre lo staff aspetta |
| Un job che riverifica gli `Unavailable` | il design dice «a ogni invio»; il membro rimanda, chi valida vede l'esito |
| Solo chi ha prenotato o ha un turno manda | «chiunque abbia volato o controllato» (c2); l'online day non ha prenotazioni |
| Le postazioni di un turno solo fra quelle dell'evento | un evento senza roster non ne ha |
| Un controllo nel modulo «non il proprio PIREP» | `DeniedToStakeholder` nel catalogo lo dice per tutti, il superadmin compreso, senza un `if` |

## 4. Le domande per Carmine — decise: (A), (A), (A), con il limite sulla prima

1. **Una decisione si cambia?** (A) **Sì** — chi può decidere il PIREP passa accettato a rifiutato e viceversa, con la nota per rifiutare, e
   il membro riceve di nuovo la mail; la stessa regola che Carmine ha dato agli esiti dei turni sulla #283 (lettura 1 di E13b). E14b, che
   proietta i segnali di award all'accettazione, li toglie se il PIREP passa a rifiutato. (B) No: la prima decisione è definitiva.
   **Raccomandata: (A)**. **Decisa: (A)**, [con un limite][l287]: dopo l'assegnazione di un award dal suo segnale, la decisione non
   cambia più (il rifiuto è di E14b).
2. **Per quali eventi si manda?** (A) **Ogni evento pubblicato, non annullato, finito, non in presenza**, entro `reportDays`, qualunque membro.
   (B) Solo gli eventi dove il membro ha una prenotazione (pilota) o un turno (ATC). **Raccomandata: (A)** (c2: «chiunque abbia volato o
   controllato»; l'online day). **Decisa: (A)**.
3. **Un volo che non tocca nessuno scalo dell'evento?** (A) **Rifiutato all'invio**, su un evento con i suoi scali e non di tutta la
   divisione: il membro lo sa subito. (B) Accettato, e lo giudica chi valida. **Raccomandata: (A)**. **Decisa: (A)**.

## Da portare nel piano

§9.2 riga Events (il PIREP di supporto: una voce con il suo VID, la verifica prima del blocco, la decisione che si cambia finché nessun
award è stato assegnato dal suo segnale); il design
`09-design-m4.md` §1.8 (le colonne in più: `checked_at` del PIREP, `event_id`, `vid`, `ordinal` della voce), §5.2 e §5.3 (le letture 3–7),
§7.2 (la scheda), §7.3 (il numero della coda), §11.1 (che cosa resta di un PIREP).
