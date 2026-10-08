# Il foglio degli slot e l'esportazione: le letture del design (E5)

**Data:** 6 ottobre 2026 — fase E5 di M4 (gli slot pubblici e l'esportazione), PR #228
**Stato:** **decisa** (Carmine, 6 ottobre 2026: [le sue risposte][a228], pubblicate dal master sulla #228 su sua istruzione). **Sì agli
otto punti** come scritti, e **due in più** sulle domande che il revisore gli ha girato ([osservazioni][v228], «For the maintainer»
n.2): **9**, l'esportazione porta la versione del suo contratto; **10**, uno slot cade nella finestra del suo evento, con un margine
(§2). Le correzioni chieste dalla revisione (punti 3–8) sono in «Com'è andata» di E5 (`10-piano-implementazione-m4.md`).
**Regola applicata:** `CLAUDE.md` §5, casi **(a)** e **(b)**: nessun meccanismo nuovo — `MapCrud`, `Refusals`, l'unico handler, il
guardiano dell'interceptor, i token personali con la loro `audience`, `IAirportDirectory`, `IAircraftTypeDirectory`, le impostazioni del
modulo —; sono letture del design M4 §1.5, §3.1, §7.1, §7.2, §7.4 e della nota `2026-09-29-gli-slot-e-le-prenotazioni` §2.2–§2.3. Per il
punto 9 il controllo della versione è un pezzo dei tour che serve a un secondo modulo: caso (b), passa nel nucleo con la fase **E10g**
(la #230, nota `2026-10-06-la-versione-di-un-contratto-nel-nucleo`), prima del codice degli eventi che lo usa (`CLAUDE.md` §0 regola 6):
la #228 è in coda dopo la #230.

[a223]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/223#issuecomment-6017107039
[a228]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/228#issuecomment-6022686808
[v228]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/228#issuecomment-6021830879
[q230]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/230#issuecomment-6026482474

## 1. Le letture (i punti 1–8)

1. **Il foglio lo legge il server.** Il design dice che la tabella incollata «arriva come testo separato da tabulazioni» e scrive i rifiuti
   con il nome della colonna (`rows[12].aircraft_types`): la richiesta porta il **testo** — incollato, o letto dal browser da un file CSV
   nella stessa casella — e il modo, e il server lo legge (`SlotSheet`). Un solo lettore per le due strade, provato da test di unità in
   C#. Non è l'import delle leg dei tour (M2, T8), che legge un XLSX nel browser con SheetJS (risposta 24): qui l'`.xlsx` è escluso (c3),
   e un testo non chiede una libreria. Alternativa: leggere nel browser e mandare righe JSON, con i rifiuti in `rows[n].aircraftTypes`.
2. **La tabella.** Una riga d'intestazione con i nomi del design §3.1 in qualunque ordine, maiuscole o minuscole: gli otto obbligatori
   nell'intestazione (le celle di `flight_number` e `stand` possono essere vuote), `rotation` e `leg` facoltativi, una colonna che l'hub
   non conosce lasciata com'è. Il separatore è quello dell'intestazione: una tabulazione, altrimenti il **punto e virgola** (il CSV di un
   foglio di calcolo dove la virgola è il separatore decimale, come in italiano), altrimenti la virgola; una cella fra virgolette tiene il
   separatore, le virgolette raddoppiate e un a capo. **Le righe si contano come le conta la tabella**: l'intestazione è la riga 1 quando
   il testo comincia con lei, una riga vuota conta e si salta, così `rows[12]` è la riga 12 che il foglio o il file mostrano. Al più
   **1000 righe**. **Gli orari solo in UTC, scritti `2026-10-17 14:30`** (con la `T`, i secondi e la `Z` se scritti): niente giorno
   prima del mese né mese prima del giorno, che un foglio di calcolo scrive secondo la lingua — il file si pulisce, non si indovina (come
   per le leg dei tour). I tipi d'aereo separati da `/`, ognuno uno che il nucleo conosce, al più dieci; il nominativo lettere e cifre.
3. **Il verso, e uno slot sempre su uno scalo dell'evento.** Il design dice «dedotti dagli ICAO» (§1.5): lo slot è una **partenza** se
   parte da uno scalo dell'evento — **anche quando arriva a un altro scalo dell'evento**: il suo orario allo scalo è l'off block con cui
   il pilota prenota —, altrimenti un **arrivo** se arriva a uno, altrimenti è **rifiutato** (`slotAwayFromEvent`). Così «ogni due tratte
   uno scalo dell'evento» (§3.1) non chiede una regola sua: ogni tratta tocca uno scalo, quindi la catena ne tocca uno ogni due tratte.
   Alternativa: ammettere in una rotazione una tratta che non tocca l'evento (LIRF-LIRN, LIRN-LIML, LIML-LIRF), senza scalo dell'evento
   — ma il Gate Manager, la capacità dei privati (E7) e la pagina leggono lo slot su uno scalo.
4. **Le rotazioni.** Una rotazione è ogni slot con il suo codice: quelli salvati che restano e quelli della tabella. Le tratte stanno
   nell'ordine dei loro posti (`leg`); **scritte tutte senza posto, lo prendono dai loro orari**; alcune con il posto e altre no, o lo
   stesso posto due volte, è rifiutato. Fra una tratta e la successiva: si parte da dove si è arrivati (`chainBroken`), non prima di
   quella prima (`chainOrder`), almeno `bookingGapMinutes` dopo il suo on block (`chainTooClose`). Il rifiuto cade sempre su una tratta
   che si sta scrivendo, mai su una salvata. Il codice si confronta com'è scritto (`R1` e `r1` sono due rotazioni). **Il form di uno
   slot** — le correzioni, e anche uno slot nuovo scritto da solo — **tiene le stesse regole** del caricamento, e la prima tratta di una
   rotazione nuova scritta senza posto prende il primo.
5. **«Sostituisci» ed «elimina i liberi».** «Sostituisci» toglie gli slot **pubblici liberi** e mette quelli della tabella (§3.1); fino
   alle prenotazioni (E6a) ogni slot è libero, e il punto che dice «libero» è uno solo (`SlotRows.Free`). «Elimina i liberi» (§7.2) toglie
   **ogni** slot libero dell'evento, **pubblici e privati**: i privati si rigenerano (E7). Ognuno con la sua riga di audit.
6. **Le regole che crescono con gli slot.** Il caricamento è rifiutato su un evento **senza slot pubblici** (l'interruttore dice che cosa
   l'evento ha, §1.2) o **senza scali suoi**; l'interruttore degli slot pubblici **non si spegne** finché l'evento ne ha (`hasPublicSlots`,
   come `wholeDivisionHasAirports`); uno scalo dell'evento **su cui ci sono slot non si elimina e non cambia codice** (`airportHasSlots`);
   eliminare un evento porta via i suoi slot nello stesso salvataggio, e il guardiano chiede `EventBookings.Edit` a chi elimina (EC ed EAC
   ce l'hanno, §6.2), come per le rotte. La finestra dell'evento è il punto 10 (§2).
7. **L'esportazione** (§7.4). Un array JSON dei voli, con i nomi che il Gate Manager legge oggi (`callsign`, `booked_by`, `aircraft_icao`,
   `gate`, `eobt`, `eat`, `origin_icao`, `destination_icao`) e i quattro nuovi (`slot_id`, `flight_number`, `rotation`, `leg`, e per un
   privato `paired_slot_id`), gli orari in ISO 8601 UTC con la `Z`, **nell'ordine dell'orario allo scalo dell'evento**: l'off block di una
   partenza, l'on block di un arrivo. `booked_by` e `aircraft_icao` vuoti finché non ci sono prenotazioni (E6a: **il tipo scelto dal
   pilota**, mai uno di quelli ammessi); `gate` è lo stand dello slot, vuoto quando non c'è. Un privato esce con il suo scalo e il suo
   orario soltanto. **Una bozza no** (deciso, §17.3 n.6): **409** con `code: "draft"`, perché un 404 farebbe credere a chi configura il
   Gate Manager di aver sbagliato l'indirizzo; un evento pubblicato sì, in ogni stato (programmato, concluso, annullato). Il permesso
   `EventBookings.View` si chiede all'unico handler sulla riga dell'evento. 401 senza token o con il cookie del back office, 403 con un
   token di un'altra `audience`, 404 per un indirizzo che nessun evento ha. **Il 404 viene prima del 403, ed è voluto** (revisione, punto
   7): è l'ordine dei verbi del back office, un token di questa `audience` lo fa solo chi ha `EventBookings.View` da qualche parte, e
   l'indirizzo di un evento pubblicato è comunque sul sito. La versione del contratto è il punto 9 (§2).
8. **La lista pubblica sulla pagina** (§7.1) viaggia nella lettura della pagina che c'è già (`PublicEventDto.Slots`, accettata da Carmine
   sulla #223): **nessun endpoint nuovo**. Per ogni slot pubblico il volo, gli orari, lo stand, la rotazione e il posto, e **libero o
   preso** — mai chi (piano §9.7); i privati si offrono per scalo e ora con E7. Le rotazioni raggruppate, ognuna dove cade la sua prima
   tratta; gli orari in UTC, con il giorno detto una volta quando cadono tutti in uno.

**Gli endpoint scritti a mano di E5** (per il conto del piano §16.6): tre verbi che il design nomina (§7.2: «incollare», «esportare», e
«elimina i liberi» della scheda Slot) — `POST /api/events/events/{id}/slots/load`, `POST /api/events/events/{id}/slots/delete-free` e
`GET /api/events/{slug}/bookings/export`, il contratto di un programma esterno come l'agente del validatore (M2). Gli slot uno per uno sono
`MapCrud` (`/api/events/slots`). Nessuna lettura nuova.

## 2. Le due in più (i punti 9 e 10)

9. **L'esportazione porta la versione del suo contratto** (Carmine: il Gate Manager è un programma suo, e romperlo in silenzio è quello
   che la versione impedisce).
   - **Un'intestazione sua**, `Hub-Bookings-Contract` (l'`audience` è `events.bookings`, come `Hub-Agent-Contract` sta a
     `flightops.agent`); la versione corrente è la **1**, le accettate `[1]`. Senza intestazione, o con una versione che l'hub non parla,
     **400** con `code: "bookingsContract"`, `current` e `accepted`, e il titolo `events:errors.bookingsContract`; con una versione
     accettata, la risposta la ripete nella stessa intestazione. Come per l'agente (piano §16 punto 10): dentro una versione l'hub solo
     **aggiunge**, e una modifica che rompe il Gate Manager è la versione 2, accettata accanto alla 1 per almeno un rilascio.
   - **L'ordine delle risposte**: prima il token (401, e 403 per un token di un'altra `audience`: la policy), poi la versione (400), poi
     l'evento (404, 403 sulla riga, 409 per una bozza).
   - **Il controllo non è una copia.** Quello dei tour (`AgentContract.RequireVersionAsync`) sta nel loro modulo, che gli eventi non
     referenziano, e una copia negli eventi sarebbe lo stesso pezzo scritto due volte (`CLAUDE.md` §2), l'alternativa che la nota della
     distanza (`2026-09-30-la-distanza-fra-due-aeroporti-nel-nucleo` §7) ha già scartato. Passa nel nucleo con la fase E10g, la #230
     (`ContractVersion`, nota `2026-10-06-la-versione-di-un-contratto-nel-nucleo`), e l'esportazione lo usa con i suoi valori: la strada
     scelta da dalberone il 6 ottobre, sulla classificazione chiesta dal master prima di scrivere. I tour ci passano in una sessione di
     Carmine ([la richiesta sulla #230][q230], nella nota di E10g §5): niente di questa PR ne dipende.
   - **Il documento pubblico** è `docs/events-bookings-export.md` (in inglese), come `docs/agent-contract.md`: il token, l'intestazione,
     l'indirizzo, i campi, le risposte che non sono 200, una `curl`.
10. **Uno slot cade nella finestra del suo evento.** L'orario di uno slot allo scalo dell'evento — l'off block di una partenza, l'on block
    di un arrivo, lo stesso orario dell'ordine dell'esportazione — sta fra **sei ore prima dell'inizio e sei ore dopo la fine**
    dell'evento (`SlotWindow.Margin`). L'orario all'altro aeroporto è libero: un volo lungo verso l'evento o dall'evento non è mai
    rifiutato per la sua durata. Fuori, `events:errors.slotOutsideWindow` sulla colonna di quell'orario (`rows[N].off_block_utc` o
    `rows[N].on_block_utc` nella tabella, `offBlockUtc` o `onBlockUtc` nel form), **nel caricamento e nel form di uno slot allo stesso
    modo**; la pagina del caricamento lo dice fra i formati.
    - **Perché sei ore.** Prendono il giorno sbagliato e il mese sbagliato, e per un evento di poche ore anche lo scambio fra mattina e
      sera (06:30 scritto per le 18:30). Lasciano passare l'arrivo che atterra dopo la fine (Carmine), l'ultima tratta di una
      rotazione che torna allo scalo a evento finito, e la prima ondata di un fly-in, partita prima dell'inizio. Due ore rifiuterebbero il
      ritorno di una rotazione dopo un evento corto; dodici lascerebbero passare l'errore delle dodici ore.
    - **Le date dell'evento che cambiano non ricontrollano gli slot salvati**: uno slot rimasto fuori resta finché qualcuno non lo salva di
      nuovo (allora è rifiutato) o un «sostituisci» non lo toglie, se è libero. Non è una regola chiesta: è scritto nell'HANDOFF.

## 3. Che cosa si è toccato

Solo il modulo: `EventSlot.cs`, la migrazione additiva `AddEventSlots`, `Staff/SlotSheet.cs`, `Staff/SlotRules.cs` (anche
`SlotWindow`), `Staff/SlotLoading.cs`, `Staff/EventSlotEndpoints.cs`, `Export/BookingsExport.cs` (con il `ContractVersion` del nucleo),
`Staff/EventSaving.cs`, `Staff/EventAirportEndpoints.cs`, `Public/PublicEvents.cs`, `EventsModule.cs` (l'`audience` `events.bookings`), le
schermate di `web/src/modules/events/` e `docs/events-bookings-export.md`. Del nucleo solo le due righe di `ErasureTests`, con la loro nota
(`2026-10-06-le-colonne-degli-slot-in-erasuretests`); il pezzo del nucleo del punto 9 è della PR di E10g, non di questa.

## 4. La domanda a Carmine, e la risposta

> Otto comportamenti degli slot pubblici e dell'esportazione che il design non dice, scritti in E5 come raccomanda questa nota: (1) la
> tabella la legge il server, incollata o da un file CSV; (2) le colonne del design in qualunque ordine, tabulazioni, punto e virgola o
> virgole, i rifiuti con la riga come la conta la tabella, gli orari solo `2026-10-17 14:30` in UTC, al più 1000 righe; (3) uno slot
> sempre su uno scalo dell'evento — partenza se parte da uno, anche fra due scali dell'evento, altrimenti arrivo —, quindi «ogni due
> tratte uno scalo» viene da sé; (4) le tratte di una rotazione nell'ordine dei posti, o dei loro orari quando non sono scritti, e il form
> di uno slot tiene le stesse regole; (5) «sostituisci» toglie i pubblici liberi, «elimina i liberi» ogni slot libero, privati compresi;
> (6) niente caricamento senza slot pubblici o senza scali, l'interruttore non si spegne e uno scalo non si toglie sotto gli slot; (7)
> l'esportazione come array con i nomi del Gate Manager, una bozza 409 `draft`, un evento pubblicato in ogni stato; (8) la lista pubblica
> nella lettura della pagina, libero o preso e mai chi. Confermi?

**La risposta** (6 ottobre 2026, [risposte di Carmine sulla #228][a228]): **sì agli otto**, come scritti; e i punti 9 e 10 (§2), codice
di questa PR accanto alle correzioni della revisione.

## Da portare nel piano

- Design M4 §1.5: il verso fra due scali dell'evento; uno slot sempre su uno scalo dell'evento.
- Design M4 §3.1: il foglio letto dal server (colonne, separatori, righe contate come la tabella, orari solo in UTC, al più mille righe);
  le rotazioni con i posti dagli orari; «sostituisci» ed «elimina i liberi»; **la finestra dell'evento, sei ore per parte sull'orario
  allo scalo**, nel caricamento e nel form.
- Design M4 §7.1: la lista pubblica nella lettura della pagina.
- Design M4 §7.4: la forma dell'esportazione, nell'ordine dell'orario allo scalo dell'evento; il 409 di una bozza; **la versione del
  contratto** nell'intestazione `Hub-Bookings-Contract`, con `docs/events-bookings-export.md`; l'ordine delle risposte.
- Piano §16 punto 10: **il secondo cliente che non viaggia con il pacchetto** è il Gate Manager, con l'esportazione delle prenotazioni
  versionata nella sua intestazione `Hub-Bookings-Contract` (`docs/events-bookings-export.md`) e controllata da `ContractVersion` del
  nucleo (nota di E10g).
- Piano §16.6: il conto degli endpoint a mano di M4 (tre verbi di E5).
- `docs/FORKING.md`, quando avrà la sua sezione degli eventi (come T20 per i tour): rimanda a `docs/events-bookings-export.md` come
  quella dei tour rimanda a `docs/agent-contract.md`. Oggi il file non ha una sezione degli eventi, e E5 non ne apre una.
