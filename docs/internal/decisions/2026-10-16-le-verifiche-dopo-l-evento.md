# Le verifiche dopo l'evento e le statistiche (E13a)

**Data:** 16 ottobre 2026 — fase E13a di M4 (M4b), PR in coda dopo la #271 di E17
**Stato:** **decisa**. La domanda è l'[issue #272][q], scelta di dalberone. Carmine ha risposto in due volte, pubblicate su sua
istruzione: **(A) alla scelta 5 e (A) alla scelta 7** ([la prima risposta][a272]), e dopo la lettura della PR #278 ([i rilievi][r278])
**sì alle dieci letture** ([la seconda risposta][a272b]) con due condizioni, che sono i due punti da correggere del revisore: **la lettura
6** — il conto di chi ha volato senza prenotare diviso per scalo, così ogni giro finisce dentro la richiesta — e **la lettura 3** — «non
contato» distinto da 0. Le letture 3 e 6 qui sotto sono scritte come le vuole la risposta; i punti minori della revisione (3–7) sono nelle
letture 2, 4, 7, 9 e 10.

[q]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/272
[a272]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/272#issuecomment-6219761876
[a272b]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/272#issuecomment-6222349300
[r278]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/278#issuecomment-6222330742
**Regola applicata:** `CLAUDE.md` §5. Il job `events-after` ogni ora a lotti, `flown_at` dalle sessioni del pilota fra partenza e arrivo
senza il callsign, chi ha volato senza prenotare solo come numero, la presenza nei turni con `attendanceMinimumMinutes` e il no-show
proposto con chi ha coperto la postazione, le statistiche ricalcolabili dallo staff e la scheda «Statistiche» sono **decisi** (design
`09-design-m4.md` §1.10, §4.5, §5.1, §7.2, §8.4, §10.2; note `2026-09-29-dopo-l-evento-e-gli-award` e `2026-09-29-il-roster-atc`, punto 7).
Questa nota registra **le letture** che la fase ha dovuto fare dove il design non dice: casi **(a)** e **(b)**, con **una colonna nuova**
(lettura 5) e **un verbo** («Ricalcola», lettura 9). Si usano `IAtcActivitySource` per VID (E10b), il motore CRUD, `MemberNames` e i
job che recuperano (E10j) così come sono. **Il tracker** (la ricerca con e senza VID di E10a) il modulo lo legge attraverso
**`INetworkSessionSource`** della fase del nucleo **E10t**, in una PR a sé prima di questa: la nota di E10a dava per scontato che E13a
chiamasse `IIvaoApiClient` con `IvaoSessionQuery`, ma il modulo non nomina la rete (`EventsArchitectureTests`, E2; `CLAUDE.md` §3), e
dalberone ha scelto il 16 ottobre 2026 un'interfaccia del nucleo con un nome neutro, come `IAtcBookingSource` di E15a.

## 1. Che cosa serviva decidere

Il design dice che dopo `ends_at_utc` il job guarda ogni prenotazione con `SearchSessionsAsync(vid, finestra, partenza, arrivo)`, conta chi
ha volato senza prenotare con la ricerca senza VID «nella finestra», confronta ogni turno con le sessioni, scrive le statistiche, lavora a
lotti che finiscono dentro la richiesta e decide da `after_done_at` e `flown_checked_at`. Non dice: da quando un evento è da verificare,
quale finestra, quale sessione va a quale prenotazione se un pilota ne ha due sulla stessa rotta, che cosa si conta come «chi» ha volato
senza prenotare, che cosa si fa quando l'archivio dei turni non copre il turno, dove sta «chi ha coperto», quanto è grande un lotto, fino a
quando si richiede a una sorgente che non risponde, come si contano i no-show proposti nelle statistiche, chi ricalcola e come si leggono.

## 2. Le letture

1. **Quando un evento è da verificare**: pubblicato, non annullato, con `after_done_at` vuoto, quando sono passate **sei ore dalla fine**
   — il margine in cui cade uno slot (`SlotWindow.Margin`, Carmine sulla #228, punto 10): l'ultima partenza ammessa è partita. Una bozza e
   un annullato non si verificano mai e non hanno statistiche dal job.
2. **La finestra dei voli** è quella degli slot: una sessione **iniziata** fra inizio − 6 ore e fine + 6 ore (la regola del tracker è
   sull'inizio, E10a). Per ogni prenotazione: il tracker **per VID**, tipo `PILOT`, la partenza e l'arrivo del volo prenotato (di uno slot
   pubblico i suoi; di uno privato lo scalo dell'evento e l'altro aeroporto del pilota, nel verso dello slot). Fra le sessioni trovate va
   alla prenotazione **quella che parte più vicina al suo orario di partenza**, se nessun'altra prenotazione dello stesso evento l'ha già
   presa: due voli prenotati sulla stessa rotta vogliono due sessioni. `flown_at` è l'inizio della sessione, `flown_session_id` il suo
   numero (già segnato `[ErasedWith]` da E8b); `flown_checked_at` si scrive anche quando non si trova niente. Un volo prenotato senza un
   orario di partenza — un privato d'arrivo il cui pilota non ha più l'orario all'altro aeroporto — si misura dall'orario d'arrivo allo
   scalo dell'evento (revisione della #278, punto 6).
3. **Chi ha volato senza prenotare**: per ogni scalo dell'evento due domande senza VID (partenza, arrivo; E10a: «partenza o arrivo» sono
   due domande), tipo `PILOT`, nella stessa finestra, fino al tetto del nucleo (1000). Si contano **le persone**, non le sessioni: i VID
   distinti, **meno chi ha prenotato** uno slot dell'evento. I VID e le sessioni vivono solo in memoria durante il conto: niente nel
   database, niente nel log. Una risposta lunga quanto il tetto si conta lo stesso (un minimo) e il log del giro lo dice. **Un evento
   `whole_division` o senza scali non si cerca** (design §5.1: nessuna ricerca per ogni aeroporto della divisione). **«Non contato» non è
   0** (la condizione di Carmine): `evt_event_stats.flown_unbooked_airports` dice a quanti scali dell'evento, nel loro ordine, il conto è
   arrivato, e **vuoto è «non contato»** — un evento di tutta la divisione, uno senza scali, uno chiuso dopo trenta giorni prima di ogni
   conto —; la lettura e «Ricalcola» rispondono `flownUnbooked` vuoto, con `flownUnbookedAirports` e gli scali dell'evento, e la scheda lo
   dice in parole («non contato»; «contato su 2 dei suoi 3 scali» quando la rete ha smesso di rispondere a metà). Contato a pezzi (lettura 6),
   un pilota che ha volato senza prenotare in due scali contati in due giri diversi si conta due volte: un evento con uno o due scali si
   conta in un giro solo, ed esatto.
4. **La presenza nei turni**, solo per i turni `Unknown`: prima **l'archivio** (`SessionsOfAsync(vid, da, a)`, le connessioni sul
   nominativo della postazione); se l'archivio non risponde (`null`, oggi IT con `atcData: none`) o **non copre** l'inizio del turno
   (`Covers` falso, E10b: «non si sa»), **il tracker per VID**, tipo `ATC`, dall'inizio del turno − 12 ore (chi era già connesso), stesso
   nominativo. I minuti si contano **dentro il turno**, con le connessioni sovrapposte unite e una connessione ancora aperta fino alla fine
   del turno. Almeno `attendanceMinimumMinutes` — **e mai più della durata del turno**, così un turno di venti minuti con la soglia a trenta
   si può fare — → `Attended`; meno → `NoShowProposed`. Il job non scrive mai `NoShow` né `Excused`: sono dello staff (E13b). L'esito è una
   modifica del turno, con la sua riga nell'audit, scritta da nessuno (un job). **Una divisione senza archivio** (`atcData: none`,
   `UnavailableAtcActivitySource` del nucleo) non lo chiede affatto: nessuna unità spesa, nessun «l'archivio non ha risposto» nel giro
   (revisione della #278, punto 4); lo dice solo un archivio configurato che non risponde.
5. **Chi ha coperto**: una colonna nuova, **`evt_atc_shifts.covered_by_vid`** (migrazione `AddAfterTheEventColumns`, con
   `evt_event_stats.flown_unbooked_airports` della lettura 3, dopo
   `AddInPersonActivities` di E17), scritta solo accanto a un no-show proposto: **dall'archivio** (`OnlineAsync(da, a)`, le connessioni sul
   nominativo con un VID diverso dal titolare, quella con più minuti dentro il turno). **Con il solo tracker nessuno**: il tracker senza VID
   non si chiede per nominativo, e chiederlo per tutte le sessioni ATC della rete nella finestra costa pagine. Per IT è un buco breve:
   Carmine vuole `atcData: vipi` prima del primo roster vero ([sull'issue #259][a259]). Colonna di persona `…Vid`: la riga in
   `ErasureTests` (`evt_atc_shifts.covered_by_vid`), lo pseudonimo lo scrive il nucleo come in ogni colonna di persona.
6. **I lotti** (§10.2: ogni giro finisce dentro la richiesta del POST pianificato, che aspetta al più 80 s): un giro ha un **conto di 60
   unità**, circa i secondi di IVAO — una domanda per VID (tracker o archivio) vale 1, una domanda per scalo vale 11 (E10a: ~10,5 s per la
   pagina con l'ultima riga). Gli eventi in ordine di fine; per ognuno prima le prenotazioni, poi i turni, poi — quando nessuna riga resta da
   verificare — chi ha volato senza prenotare **a pezzi, per scalo** (la condizione di Carmine): ogni giro conta, nell'ordine degli scali,
   quanti ne stanno nel resto del conto (due domande da 11 ciascuno: due scali in un giro nuovo), e salva il pezzo con le statistiche
   (`flown_unbooked_airports` dice dove è arrivato); **un giro non spende mai più di 60 unità**. Contato l'ultimo scalo, le statistiche e
   `after_done_at` in una transazione. I numeri sono costanti del codice, da rimisurare con i limiti veri (design §9.1).
7. **Una sorgente che non risponde** (`null`): il giro smette di chiederle, le righe restano **da verificare** (né `flown_checked_at` né un
   esito) e l'evento resta aperto; il giro dopo riprova. **Trenta giorni dopo la fine** il job smette di chiedere: chiude l'evento con le
   righe ancora da verificare — **mai segnate** — e le statistiche le contano come da verificare (prenotati − volati − non volati); lo staff
   segna a mano i turni (E13b). Il tracker tiene le tracce circa novanta giorni. ⚠️ **Al primo avvio** con E13a ogni evento pubblicato e
   finito è da verificare: quelli finiti da meno di trenta giorni si verificano a lotti come gli altri, quelli più vecchi si chiudono subito,
   senza chiedere niente, con le statistiche dalle righe e chi ha volato senza prenotare «non contato» (revisione della #278, punto 7).
8. **Le statistiche** (§1.10) si fanno in un posto solo, dal job e da «Ricalcola», sulle righe: slot pubblici e privati, prenotati,
   volati (`flown_at`), non volati (verificati senza volo), disponibilità, turni, presenti, **no-show = proposti più confermati**, giustificati;
   PIREP e segnali di award a 0 finché non ci sono le tabelle di E14a ed E14b; per un evento in presenza iscritti, somme e posti **dalle righe
   finché ci sono**, e dopo la conservazione restano quelli scritti da `events-retention` (E17). **Chi ha volato senza prenotare** viene solo
   dalla verifica: «Ricalcola» lo tiene com'è, perché ricontarlo è chiedere di nuovo IVAO dentro una richiesta. Ricalcolare dà gli stessi
   numeri finché le righe non cambiano.
9. **«Ricalcola»** è un verbo, **`POST /api/events/events/{id}/stats`**, con `Events.Edit` sulla riga dell'evento (scrive una riga
   dell'area `Events`), solo per un evento pubblicato, non annullato e finito; risponde i numeri nuovi. **La lettura** è una risorsa del
   motore in sola lettura, `/api/events/stats` con `filter[eventId]`, `Events.View`: nessuna lettura scritta a mano. **La riga delle
   statistiche si scrive sotto il blocco della riga dell'evento** (`FOR UPDATE`), dal job e da «Ricalcola»: le due nello stesso istante non
   la inseriscono due volte (revisione della #278, punto 3; provato con una transazione del test che tiene il blocco — senza, «Ricalcola»
   rispondeva 500).
10. **La scheda «Statistiche»** sta sulla pagina dell'evento dello staff, per chi ha `Events.View`, su un evento pubblicato e finito: i
    numeri in un **form generato in sola lettura** (le parole sono della pagina), «Ricalcola» a chi scrive l'evento, e **due liste
    generate** con i dati — le prenotazioni con il loro esito (`EventBookingDto` ha `flownAt` e `flownCheckedAt` in più; con
    `EventBookings.View`) e i turni con l'esito e chi ha coperto (la lista della risorsa dei turni, che nessuna pagina leggeva, dà il
    controllore, il nominativo e chi ha coperto con `MemberNames`; con `EventAtc.View`). ⚠️ **La lista dei turni del motore**
    (`/api/events/atc/shifts`) risponde ora `EventAtcShiftRowDto` invece di `EventAtcShiftDto`: nessun altro la chiamava; il form di un turno
    legge il dettaglio, che resta `EventAtcShiftDto` (revisione della #278, punto 5). Le somme delle risposte e i posti restano nelle
    loro schede, che li leggono già dalle statistiche dopo la conservazione (E17). Le ore con `formatMoment`/`useMoment` (E10p).

[a259]: https://github.com/SkyMistery/Ivao-Italy-Hub/issues/259#issuecomment-6146395689

## 3. Alternative scartate

| Alternativa | Perché no |
|---|---|
| La finestra intorno all'orario di ogni slot (± qualche ora) | un pilota in ritardo di tre ore ha volato l'evento; e «senza prenotare» deve contare gli stessi voli nella stessa finestra |
| Contare le sessioni senza prenotazione, non le persone | il design dice «chi»: una riconnessione non è un pilota in più |
| Salvare i numeri di sessione già contati per spezzare il conto fra due giri | un numero di sessione ritrova la persona su IVAO (E10a, la revisione della #210) |
| Chi ha coperto calcolato quando lo staff guarda (E13b) | chiede IVAO dentro una lista, e il tracker dopo novanta giorni non risponde più |
| Il nominativo nella domanda del tracker senza VID (una fase del nucleo prima di E13a) | serve solo finché IT è senza archivio; resta possibile dopo, come fase del nucleo con la sua nota |
| No-show nelle statistiche solo se confermati | subito dopo il giro i turni non tornerebbero (turni ≠ presenti + no-show + giustificati); «Ricalcola» dopo E13b li aggiorna |
| Riprovare per sempre una sorgente che non risponde | un evento non si chiude mai e ogni ora costa la stessa attesa |

## 4. La domanda per Carmine

Le dieci letture del §2, con le due in cui il design lascia una scelta vera:

- **la lettura 5** — (A) `covered_by_vid` dall'archivio, nessuno con il solo tracker; (B) prima una fase del nucleo che mette il
  nominativo nella domanda del tracker senza VID, ed E13a lo legge anche da lì. **Raccomandata (A)**.
- **la lettura 7** — (A) dopo trenta giorni dalla fine il job chiude l'evento con le righe da verificare; (B) riprova per sempre.
  **Raccomandata (A)**.

**La risposta di Carmine** ([sull'issue #272][a272], 16 ottobre 2026): **(A)** alla lettura 5 — chi ha coperto si legge
dall'archivio, con il solo tracker resta sconosciuto, nessuna fase del nucleo prima di E13a — e **(A)** alla lettura 7 — trenta giorni
dopo la fine il job chiude l'evento con quelle righe ancora da verificare, mai segnate, e lo staff le può ancora segnare con E13b. Le
dieci letture restano **Proposta** fino alla lettura della PR. (La fase del nucleo E10t, `INetworkSessionSource`, è un'altra cosa: il
nome neutro del tracker che il modulo deve usare, §«Regola applicata», non il nominativo nella domanda della scelta (B).)

**La seconda risposta** ([sull'issue #272][a272b], 16 ottobre 2026, dopo [i rilievi del revisore sulla #278][r278]): **sì alle dieci
letture**, con le due condizioni scritte nelle letture 3 e 6 — il conto per scalo, così ogni giro finisce dentro la richiesta, e «non
contato» distinto da 0.

## Da portare nel piano

§9.2 riga Events (le verifiche dopo l'evento: la finestra degli slot, chi ha volato senza prenotare come numero di persone, chi ha coperto
dall'archivio, «non contato» distinto da 0), §10 (il lotto di `events-after`, i suoi pesi e il conto per scalo, la chiusura dopo trenta
giorni). Il design `09-design-m4.md` §1.7 (la colonna `covered_by_vid`), §1.10 (`flown_unbooked_airports`), §5.1 e §8.4 (le letture 1–7).
