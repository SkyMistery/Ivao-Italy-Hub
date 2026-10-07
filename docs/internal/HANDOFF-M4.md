# HANDOFF — stato di M4 (Events)

> Documento **interno** (italiano). È il punto d'ingresso di ogni sessione che lavora al modulo Events. Lo scrive **la sessione
> di lavoro** di ogni fase, alla fine, con un paragrafo «Che cosa ha lasciato <fase>» in cima alla sezione «Lo stato». Lo stato
> generale del progetto sta in `HANDOFF.md`, che scrive solo il master. Le regole — chi unisce, che cosa non si tocca, come si
> ottiene una decisione — sono in `CLAUDE.md` §0 e in `10-piano-implementazione-m4.md`, «Regole di tutte le fasi», e non si
> ripetono qui.

**Ultimo aggiornamento:** 7 ottobre 2026 — **fase E5** (modulo: gli slot pubblici e l'esportazione), sul branch `m4/e5-public-slots`,
**PR #228** verso `main`, nata **in coda dopo la #223** di E4 — dalla sua testa `94ca28b`, e unita di nuovo alla sua ultima spinta `3224a9f`
(le risposte di Carmine, `main` con la #222, i tipi degli eventi) —; **la #223 è unita** (6 ottobre 2026, `77a2031`) prima che la #228 si
aprisse. **Dalla revisione la #228 è in coda dopo la #230** di E10g (la versione di un contratto nel nucleo, da `main` a `e9702b2`): ha
unito il suo branch a `cc1b46c`, e con lui `main`. Sono unite E1 (#200), E2 (#209), E2b (#212), E3a (#214), E3b (#221), E4 (#223), E10a
(#210), E10b (#208), E10c (#204), E10d (#205), E10e (#206), E10f (#213) ed E15a (#207), il passaggio dei tour al calcolo del nucleo (#211),
la `0.6.0` (#216), il piano 1.29 (#217), la parola degli eventi nella ricerca (#222), le altre correzioni del nucleo fino alla `0.6.5`
(#218, #219, #225), gli spec che dicono al banco che cosa rimettono a posto (#227) e il piano 1.30 (#229). **E4b** («chi è online sugli
scali», la fase del nucleo decisa da Carmine sulla #223) è la **#226**, aperta, da `main`: la striscia sulla pagina dell'evento la monta la
prima fase del modulo dopo che E4b è unita.
**Il prossimo passo**: **E10g** (la #230), poi **E5** (la #228, in coda dopo la #230), poi **E6a** (prenotare: il server), in coda sul
branch di E5. Da E3b un evento si **pubblica**, entra nel calendario e nella ricerca quando si vede e ne esce alla fine, e tiene i suoi
file; da E4 ha la sua pagina `/events/{slug}`, sta in `/events` e nel blocco `events.eventList`, il FOD ne scrive le rotte, e il suo tipo è
uno dei tipi degli eventi («Che cosa ha lasciato E4», sotto); da E5 ha i suoi **slot pubblici**, caricati da una tabella con le rotazioni e
mostrati sulla sua pagina, e il Gate Manager li legge con un token personale («Che cosa ha lasciato E5», sotto). **Carmine ha risposto
sulla #228** ([le sue risposte][a228]): sì alle otto letture della nota `2026-10-06-il-foglio-degli-slot-e-l-esportazione`, ora
**decisa**, e due punti in più, fatti sulla stessa PR — uno slot cade nella finestra del suo evento (sei ore per parte), e l'esportazione
porta la versione del suo contratto. ⚠️ **Il controllo della versione è del nucleo**: lo porta **E10g** (`ContractVersion`, la #230:
«Che cosa ha lasciato E10g», sotto), perché quello dei tour sta nel loro modulo e una copia negli eventi sarebbe lo stesso pezzo scritto
due volte. **Dopo la prova sul banco (7 ottobre) dalberone ha riaperto la #228** per la pagina degli slot: il tipo principale, partenze e
arrivi per scalo, le rotazioni segnate, il dettaglio di uno slot, e `aircraft_types` nell'esportazione — fatti su questa PR, con una nota
**«Proposta»** e la domanda a Carmine (`2026-10-07-gli-slot-sulla-pagina-dell-evento`). **E6a** (prenotare: il server) è in coda sul
branch di E5 da `d901f43` e aspetta E10h (del nucleo): unisce la nuova testa di E5 quando la sessione che coordina glielo dice. ⚠️ **Fra E3b ed E4 nessuna consegna e nessun «Pubblica»
sull'installazione di prova** (Carmine, 6 ottobre 2026, [sulla #221][seq221]): la voce di calendario e la riga di ricerca di un evento
pubblicato puntano a `/events/{slug}`, una pagina che porta solo E4 — **con E4 unita dopo la #221 il vincolo cade** (la pagina c'è).
**Le tre domande di E4 hanno la risposta di Carmine** ([sulla #223][ok223]): sì alle cinque letture del pubblico, E4b come fase del
nucleo, la seconda lettura scritta a mano accettata, i tipi come raccomandato — fatti su questa PR. **La
lettura dei preset** (`GET /api/events/kind-presets`) **resta**: Carmine l'ha accettata sulla #214 come scostamento dal design §7.2 (nota
`2026-10-01-la-lettura-dei-preset-dei-tipi`; «Che cosa ha lasciato E3a», sotto). ⚠️
**`EffectivePermission.FromOutside` è uno solo**, con le due vie che lo danno — un grant a una posizione su un altro dipartimento o al
team di un FIR (E2b), un permesso globale da un grant (E10f) — e una condizione sola nel calcolo (nota di E2b, §3.8; «Che cosa ha
lasciato E2b», sotto). **E11a** ed **E11b** trovano in E10c le postazioni della divisione, il rating preferito e il minimo di una
postazione (l'FRA di IVAO); E10a serve a **E13a**, che aspetta anche E12; **E11b** ed **E13a** trovano in E10b la storia di un
controllore e la presenza in un turno; **E14a** ed **E14b** trovano in E10e la distanza nel nucleo; **E14b** trova in E10d il riepilogo
a chi assegna gli award, e non chiama niente: con E10f il riepilogo arriva anche al coordinatore e all'assistente dell'MD; **E15b** (dopo
E14b) trova in E15a le prenotazioni della rete.

## Per chi prende M4 (`dalberone`)

Scritta dal master il 30 settembre 2026, quando Carmine ha affidato **tutta M4** a `dalberone`, fasi del nucleo comprese (nota
`2026-09-30-m4-al-collaboratore`). È quello che le sessioni del maintainer sanno e che M3 può non aver mostrato; ogni punto è
verificato su `origin/main` quel giorno.

- **Le regole di tutte le fasi** di `10-piano-implementazione-m4.md` valgono per te, con una correzione: dove dicono «una sessione di
  lavoro di Carmine» leggi **la tua sessione, nel tuo worktree**, e l'avviso che «`core-guard` non giudica le PR del proprietario» non
  vale più: le tue PR le giudica. `core-guard.sh` riconosce come tuoi `src/IvaoHub.Modules.Events/`, `web/src/modules/events/`,
  `locales/<lang>/events.json` e i test e le spec che cominciano con `Events`/`events` (`EventsSkeletonTests.cs`,
  `web/e2e/full/events-*.spec.ts`); **`tests/IvaoHub.IntegrationTests/SampleEvents.cs`** e le sue migrazioni `…_AddSampleEvents` sono
  il modulo di prova del nucleo, non gli eventi: toccarli è nucleo. La voce «Chi scrive» di `10` è già corretta (piano 1.25).
- **Le fasi del nucleo** (E1, E10a–E10e, E15a) sono **ognuna una PR a sé con una nota nuova** in `decisions/` (caso (b): quale
  meccanismo si estende e perché il modulo non ne fa a meno), unite **prima** della fase del modulo che le usa. Senza la nota
  `core-guard` va rosso sui file del nucleo. Non si mischiano con codice del modulo.
- **Il perimetro di IVAO** (`CLAUDE.md` §3): il codice che nomina IVAO sta in `src/IvaoHub.Core/Ivao/` e nella metà IVAO di
  `src/IvaoHub.Core/Auth/`. E10a, E10b ed E15a toccano `Core/Ivao/`: si estende **l'unico** `IIvaoApiClient`
  (`IIvaoApiClient.cs`, `IvaoApiClient.cs` con `IMemoryCache`; il resiliente è `AddStandardResilienceHandler()` in
  `IvaoServiceCollectionExtensions.cs`), e **anche `FixtureIvaoApiClient.cs`**, che in sviluppo risponde al posto di IVAO. Il modulo
  non parla mai con IVAO. Le risposte si leggono in un lettore unico per i due client (`IvaoTrackerReader` in `IvaoTracker.cs`: «a
  fixture that parsed itself differently from production would be a fixture that proves nothing»).
- **La richiesta al tracker oggi** è `IvaoSessionQuery` (`IvaoTracker.cs`): **per VID** (`userId`), una finestra, facoltativi partenza
  e arrivo; `PageSize = 50` («what the API was measured to accept») e **al più `MaxSessions = 200`** sessioni, poi si smette di
  sfogliare. E10a (senza VID, per postazione e tipo di connessione) cambia la forma della domanda: la misura del limite di pagina e del
  tetto va rifatta con il token vero, non ereditata.
- **Le fixture** si registrano con `tools/record-ivao-fixtures.mjs` (dalla radice, con `config/ivao-oauth.json`): anonimizza il VID nel
  VID dei test e toglie l'oggetto `user` di IVAO; oggi registra per VID, per elenco, aeroporti e `/users/me`. E10a ed E15a aggiungono
  le loro modalità allo script (è un file del nucleo: `tools/`). **Mai una chiamata vera in un test.** Le tracce restano su IVAO circa
  novanta giorni.
- **La cancellazione** (`tests/IvaoHub.IntegrationTests/ErasureTests.cs`): dalla #187 il test legge da solo i contesti di **ogni**
  modulo abilitato e confronta le colonne `…Vid` e `…By` con la lista `PersonColumnsOfTheHub`. **Da E2** (`evt_events.cancelled_by`)
  ogni fase che crea una colonna di persona scrive la sua riga lì, o il test va rosso; è uno dei due casi in cui un test condiviso si
  tocca (`10`, «Regole di tutte le fasi»), e la PR lo dice.
- **Gli aiuti del nucleo che conosci da M3** valgono uguali: `personName`/`isErased` (`web/src/shared/ui/people.ts`) e la colonna
  `col.person` della lista generata (`web/src/shared/list/columns.ts`) per «persona cancellata»; `IHasAssignee` con `OnlyForAssignee`
  (e `AlsoOnCreation`/`AlsoOnDeletion`) per le righe affidate a chi scrive; i grant `firTeam` al team di un FIR; `Refusals` con
  `CrudProblems.Validation` (`src/IvaoHub.Core/Data/Crud/Refusals.cs`) per i rifiuti raccolti a mano. Mai una copia nel modulo.
- **Il banco e2e** (`web/scripts/e2e-server.mjs`): `999001` il web master (`IT-WM`), `999002` il pilota (`?as=pilot`), `999003` l'assistente
  dei tour (`?as=assistant`, `IT-FOAC`), `999004` il trainer (`?as=trainer`, `IT-T01`). **E1 aggiunge il personaggio dell'ED**
  (`?as=events`, `IT-EC`, un VID dopo `999004`). Il database del banco `ivaohub_e2e` **si accumula** fra un giro locale e l'altro: una
  spec si riprende le sue righe (`CONTRIBUTING.md`, «Tests»).
- **La versione**: le tue PR **non alzano** `<Version>` in `Directory.Build.props`; la alza il master prima di un rilascio, con la regola
  scritta lì (la prossima è la `0.5.0`, piano 1.25). Sulla prova (`test.it.ivao.aero`) gira la **`0.4.1`**; tag, rilasci e consegne sono
  del maintainer.
- **Le decisioni**: una domanda a Carmine è un **commento sulla PR** con la nota «Proposta» e la raccomandazione; Carmine risponde lì,
  oppure in chat al master, che pubblica la risposta sulla PR su sua istruzione: il link al commento va nella nota. Il master non
  risponde mai al posto di Carmine.

## Da leggere, nell'ordine

1. `CLAUDE.md` (tutto, §0 per primo) e `CONTRIBUTING.md`.
2. Il piano `00-piano-di-progettazione.md`: l'intestazione e il changelog 1.24, **§9** (la riga Events di §9.2), **§9.3**, **§9.5**
   (il calendario unico e i tipi degli eventi), **§9.7** (contratti nucleo↔moduli, «Privacy dei membri», le collaborazioni
   ATC↔Events e FlightOps↔Events), **§10** (l'API di IVAO), **§13** (la riga M4), **§15** punto 12, **§16**.
3. **Il design `09-design-m4.md`**, tutto: è deciso (§17, con i link ai commenti di Carmine sulla #180).
4. **Le dieci note della fase E0** (`decisions/2026-09-29-*`, elencate in `10`, E0) e le note che decidono come stanno i moduli:
   `2026-09-13-moduli-non-subordinati-ai-dipartimenti`, `2026-09-13-ordine-dei-moduli`,
   `2026-09-15-permessi-su-una-riga-e-chi-ha-interesse`, `2026-09-27-i-capi-fir-sul-loro-fir`,
   `2026-09-28-i-job-quando-passenger-spegne-l-hub`.
5. **`10-piano-implementazione-m4.md`**: le regole di tutte le fasi, «Com'è andata» di E0 (che cosa c'è nel codice e che cosa no) e la
   fase che si apre.
6. **I moduli che esistono già**, da leggere e non da toccare né importare: `src/IvaoHub.Modules.FlightOps/` (lo stato dalle date,
   `TourReleaseJob`, le leg come righe figlie con `BeforeAuthorize`, il token dell'agente, `myTours`) e
   `src/IvaoHub.Modules.Training/` (lo scheletro di A4, `TrainingArchitectureTests`).

## Che cosa il nucleo dà già a Events

Verificato nel codice il 29 settembre 2026 (`10`, E0, «Trovato leggendo il codice»):

- **Il modulo** (`IModule`): permessi nel catalogo, impostazioni (`ModuleSettings`, lette campo per campo sopra i predefiniti), tipi
  di notifica, preferenze del membro, audience dei token personali, segmenti riservati, il contesto con la sua storia delle
  migrazioni. `division.json → modules.events.baseDepartment` è già `ED`.
- **Chi lavora**: i grant a una posizione con uno `scope` (`PositionGrantSeed`), al team di un FIR (`firTeam`, A11a), su una riga
  (`resource_scope`, `events:event:{id}`); `IHasStakeholder` con `DeniedToStakeholder`; `ISubmittedByMembers`; `IHasFir`.
- **Calendario, ricerca, award, usi dei file** con `IProjectable`; il job `media-expiry` che elimina un file quando tutti i suoi usi
  sono scaduti; `ProjectionRefresh` per riproiettare (come `TourReleaseJob`).
- **Lista e form generati** (`MapCrud`, `DataList`, `SchemaForm`), `BeforeAuthorize`, `DeletePolicy`, `Refusals` e
  `CrudProblems.Validation`, `BlockDocument` per la descrizione, `MediaPicker`, `CalendarView`.
- **Da IVAO**: `SearchSessionsAsync` **per VID**, `GetNetworkStatusAsync` (senza VID), `IAirportDirectory` con le coordinate,
  `IAircraftTypeDirectory`, `IvaoAirspace.Covers`/`Serves` per «della divisione».
- **La cancellazione dei dati di una persona**: lo pseudonimo nelle colonne `…Vid` e `…By` di ogni contesto dei moduli, e
  `IPersonalDataEraser` per quello che il modulo deve cancellare.

**Che cosa manca, e quale fase lo porta**: ~~i tipi `rfe`, `rfo`, `mse`, `onlineDay` e uno staff degli eventi sul banco (E1)~~
**portati da E1** (la chiave è `online-day`: sotto, «Che cosa ha lasciato E1»); le sessioni
senza VID, con il tipo di connessione (E10a); ~~il VID nelle sessioni condivise (E10b)~~ **portato da E10b** (e la storia di un
controllore: sotto, «Che cosa ha lasciato E10b»); ~~il rating preferito e minimo, le postazioni della
divisione per nominativo (E10c)~~ **portati da E10c** (il minimo è l'FRA di IVAO: sotto, «Che cosa ha lasciato E10c»); ~~la mail a chi assegna (E10d)~~ **portata da E10d** (un riepilogo al giorno: sotto, «Che cosa ha
lasciato E10d»); ~~la distanza nel nucleo (E10e)~~ **portata da E10e** (`GreatCircle` in `Core/Airspace/`: sotto, «Che cosa ha
lasciato E10e»); ~~le prenotazioni ATC della rete (E15a)~~ **portate da E15a** (`IAtcBookingSource`: sotto, «Che cosa ha lasciato
E15a»); la versione del contratto di un programma esterno, che E0 non prevedeva, **portata da E10g** (`ContractVersion`, per
l'esportazione di E5: sotto, «Che cosa ha lasciato E10g»);
l'helper «persona cancellata» e `ErasureTests` che legge ogni modulo sono già arrivati con A12a di M3 (#187): **E8a è tolta** (piano
1.25), e da E2 ogni fase che crea una colonna di persona scrive la sua riga in `ErasureTests`.

## Per i test

VID `761001–761099`, slug `evt-test-` (il master li scrive in `CONTRIBUTING.md` prima che E2 sia unita). **Attenzione all'ED e
all'MD**: i test dei contatti affermano i destinatari esatti dell'MD e seminano `IT-EC` con un indirizzo; nessuno staff dell'ED o
dell'MD con un indirizzo nei test del modulo, i permessi con grant a un VID. Nessuna chiamata a IVAO: fixture.

## Lo stato

*(Qui, in cima, il paragrafo «Che cosa ha lasciato <fase>» di ogni fase chiusa, la più recente per prima.)*

### Che cosa ha lasciato E5 (6–7 ottobre 2026, branch `m4/e5-public-slots`, PR #228, nata in coda dopo la #223, unita prima che si aprisse; dalla revisione in coda dopo la #230 di E10g)

- **Che cosa c'è** (il dettaglio in `10`, E5, «Com'è andata»; una migrazione additiva, `AddEventSlots`; del nucleo solo le due righe di
  `ErasureTests`; due note nuove: `2026-10-06-il-foglio-degli-slot-e-l-esportazione`, **decisa** da Carmine sulla #228 — [le sue
  risposte][a228]: sì alle otto letture, e i punti 9 e 10 sulle domande del revisore ([osservazioni][v228]) —, e
  `2026-10-06-le-colonne-degli-slot-in-erasuretests`, nessuna decisione nuova; il controllo della versione dell'esportazione è del nucleo,
  dalla #230 di E10g, unita a questo branch; dopo la prova sul banco, una terza nota, **«Proposta»** con la domanda a Carmine sulla #228:
  `2026-10-07-gli-slot-sulla-pagina-dell-evento`, il tipo principale e la pagina pubblica degli slot che dalberone ha chiesto):
  - **`evt_slots` intera** (`EventSlot`, design §1.5), per i pubblici di E5 e i privati di E7: `kind`, `event_airport_icao`, `is_arrival`,
    `callsign`, `flight_number`, `aircraft_types` (JSON), `departure_icao`, `arrival_icao`, `off_block_utc`, `on_block_utc`, `stand`,
    `rotation_code`, `rotation_leg`, `generated`, `row_version`; univoco `(event_id, callsign, off_block_utc)` (un privato non ha callsign, e
    l'indice ne lascia passare quanti vuole). Riga `IEventChild` nell'area **`EventBookings`**, `[Audited]`, lo scope dell'evento.
  - **Incolla o carica** (`POST /api/events/events/{id}/slots/load`, `EventBookings.Edit` sull'evento; `Staff/SlotLoading.cs`): il testo —
    incollato da un foglio di calcolo, o il file CSV letto dal browser nella stessa casella — e il modo (`Add`, `ReplaceFree`). **Lo legge il
    server** (`Staff/SlotSheet.cs`): l'intestazione del design in qualunque ordine, tabulazioni, punto e virgola o virgole, le virgolette; le
    righe contate come le conta la tabella (`rows[12].aircraft_types`); al più mille. Ogni riga uno slot pubblico (`SlotDraft.Read`): orari
    solo `2026-10-17 14:30` in UTC, tipi separati da `/`; tipi e scali chiesti al nucleo una volta per tutta la tabella; **il verso dagli
    ICAO** (`SlotDirection`: partenza da uno scalo dell'evento, anche fra due scali dell'evento, altrimenti arrivo, altrimenti rifiutato);
    callsign e off block una volta nell'evento; **le rotazioni** (`SlotChains`, con gli slot salvati che restano): i posti, o gli orari
    quando nessun posto è scritto, lo scalo che coincide, l'ordine, `bookingGapMinutes`; **la finestra dell'evento** (`SlotWindow`, il
    punto 10 di Carmine): l'orario allo scalo dell'evento — l'off block di una partenza, l'on block di un arrivo — fra sei ore prima
    dell'inizio e sei ore dopo la fine, `events:errors.slotOutsideWindow` sulla colonna di quell'orario; l'altro orario è libero. **Tutto
    o niente**, una transazione; **409 «carica di nuovo» solo per un'altra scrittura delle stesse righe** (una chiave che l'indice univoco
    ha già, o il deadlock di due insert della stessa chiave, cercati nella catena dell'eccezione come fa `InitialisationMarker`): ogni
    altro errore esce com'è (revisione, punto 4).
  - **La lista e il form generati** (`/api/events/slots`, `MapCrud`, `EventBookings.View`/`.Edit`): la scheda **«Slot»** della pagina
    dell'evento, su un evento con slot e scali suoi; il form di uno slot (`/staff/events/{id}/slots/{slotId}`, anche «Nuovo slot») tiene le
    regole del caricamento (`SlotSaving`, la finestra compresa), e i tipi in **due campi, «Tipo principale» e «Altri tipi»**
    (`MainAircraftType`, `OtherAircraftTypes` di `EventSlotWriteDto`), salvati principale per primo (`SlotValues.MainFirst`), ogni rifiuto
    sul suo campo; **il primo di `aircraft_types` è il principale**, anche nella cella `A320/A20N` della tabella; **«Elimina i liberi»**
    (`POST …/slots/delete-free`): ogni slot libero dell'evento, pubblici e privati. La pagina del caricamento
    (`/staff/events/{id}/slots/load`): l'intestazione da copiare, il file CSV, i rifiuti elencati per riga e colonna.
  - **Le regole che crescono**: niente caricamento su un evento senza slot pubblici o senza scali; l'interruttore degli slot pubblici non si
    spegne sotto gli slot (`events:errors.hasPublicSlots`); uno scalo con slot non si elimina né cambia codice (`airportHasSlots`); eliminare
    un evento porta via i suoi slot con l'audit (`EventSaving.DeleteAsync`).
  - **La pagina dell'evento** elenca gli slot pubblici (`PublicEventDto.Slots`, nella lettura che c'è già; `screens/EventSlots.tsx` e
    `screens/slotList.ts`), come dalberone l'ha chiesta dopo il banco (nota del 7 ottobre, «Proposta»): **per scalo dell'evento**, una
    sezione ciascuno quando sono più d'uno, e in ognuno **«Partenze» e «Arrivi»** in due tabelle, per l'orario allo scalo; **il tipo
    principale**, gli altri nel tooltip di Atmosphere al passaggio del mouse, al focus e al tocco; le tratte di una rotazione ognuna nella
    sua tabella, **segnate da un'icona** (`Repeat`) che lo dice; **una riga apre lo slot** in sola lettura in un dialog di Atmosphere, con
    tutti i tipi ammessi, gli orari, lo stand e le tratte della rotazione; **libero o preso — mai chi**. «Prenota» è di E6b, nel dialog.
  - **L'esportazione** per il Gate Manager (`GET /api/events/{slug}/bookings/export`, `Export/BookingsExport.cs`): con un token personale
    dell'`audience` **`events.bookings`** (`EventsModule.TokenAudiences`, permesso `EventBookings.View`, la parola
    `events:tokenAudiences.bookings`), chiesto anche all'unico handler sulla riga; un array con i nomi del Gate Manager (`slot_id`,
    `callsign`, `flight_number`, `booked_by`, `aircraft_icao`, `aircraft_types`, `gate`, `eobt`, `eat`, `origin_icao`, `destination_icao`,
    `rotation`, `leg`, `paired_slot_id` — `aircraft_types` aggiunto dopo il banco, i tipi ammessi con il principale per primo, vuoto su un
    privato, un'aggiunta alla versione 1), orari UTC con la `Z`, **nell'ordine dell'orario allo scalo dell'evento** (l'on block di un arrivo: fino alla
    revisione andava per off block, contro la nota). **Una bozza mai**: 409 `code: "draft"`; il 404 prima del 403 è voluto (revisione,
    punto 7). **La versione del contratto** (punto 9 di Carmine): l'intestazione **`Hub-Bookings-Contract: 1`**; senza, o con una versione
    che l'hub non parla, 400 `code: "bookingsContract"` con `current` e `accepted`; la controlla il `ContractVersion` del nucleo (E10g),
    dopo il token e prima dell'evento. Il contratto per chi scrive il programma è **`docs/events-bookings-export.md`** (inglese).
  - **I test**: `EventsSlotsTests` (unità, 41: il lettore, una riga, gli istanti, il verso, le catene, la finestra, i tipi del form),
    `EventsSlotsTests` (integrazione, 5 — la finestra, il volo ricaricato, l'ordine e la versione dell'esportazione dalla revisione, i tipi
    del form e `aircraft_types` dal banco —, VID 761012–761014, scali `XED1`–`XED4`, tipi `XE5A`/`XE5B`, slug `evt-test-e5-…`),
    `screens/slotList.test.ts` (vitest, 6) ed `screens/EventSlots.test.tsx` (vitest, 5: le sezioni, il tooltip al focus e al tocco,
    l'icona, il dialog), due test nella smoke `web/e2e/events-public.spec.ts` (10, **uno su un telefono**: `hasTouch`, il tocco vero in
    Chromium), il giro `web/e2e/full/events-slots.spec.ts` (il «fatta quando», con `afterwards(…)`).
    `EventsTestRows` toglie anche gli slot; `ErasureTests` ha le due righe di `evt_slots`. VID 761015–761016 e 761062–761067 restano
    liberi.
- **Che cosa deve sapere la fase dopo**:
  - **L'esportazione è la versione 1 di un contratto** (`docs/events-bookings-export.md`): dentro una versione l'hub solo aggiunge. E6a
    riempie `booked_by` e `aircraft_icao`, che ci sono già: nessuna versione nuova. Un campo tolto o rinominato, o un significato cambiato,
    è la versione 2, accettata accanto alla 1 per almeno un rilascio; il documento cambia nella stessa PR del codice.
  - **E6a**: **«libero» si dice in un posto solo**, `SlotRows.Free` (`Staff/SlotLoading.cs`): oggi ogni slot, perché non c'è ancora una
    prenotazione; E6a lo restringe agli slot che nessuna prenotazione nomina, e «sostituisci» ed «elimina i liberi» seguono. Poi:
    `PublicEventSlotDto.Taken` (oggi `false`), `booked_by` e `aircraft_icao` dell'esportazione (oggi vuoti; `aircraft_icao` è **il tipo scelto
    dal pilota**), «uno slot prenotato non si elimina» (il `Delete` del CRUD degli slot e il primo rifiuto di `EventSaving.DeleteAsync`), e le
    righe delle colonne di persona di `evt_bookings` in `ErasureTests`.
  - **E6b**: «Prenota» va **nel dialog dello slot** (`SlotDetail` in `screens/EventSlots.tsx`), che mostra già tutti i tipi ammessi, il
    principale per primo: fra quelli il pilota sceglie il suo, che diventa `aircraft_icao` dell'esportazione. La pagina pubblica non ha una
    lettura sua: tutto viene da `PublicEventDto.Slots`, e un campo che servisse è un'aggiunta a quel DTO.
  - ⚠️ **Il tooltip che si apre al tocco** (`Hint` in `screens/EventSlots.tsx`): il tooltip di Radix si apre solo al passaggio del mouse e
    al focus, e fra la pressione e il click lo chiude e lo riapre da sé (un tocco dà il focus al bottone dopo che il dito si alza).
    Per questo il click rovescia quello che si vedeva **quando la pressione è cominciata**. Un semplice «al click si rovescia» lascia aperto
    il secondo tocco (provato al contrario nella smoke con `hasTouch`). Un altro tooltip che debba aprirsi al tocco riusa `Hint`, non una
    copia; se serve fuori dal modulo, passa nel nucleo con una nota.
  - **E7**: un privato è `Kind = Private`, `Generated`, lo scalo e il verso, e l'orario allo scalo in `OffBlockUtc` (partenza) o
    `OnBlockUtc` (arrivo); l'esportazione li porta già così (gate vuoto, `paired_slot_id` vuoto finché E7 non lo legge dalla prenotazione);
    il form di uno slot li rifiuta (`events:errors.slotNotPublic`) e la scheda non dà loro «Modifica»; «elimina i liberi» li toglie.
  - **E8b** («Duplica») copia, a scelta, gli slot pubblici con le rotazioni: le colonne sono quelle di `SlotDraft`, e i posti ci sono già.
    Gli slot copiati si spostano con le date del nuovo evento, o cadono fuori dalla sua finestra (`SlotWindow`).
  - ⚠️ **Il verso si fissa quando lo slot si scrive** (`IsArrival` ed `EventAirportIcao`; revisione, punto 8): uno scalo aggiunto all'evento
    dopo lascia uno slot salvato com'era, finché qualcuno non lo salva di nuovo.
  - ⚠️ **Le date dell'evento che cambiano non ricontrollano la finestra**: uno slot rimasto fuori resta finché non si salva di nuovo
    (allora è rifiutato) o un «sostituisci» non lo toglie, se è libero.
  - ⚠️ **Le catene usano `bookingGapMinutes` di quando si caricano**: cambiare l'impostazione dopo non ricontrolla le rotazioni salvate.
  - ⚠️ **Due caricamenti dello stesso evento nello stesso momento li tiene solo l'indice univoco** (revisione, punto 6): ognuno controlla le
    catene con gli slot che legge, quindi insieme possono salvare una catena che nessuno dei due avrebbe accettato da solo. Due persone
    che caricano lo stesso evento nello stesso secondo sono un caso lontano; una catena così resta salvata, e chi corregge poi una delle sue
    tratte se la vede rifiutare finché non la ripara.
  - ⚠️ **FluentValidation: un `.When` alla fine di una catena di regole vale per tutta la catena** (`ApplyConditionTo.AllValidators`):
    scritta così, la regola del formato del callsign avrebbe spento anche «obbligatorio», e un callsign vuoto sarebbe passato. Trovato
    rileggendo, prima dei test: ora il formato sta in un `RuleFor` suo, e il test d'integrazione manda un callsign vuoto.
  - ⚠️ **Il test d'integrazione scrive un privato sul database** (nessuno lo genera ancora): E7 lo sostituisce con il generatore.
  - ⚠️ **Uno spec del giro completo dice che cosa rimette a posto con `afterwards(…)` del banco, mai in un `finally`** (#227,
    `CONTRIBUTING.md`): `events-slots.spec.ts` lo fa dal merge di `main` che è arrivato con il branch di E10g; `events-public.spec.ts` ed
    `events-staff.spec.ts` del giro completo, di E4 e di prima, hanno ancora il `finally`.

[a228]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/228#issuecomment-6022686808
[v228]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/228#issuecomment-6021830879

### Che cosa ha lasciato E10g (6–7 ottobre 2026, branch `m4/e10g-contract-version`, PR #230, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-10-06-la-versione-di-un-contratto-nel-nucleo.md`, scelta tecnica, con una richiesta e una
  domanda a Carmine):
  - **La versione di un contratto nel nucleo**: `ContractVersion` in `src/IvaoHub.Core/Auth/ContractVersion.cs`, namespace
    **`IvaoHub.Core.Auth`**, accanto ai token personali. Si costruisce con l'intestazione del contratto, la versione corrente, le
    accettate, la chiave del titolo e il `code`. `RequireAsync` è un filtro di endpoint che risponde come quello dei tour
    (`AgentContract.RequireVersionAsync`): l'intestazione senza spazi intorno, sole cifre ASCII, fra le accettate; altrimenti **400** con
    `code`, `current` e `accepted` e il titolo nella lingua di chi chiede; se accettata, la risposta ripete nella stessa intestazione **la
    versione parlata** (non la corrente), e l'endpoint risponde.
  - **Il costruttore rifiuta** un'intestazione che non è un token di HTTP, nessuna versione, una versione non positiva o ripetuta, la
    corrente fuori dalle accettate, una chiave o un `code` vuoti: quando il modulo dichiara il contratto, all'avvio.
  - **I test**: `tests/IvaoHub.UnitTests/ContractVersionTests.cs` (unità, 21), con **il test gemello**, che confronta il nucleo con i
    valori dei tour e il filtro dei tour su 120 coppie, byte per byte.
  - Nessuna migrazione, nessun endpoint, nessuna chiave del nucleo, niente nel browser.
- **Che cosa deve sapere la fase dopo** (E5, la #228, in coda dopo questa):
  - **Come la usa un modulo**: un campo `static readonly ContractVersion` con i valori del suo contratto — **un'intestazione sua**, non
    quella dell'agente (Carmine, punto 9 sulla #228), la versione corrente, le accettate, **la chiave del titolo con il namespace del
    modulo**, nel file delle parole del modulo in ogni lingua, e il suo `code` — e `.AddEndpointFilter(contratto.RequireAsync)` sugli
    endpoint del contratto. La policy del token risponde prima del filtro (401, 403): l'autorizzazione è un middleware, il filtro gira
    dentro l'endpoint.
  - ⚠️ **Il titolo è del modulo**: il nucleo non aggiunge parole. Senza la chiave nel file del modulo il 400 ha per titolo la chiave
    stessa, e `ArchitectureTests.AModuleKeyIsAskedWithItsNamespaceOnTheServer` vuole una chiave con il namespace che il file del modulo
    dichiari.
  - ⚠️ **Un endpoint aperto** che dice il contratto prima che il programma parli una versione (i tour hanno `/contract`) non mette il
    filtro e scrive da sé `Header` con `Current`: il nucleo non ha un `Announce` (nota §3). L'esportazione di E5 non ne ha uno.
  - **Il documento pubblico** del contratto è del modulo, come `docs/agent-contract.md` dei tour: l'intestazione, il 400 con `code`,
    `current` e `accepted`, la regola delle versioni (dentro una versione solo aggiunte; una rottura è la versione dopo, accettata
    accanto alla vecchia per almeno un rilascio).
  - ⚠️ **La copia dei tour c'è ancora** (`AgentContract.RequireVersionAsync`): la toglie una sessione di Carmine dopo l'unione di questa
    PR (la richiesta sulla #230, nota §5). Fino ad allora il test gemello tiene le due copie uguali; con il passaggio se ne va anche lui.

### Che cosa ha lasciato E4 (6 ottobre 2026, branch `m4/e4-public-and-routes`, PR #223, in coda dopo la #221)

- **Che cosa c'è** (il dettaglio in `10`, E4, «Com'è andata»; una migrazione additiva, `AddEventRoutes`; nessun file del nucleo oltre ai
  tre test condivisi che la fase deve toccare; quattro note nuove, tre decise da Carmine sulla #223 — [la risposta][ok223]):
  - **Le rotte** (`evt_routes`, `EventRoute`, design §1.4): da uno scalo a un altro — mai a sé stesso, `events:errors.routeToItself`
    (la revisione della #223) —, la rotta da inserire (obbligatoria, 1024 caratteri, non tradotta) e le note (`remarks_i18n`, tradotte:
    scritte in una lingua, in tutte quelle della divisione; 500 per lingua). Riga `IEventChild` nell'area **`EventRoutes`**
    (`[PermissionArea]`, `[Audited]`, lo scope dell'evento); CRUD `/api/events/routes` (`EventRoutes.View`/`.Edit`, `filter[eventId]`,
    l'evento adottato in `BeforeAuthorize`, i due scali conosciuti dal nucleo; più rotte fra gli stessi due scali ammesse). Il FOD, che
    tiene l'area sull'ED per il grant della sua posizione, scrive le rotte di ogni evento e non l'evento. **Le rotte stanno nell'ordine in
    cui il FOD le scrive** (l'`Id`), sulla pagina e nella scheda del back office, dove si riordinano per scalo a mano: l'ordine per
    partenza metteva il ritorno prima dell'andata (dal banco). **Eliminare un evento** porta via le sue rotte nello stesso salvataggio,
    con l'audit, come gli scali.
  - **La pagina** `/events/{slug}` (`GET /api/events/public/{slug}`, anonima, `Public/PublicEvents.cs`): banner, stato e tipo, quando in
    UTC e nell'ora della divisione, chi organizza (con la sua pagina), gli scali e le rotte con il nome che il nucleo conosce, la nota di
    un annullato, la descrizione. La legge chi è il pubblico dell'evento **mentre si vede** (`EventState.IsSeen` sul filtro globale: un
    evento `Members` solo a chi è entrato); **lo staff degli eventi** (`Events.View` sulla riga, chiesto all'unico handler) **in ogni
    stato**, con una riga che dice che nessun altro la vede e perché — `unseen` del DTO, da `EventState.Unseen`: bozza, non ancora
    visibile, concluso; lo stato da solo non distingue un annullato non ancora visibile da uno concluso —, più il link al back office;
    tutti gli altri **404**.
  - **Il blocco `events.eventList`** (sempre vivo, le due metà; `EventListProvider`, proprietà `kinds` e `limit`): gli eventi che si
    vedono, in arrivo e in corso, annullati compresi fino alla fine, dal più vicino. **`/events` lo legge** come `/calendar` legge il blocco
    del calendario, quindi la lista non ha un endpoint suo: schede (`screens/EventCards.tsx`, le stesse del blocco, con gli scali per
    codice e nome come la pagina: `AirportName`), filtri per tipo e per scalo nell'indirizzo (le scelte sono quelle che le schede hanno;
    lo scalo si sceglie per codice e nome, e l'indirizzo tiene il codice), il `CalendarView` sugli stessi eventi meno gli annullati. ⚠️ **Al
    più 50**: `/events` chiede al blocco `limit: 0`, e il server dà a una lista al più `PublicEvents.MaxItems` (50, il limite di una lista
    dei blocchi del nucleo, `DataBlockScope.MaxItems`), i più vicini. Una divisione che annunciasse più di cinquanta eventi insieme
    vedrebbe i cinquanta più vicini, con i filtri e il calendario su quelli; gli altri entrano man mano che i primi finiscono, e
    nessuna riga dice che ce ne sono altri. Oggi è lontano; se servisse, la pagina avrebbe una lettura sua, a pagine.
  - **`EventState.Seen(now)`**: `IsSeen` scritto in quello che SQL chiede, tenuto alla stessa risposta da `EventsStateTests`.
  - **La scheda «Rotte»** della pagina dell'evento nel back office (lista e form generati, `/staff/events/{id}/routes/{routeId}`), a chi
    legge l'area, con «Nuova rotta» a chi la scrive; e il link «La sua pagina sul sito».
  - **«Pubblica» aspetta le impostazioni salvate** (l'⚠️ che E3b lasciava a E4): spento, con una riga che dice perché, finché il form ha
    modifiche non salvate.
  - **I tipi di un evento sono quelli degli eventi** (nota `2026-10-06-i-tipi-che-un-evento-sceglie`, decisa da Carmine): quelli con una
    riga in `kindPresets`, e tutti i tipi del calendario finché non ce n'è nessuna. Il form li offre (`eventKinds` in `schemas.ts`), il
    server rifiuta gli altri su un evento nuovo o al cambio di tipo (`events:errors.kindNotOfEvents`, in `EventSaving.PrepareAsync`), e un
    evento già scritto tiene il suo tipo anche se la riga va via. ⚠️ Sul banco e sulla prova la scelta resta com'è finché le impostazioni
    degli eventi non hanno righe: per IT le scrive la divisione (RFE, RFO, MSE, Online Day e l'evento libero, con tutto spento).
  - **I test**: `EventsPublicTests` (integrazione, 4, VID 761033–761036, scali `XEC1`/`XEC2`, slug `evt-test-e4-…`), `EventsStateTests`
    (+2), un test dei tipi in `EventsStaffTests`, `screens/cards.test.ts`, `schemas.test.ts` ed `eventForm.test.ts` (vitest), la smoke
    `web/e2e/events-public.spec.ts` (8), il giro `web/e2e/full/events-public.spec.ts` (1), un terzo test in
    `web/e2e/full/events-staff.spec.ts` e, nel primo, i tipi che il form offre. `EventsTestRows` toglie anche le rotte.
- **Che cosa deve sapere la fase dopo**:
  - ⚠️ **Con E4 unita dopo la #221 cade l'⚠️ di E3b** («nessuna consegna e nessun «Pubblica» sulla prova fra E3b ed E4»): la voce di
    calendario e la riga di ricerca trovano la loro pagina. Quando consegnare resta di Carmine.
  - **Le tre note di E4 sono decise** da Carmine ([la risposta sulla #223][ok223], 6 ottobre 2026, autore `SkyMistery`, pubblicata dal
    master su sua istruzione): `2026-10-06-il-pubblico-degli-eventi` (sì alle cinque letture, e **la seconda lettura scritta a mano**,
    `GET /api/events/public/{slug}`, accettata come quella dei preset: uno scostamento dichiarato dal design §7.2, contato per il piano
    §16.6 — nella nota, §4), `2026-10-06-chi-e-online-sugli-scali-di-un-evento` (la `LiveStatusStrip` sugli scali dell'evento, design §7.1,
    **non è di E4**: è la fase del nucleo **E4b**, in una PR sua con la sua nota; non è urgente, può venire dopo E5, e la monta la prima
    fase del modulo dopo di lei) e `2026-10-06-i-tipi-che-un-evento-sceglie` (come raccomandato, fatta su questa PR: sopra).
  - **`search.kinds.events`** (la parola del tipo `events` nella ricerca) è arrivata con la #222 del nucleo, e il merge di `main` su questo
    branch l'ha tenuta, senza conflitti ([i rilievi sulla #223][r223], punto 3).
  - **E5** (gli slot pubblici) è **in coda sul branch di E4**: la sessione che coordina la prepara dalla testa `94ca28b` di E4, e unisce
    l'ultima spinta di E4 — con il merge di `main`, i tipi degli eventi e le risposte di Carmine. La lista pubblica degli slot va sulla
    pagina — `PublicEvents.ReadAsync` e `PublicEventDto`, oppure una lettura sua accanto —; la scheda «Slot» accanto a «Rotte»;
    `EventSlot` è `IEventChild` come le rotte. E5 non aggiunge blocchi.
  - ⚠️ **`EventSaving.DeleteAsync` elimina le righe figlie di ogni area** (scali, rotte): una fase che aggiunge una tabella figlia (slot E5,
    postazioni E11a, regole di award E14b) la aggiunge lì, e il guardiano chiederà a chi elimina l'`Edit` di quell'area — EC ed EAC tengono
    tutte le aree tranne `EventReports.Edit` (§6.2), che non ha righe figlie dell'evento.
  - **E6b** (il promemoria con la rotta del FOD) e **E8b** («Duplica» copia le rotte) trovano le rotte per evento; fra gli stessi due scali
    ce ne può essere più d'una, e le note dicono quale. L'ordine è quello di scrittura (l'`Id`): «Duplica» le copia in quell'ordine, o
    la copia le mostra in un altro.
  - ⚠️ **`EventsArchitectureTests` vieta fra apici una chiave seminata del calendario anche fuori da un tipo** — una chiave di query
    `'event'` è caduta al primo giro (ora `'page'`) — **e un ICAO di quattro maiuscole**: gli esempi della galleria usano `XX01`, `XX02` e il
    tipo `gallery`.
  - **`/events` non è nel menu pubblico**: si aggiunge dall'editor del menu, come `/tours` e `/training` (un dato, non codice).
  - ⚠️ Lo snapshot del modello degli eventi (`EventsDbContextModelSnapshot`) ha preso con `AddEventRoutes` anche la colonna `notified_at`
    di `cms_award_signals` (E10d), una tabella del nucleo mappata ed esclusa dalle migrazioni del modulo: nessun SQL, solo il modello al
    passo. La prossima migrazione degli eventi non la ritrova.

[r223]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/223#issuecomment-6014660539
[ok223]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/223#issuecomment-6017107039

### Che cosa ha lasciato E3b (5 ottobre 2026, branch `m4/e3b-event-life`, PR #221, senza coda)

- **Che cosa c'è** (il dettaglio in `10`, E3b, «Com'è andata»; nessuna migrazione, nessun file del nucleo; dopo la revisione una nota,
  `decisions/2026-10-06-le-regole-di-pubblica.md`, decisa da Carmine: le tre regole qui sotto — pubblicabile dopo «Pubblica»,
  `bookingOpensAtUtc` per un evento con slot, il tipo `events` nella ricerca — e la scelta sui file di una bozza):
  - **«Pubblica»** (`POST /api/events/events/{id}/publish`, `Events.Edit` sulla riga): i controlli in una classe sola, `EventPublishing`
    (`src/IvaoHub.Modules.Events/Staff/EventPublishing.cs`), con i `Refusals` campo per campo — titolo e riassunto in ogni lingua, le date
    in ordine (visibile ≤ prenotazioni ≤ inizio < fine), per un evento con slot l'apertura delle prenotazioni e uno scalo (campo
    `airports`), il link per un evento di altri. Una volta; mai su un annullato; 409 su una versione vecchia.
  - **Un evento pubblicato resta pubblicabile**: `EventSaving` rifiuta una modifica con le chiavi di «Pubblica», e l'ultimo scalo di un
    evento pubblicato con slot non si elimina.
  - **La proiezione** (`Event.Project`, `IProjectable`): quando l'evento si vede (`EventState.IsSeen`: pubblicato, da `visible_from` o
    dalla pubblicazione alla fine, annullato compreso) una voce di calendario — tipo e visibilità dell'evento, `/events/{slug}` — se non
    è annullato, e una riga di ricerca per lingua (tipo `events`) se è per tutti; il banner e le immagini della descrizione come usi dei
    file: senza fine finché l'evento è una bozza, fino alla fine + 7 giorni quando è pubblicato.
  - **`events-release`** (`EventReleaseJob`, ogni 15 minuti): riproietta gli eventi pubblicati diventati visibili o conclusi
    dall'inizio dell'ultimo giro riuscito, come `TourReleaseJob`.
  - **`events.eventChanged`** dichiarato (mail e parola del profilo), senza destinatari.
  - **La schermata**: «Pubblica» nella barra della pagina di una bozza; i rifiuti come elenco «campo: che cosa manca» sotto la barra.
  - **I test**: `EventsLifeTests` (integrazione, 6, VID 761011, scalo `XEB1`, orologio dell'host spostato dal test), `EventsStateTests`
    (+1), un secondo test in `web/e2e/full/events-staff.spec.ts`; `EventsTestRows.ForgetAsync` toglie gli eventi di una classe con scali
    e proiezioni (lo usa anche `EventsStaffTests`).
- **Che cosa deve sapere la fase dopo**:
  - ⚠️ **Nessuna consegna e nessun «Pubblica» sull'installazione di prova fra E3b ed E4** (Carmine, 6 ottobre 2026, [sulla #221][seq221]):
    la voce di calendario e la riga di ricerca di un evento pubblicato puntano a `/events/{slug}`, che porta solo E4.
  - ⚠️ **«Pubblica» pubblica la riga salvata**: il form delle impostazioni si ridisegna sulla riga pubblicata (la sua chiave è la
    versione della riga), e una modifica non salvata si perde senza un avviso (dalla lettura del codice, non provato nel browser; il
    revisore l'ha lasciato fra i non verificati). La fase che tocca di nuovo la pagina (E4, con la scheda delle rotte) può spegnere
    «Pubblica» finché il form ha modifiche.
  - **E4** trova in `EventState.IsSeen` la regola di chi vede un evento (il pubblico: pubblicato, visibile e non concluso; annullato
    compreso, con la nota) e l'indirizzo `/events/{slug}` che calendario e ricerca usano già: la pagina pubblica deve rispondere lì.
    ⚠️ Un evento `Members` è nel calendario dei membri e mai nella ricerca (§8.1).
  - ⚠️ **Il tipo della riga di ricerca è `events`** (la chiave del modulo): `event` è una parola seminata del calendario, e
    `EventsArchitectureTests` vieta al modulo di scriverne una. Il distintivo della ricerca mostra la chiave finché `common.json` (nucleo)
    non ha `search.kinds.events`, come oggi `tour`.
  - ⚠️ **Ogni scrittura di un evento pubblicato** passa da `EventPublishing`: una fase che aggiunge un controllo a «Pubblica» lo aggiunge
    lì, e vale anche per le modifiche; una riga figlia la cui assenza rende l'evento non pubblicabile (come l'ultimo scalo) rifiuta il
    suo `Delete` allo stesso modo.
  - ⚠️ **E6a** manda `events.eventChanged` dopo il salvataggio di un evento pubblicato i cui orari cambiano (il punto è
    `EventSaving`), con i segnaposto proposti `{{event}}`, `{{starts}}`, `{{ends}}`, `{{url}}` o altri suoi.
  - ⚠️ **Gli eventi scritti prima di E3b** (banco, installazione di prova) non hanno proiezioni finché non si salvano: oggi sono bozze, e
    il banner diventa un uso alla prossima scrittura.
  - ⚠️ **Un test che sposta l'orologio** usa un host derivato (`WithWebHostBuilder` + `ConfigureTestServices` con un `IClock` suo, come
    `EventsLifeTests`), mette in pausa il job nello scheduler, toglie le righe di `hub_jobs_log` del job all'inizio e non fa richieste
    firmate mentre l'orologio è spostato.

[seq221]: https://github.com/SkyMistery/Ivao-Italy-Hub/pull/221#issuecomment-6012333670

### Che cosa ha lasciato E3a (1 ottobre 2026, branch `m4/e3a-event-staff`, PR #214, in coda dopo la #212)

- **Che cosa c'è** (il dettaglio in `10`, E3a, «Com'è andata»; nessuna migrazione; una nota, dopo la revisione:
  `decisions/2026-10-01-la-lettura-dei-preset-dei-tipi.md`, decisa da Carmine):
  - **Lo stato dalle date** in una funzione sola, `EventState.Of` (`src/IvaoHub.Modules.Events/EventState.cs`), e le cinque viste della
    lista (`EventViews`, `filter[view]`: bozze, prossimi, in corso, conclusi, annullati), scritte anche in SQL e tenute alla stessa
    risposta da `EventsStateTests`. Ogni istante è il primo momento di ciò che apre; annullato vince su tutto, bozza su ogni data.
  - **L'evento nel CRUD** (`/api/events/events`, `src/IvaoHub.Modules.Events/Staff/`): `Events.View`/`Edit`, `DeletePolicy`
    `Events.Delete`; `EventSaving` (indirizzo libero, tipo del calendario e attivo quando lo si sceglie, niente scali su un evento di
    tutta la divisione; eliminare porta via gli scali); **gli scali** (`/api/events/airports`, `EventAirportEndpoints`): righe
    `IEventChild` che adottano dipartimento e maschera dell'evento in `BeforeAuthorize` (`EventChildren.AdoptAsync`), un aeroporto del
    nucleo una volta, la capacità in movimenti oppure in arrivi e partenze.
  - **Due endpoint a mano**: **annulla** (`POST /{id}/cancel`, `Events.Edit` sulla riga: quando, chi, la nota in ogni lingua; una volta;
    mai su un evento concluso; 409 su una versione vecchia) e **i preset dei tipi** (`GET /api/events/kind-presets`, `Events.Edit`):
    il form li legge per preimpostare gli interruttori al cambio del tipo, perché le impostazioni le legge solo chi le gestisce.
  - **Le schermate**: `/staff/events` (lista con il filtro delle viste), `/staff/events/{id}` (impostazioni, descrizione con l'editor
    dei blocchi, scali; «Annulla l'evento» ed «Elimina» a chi il server lo permette), `/staff/events/{id}/cancel`,
    `/staff/events/{id}/airports/{airportId}`. Il tipo di notifica **`events.eventCancelled`** è dichiarato, senza destinatari.
  - **I nove grant di chi collabora** (AOD, FOD, MD) nei due file della divisione, e `EventsArchitectureTests` che li vuole.
  - **I test**: `EventsStateTests` (unità, 6), `EventsStaffTests` (integrazione, 7, VID 761006–761010, aeroporti `XEA1`/`XEA2`),
    `eventForm.test.ts` (vitest, 4), `web/e2e/full/events-staff.spec.ts` (il personaggio dell'ED: un RFO con due scali creato,
    riaperto, annullato; una bozza vuota eliminata).
- **Che cosa deve sapere la fase dopo**:
  - **E3b** trova pronti gli stati che «Pubblica» accende (`Scheduled`, `Announced`, `BookingOpen` vengono da `Status == Published` e
    dalle date) e le viste; «Pubblica» è un verbo accanto ad «Annulla» in `EventEndpoints`, con i `Refusals` del design §2.2.
    ⚠️ Sul banco oggi ci sono solo bozze: le viste «prossimi», «in corso», «conclusi» le prova l'integrazione con righe scritte sul
    database.
  - ⚠️ **`SchemaForm` disegna i default una volta**: il form dell'evento si ridisegna (chiave nuova, stessi valori) quando cambia il
    tipo; l'indirizzo proposto dal titolo smette di seguirlo dopo il ridisegno se il titolo c'era già.
  - ⚠️ **E11a ed E16** portano `has_roster` e `in_person` nel form e anche in `presetSwitches`, `EventWriteDto` ed `EventMapper.Apply`:
    oggi il form ne applica tre.
  - ⚠️ **Ogni riga figlia nuova** (rotte E4, slot E5, postazioni E11a, regole di award E14b) implementa `IEventChild` e adotta l'evento
    con `EventChildren.AdoptAsync`; nel browser `writableDepartments` non offre l'ED a chi collabora (E2b): la riga si crea sotto
    l'evento. Chi collabora vede oggi la pagina dell'evento con il form senza «Salva» e gli scali.
  - ⚠️ **E6a** mette il primo rifiuto di «Elimina» in `EventSaving.DeleteAsync` e i destinatari di `events.eventCancelled` dopo il
    salvataggio in `CancelAsync`; la fase che manda la prima di quelle mail **rimette sulla pagina «Annulla»** la frase su chi la riceve
    (`events:cancel.description` e l'aiuto della nota), tolta dopo la revisione della #214 perché oggi non parte nessuna mail.
  - ⚠️ **Il banner non è ancora un uso di un file** (`Event` non è `IProjectable`): fino a E3b un banner si può eliminare dalla libreria
    mentre un evento lo mostra (rilievo 4 del revisore sulla #214). E3b subito dopo.
  - **La lettura dei preset** (`GET /api/events/kind-presets`) **resta com'è**: Carmine l'ha accettata il 1 ottobre 2026
    ([la sua risposta sulla #214](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/214#issuecomment-5934118725)), uno scostamento dal
    design §7.2 («gli endpoint a mano sono verbi») scritto nella nota `decisions/2026-10-01-la-lettura-dei-preset-dei-tipi.md`; nessun
    permesso di lettura in `ModuleSettingsDescriptor`. ⚠️ Una fase che vuole un'altra impostazione del modulo nel form di chi non le
    gestisce non la aggiunge qui da sola: è un endpoint a mano nuovo, con la sua decisione (piano §16.6).
  - ⚠️ **Nel back office `PageShell` non disegna la `description`** (solo il tooltip del titolo): lo stato dell'evento sta nella `note`.
  - ⚠️ **`AirportEndpoints` è un nome del nucleo** (`Core/Ivao`): le classi del modulo si chiamano `Event…`.
  - La spec e2e scrive un preset dell'RFO nelle impostazioni del banco e lo rimette com'era, come `events-skeleton.spec.ts` con l'RFE.
  - ⚠️ **Il tipo di un evento si sceglie fra tutte le parole del calendario**, anche training, esame, tour, riunione e scadenza (come i
    preset di E2): nessun tipo del calendario dice di essere di un evento, e il codice non ne conosce nessuno. Un elenco più stretto
    sarebbe una domanda a Carmine e un segno del nucleo, non una lista nel modulo.

### Che cosa ha lasciato E2b (1 ottobre 2026, branch `m4/e2b-grant-without-department`, PR #212, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-10-01-il-permesso-non-il-dipartimento.md`, **decisa da Carmine** il 30 settembre, in chat al
  master e pubblicata su sua istruzione [sulla #209](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/209#issuecomment-5917066144)):
  - **Un grant a una posizione su un dipartimento che non è il suo** — su un altro, o su tutti — e **un grant al team di un FIR** con
    `firStaffScope: all` danno **il permesso, non il dipartimento**: `EffectivePermission.FromOutside`, scritto dal calcolo
    (`UserGrant.GivesThePermissionNotTheDepartment`) e portato nel claim da un `!` in testa al pezzo dello scope (`Events.View:ED@!`), che un
    lettore che non lo conosce legge chiuso.
  - **Chi lo tiene** non ha il claim `dept` di quel dipartimento (`HubClaims.BuildIdentity`): nessuna riga `Visibility.Department`, nessun
    gruppo nella barra dello staff, nessun'altra lista di quel dipartimento. **La lista generata** che legge con quel permesso ne tiene le
    righe (`TryNarrowToDepartments`, accanto a `onTheirFir`); **l'unico handler e il guardiano** lo tengono sulle righe del dipartimento come
    ogni permesso, e non sono cambiati.
  - **Un grant a una persona** fa ancora entrare nel dipartimento (6 settembre); **lo stesso permesso da fuori e per nome** resta quello
    per nome.
  - **I test**: `PermissionFromOutsideRulesTests` (unità, 8) e `PermissionFromOutsideTests` (integrazione, 4, VID 761091–761094; 761090 è
    un'identità delle unità; 761095–099 restano di E2b per le sue correzioni). **Le parole**: l'aiuto del form dei permessi
    (`grants.formHint`), `docs/FORKING.md` e i commenti di `config/division.example.json`. Nessuna migrazione, nessun endpoint.
  - **Tenuto da fuori su tutti i dipartimenti** (un grant a una posizione senza `scope`), un permesso di lettura dà nella sua lista le
    righe di ogni dipartimento, come i claim `dept` di tutti prima di E2b, e niente altro di loro; un divieto su un dipartimento accanto
    toglie quel dipartimento (dopo la revisione della #212).
- **Che cosa deve sapere la fase dopo**:
  - ⚠️ **I nove grant di chi collabora non ci sono**, nemmeno dopo l'unione di E2 (#209) in questo branch: li porta **E3a** (`10`, E3a
    punto 5), la cui sessione nasce sopra E2b, in coda dopo la #212, in `config/division.json` e in `config/division.example.json`, e cambia
    `EventsArchitectureTests` di E2, che li rifiuta finché la nota non ha risposta — ora ce l'ha. **Misurato** sopra E2b con E2 e i nove
    grant: integrazione intera 443/443, i tre test del maintainer verdi senza toccarli; fra le unità l'unico rosso è quel test di E2.
  - **Che cosa vedranno AOD, FOD e MD** (nota §3.7): gli eventi e le liste della loro area, con le righe dell'ED, ogni riga con l'unico
    handler, la sezione «Eventi»; non le righe che l'ED tiene per sé, non il gruppo dell'ED nella barra, non i suoi contatti né i suoi
    contenuti.
  - ⚠️ **`writableDepartments`** nel browser non offre il dipartimento di un permesso da fuori: una schermata che fa creare una riga a chi
    collabora (una rotta del FOD) la crea sotto l'evento, con maschera e scope dell'evento, e chiede al server le `actions`.
  - ⚠️ **Un test che prova che cosa può chi collabora** entra come una posizione di quel dipartimento, senza indirizzo, e mai come il web
    master del banco, che raggiunge ogni dipartimento; dopo aver scritto un grant rifà l'ingresso.
  - **E10f (#213) e E2b sono riconciliate in questo branch** (E10f è entrata prima): **un solo `EffectivePermission.FromOutside`** con le
    due vie nel suo `<param>`, una condizione sola nel calcolo — `fir is null && (grant.GivesThePermissionNotTheDepartment ||
    catalogue.IsGlobal(grant.Value))` —, un solo `.ThenBy(FromOutside)`, una sola riga in `BuildIdentity` con i due commenti, e le due
    aggiunte in `docs/FORKING.md` e in `config/division.example.json`. ⚠️ **Il segno ora viaggia anche per i globali**: chi assegna gli
    award per grant ha nel cookie `Awards.Assign@!` e `Awards.View@!`. Ogni riga di `Award` resta aperta: il catalogo è condiviso in
    lettura (`Award` è `ISharedForReading`, la sua lista dice `SharedForReading = award => true`) e l'unico handler risponde a una lettura
    con `HasAny(Awards.View)`, che non guarda né il dipartimento né il segno; e `Awards.View@!` si rilegge senza dipartimento, cioè su
    tutti (`10`, E2b, «Com'è andata»).
  - **Il team di un FIR con `own`** (IT) non cambia: il suo permesso porta il FIR, e il suo claim è quello di prima. ⚠️ Un cookie scritto
    prima di E2b tiene i suoi claim `dept` fino al prossimo ingresso; su IT nessuno ne ha uno da un grant a una posizione su un altro
    dipartimento.

### Che cosa ha lasciato E10c (1° ottobre 2026, branch `m4/e10c-ratings-and-positions`, PR #204, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-09-30-il-rating-preferito-e-il-minimo-di-una-postazione.md`, **decisa da Carmine**, in chat
  al master e pubblicata su sua istruzione [sulla #204](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/204#issuecomment-5916282164)),
  tutto in `src/IvaoHub.Core/Ivao/`:
  - **il rating preferito** per un tipo di postazione: **la regola è della divisione**, `config/division.json → preferredAtcRatings`
    (tipo di postazione → sigla del rating ATC; [seconda risposta di Carmine](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/204#issuecomment-5926811688)),
    controllata all'avvio (`PreferredAtcRatingsValidator`: un tipo o un rating sconosciuti fermano l'hub con un messaggio); il
    nucleo risponde `RatingVocabulary.PreferredFor(tipo)` dal vocabolario che registra (`IvaoRatings.WithPreferred`). I valori di IT
    sono di Carmine: **AS3** su `DEL`; **ADC** su `FSS`, `GND`, `TWR`; **APC** su `APP`, `DEP`; **ACC** su `CTR`; **nessuno** su `ATIS`.
    «Ha il preferito» è `IsAtLeast(Atc, rating, PreferredFor(tipo).Number)`; `null` = nessuno è preferito su quel tipo;
  - **le postazioni della divisione**: `IAtcPositionDirectory.OfDivisionAsync()` (tutte, militari e ATIS compresi) e
    `FindAsync(nominativi)` (a lotti, per nominativo in qualunque maiuscola, come `IAirportDirectory.FindAsync`); **`AtcPositionDto`
    porta il tipo** (`Type`), accanto a nominativo, nome, aeroporto e **FIR** (per una postazione d'aeroporto, quello del suo aeroporto);
  - **il minimo di una postazione è il suo FRA su IVAO**: `MinimaAsync(nominativi)` → un `AtcPositionMinimum` per nominativo, e
    `Over(da, a)` dà il **numero di IVAO** del rating più alto fra gli FRA attivi che valgono in qualche momento del turno (giorno e
    notte, feriali e fine settimana, una data, oltre la mezzanotte); `null` = nessun minimo. Gli FRA stanno in **`ref_ivao_fras`**
    (migrazione del nucleo `AddIvaoFras`, dopo quella di E10d), rinfrescati **ogni notte** con i dati di riferimento, solo le righe per
    postazione (**nessuna persona**); **senza risposta di IVAO restano, con una risposta vuota se ne vanno** (la divisione li ha tolti
    tutti: il rilievo 2 del revisore); il conteggio, o «nessuna risposta», è nel messaggio del giro, non nel suo esito;
  - **le parole dell'avviso** sotto il minimo — togli l'FRA su IVAO per quel controllore —: la chiave del nucleo
    `atcPositions.belowMinimum` (`AtcPositionMinimum.BelowMinimumKey`), perché nominano IVAO;
  - **le misure**: `tools/record-ivao-fixtures.mjs --fras <paese> [ICAO…]`, e la fixture `tests/fixtures/ivao/fras-IT.json` (94 FRA
    delle postazioni del banco, 30 settembre 2026);
  - **i test**: `RatingVocabularyTests`, `IvaoFraReaderTests`, `AtcPositionMinimumTests` e `PreferredAtcRatingsValidatorTests`
    (unità), `AtcPositionTests` (integrazione).
- **Che cosa deve sapere la fase dopo**:
  - **E11a**: la scelta delle postazioni con `OfDivisionAsync()`, il controllo del nominativo e la copia del FIR con `FindAsync`; il
    tipo non va salvato sulla riga (lo sa la directory).
  - **E11b**: «chi può» = almeno `minimumAtcRating` **e** almeno `minima[callsign].Over(turno.da, turno.a)` quando non è `null`, sempre
    con `IsAtLeast` (un numero che il vocabolario non conosce non lo raggiunge nessuno); il proponente **non va mai sotto**. «Chi prima»
    = `PreferredFor(position.Type)`. **La correzione a mano può andare sotto l'FRA**: è un **avviso**, mai un rifiuto, con le parole di
    `atcPositions.belowMinimum` (decisione di Carmine sulla #204); lo stesso per la cessione di un turno (E12).
  - ⚠️ **Il minimo è un numero di IVAO** (`int?`), non un `Rating`: confrontalo con `IsAtLeast`, mai con `>=`.
  - ⚠️ **`IvaoRatings.Vocabulary` (statico) è la scala di IVAO da sola**, senza la regola della divisione: `PreferredFor` vi risponde
    sempre `null`. Il modulo prende il `RatingVocabulary` dal contenitore, che la porta; i suoi test costruiscono un vocabolario di
    prova con la loro mappa (`new RatingVocabulary(ratings, preferred)`).
  - ⚠️ **Gli orari degli FRA si leggono UTC**, come ogni orario di IVAO: non verificato (nessuna fonte lo dice).
  - ⚠️ **Le eccezioni per membro restano su IVAO**: un controllore che lo staff ha già sbloccato su IVAO resta «sotto il minimo» per
    l'hub, e l'avviso compare lo stesso.
  - ⚠️ **L'ordine dei nominativi è quello del database** (`utf8mb4_unicode_ci`: `_` prima delle cifre, `LIRR_NE_CTR` prima di
    `LIRR_NE1_CTR`): un test non lo confronti con un ordinamento `Ordinal`.
  - ⚠️ **E15a ed E10a** toccavano gli stessi file del client di IVAO (`IIvaoApiClient.cs`, `IvaoApiClient.cs`,
    `FixtureIvaoApiClient.cs`), lo strumento delle fixture e il suo README: entrate in E10c con due merge il 1° ottobre, lo strumento ha
    ora le quattro modalità di riferimento (`--positions`, `--bookings`, `--fras`, `--sessions-at`) e il README tutte le sezioni. **E10c
    migra il contesto del nucleo**: se un'altra fase del nucleo lo migra insieme, la seconda unita rifà la sua migrazione sopra `main`
    (come ha fatto E10c dopo E10d).

### Che cosa ha lasciato E10f (1 ottobre 2026, branch `m4/e10f-grantable-award-assign`, PR #213, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-10-01-chi-assegna-gli-award-con-un-grant.md`, **decisa da Carmine**, in chat al master e
  pubblicata su sua istruzione [sulla #205](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/205#issuecomment-5916282643), risposta 2):
  - **`Awards.Assign` si dà con un grant, ed è il solo globale così**: il campo `PermissionDescriptor.GrantableAlthoughGlobal`, detto sul
    permesso come `DeniedToStakeholder`, vero solo per `Awards.Assign` in `CorePermissions`. Il catalogo risponde con
    `IsClosedToGrants(name)`, la domanda che fanno il calcolatore, la schermata dei permessi e il seme; e **non nasce** se lo dicesse un
    altro permesso (`Permissions.Manage` per primo, ma anche un altro globale, un permesso di un dipartimento, un globale di un modulo:
    il paletto che il revisore proponeva sulla #213, preso).
  - **Solo intero**: il calcolatore lo tiene (su nessun dipartimento, come un ruolo) solo da un grant senza dipartimento, senza scope e
    non al team di un FIR; un rifiuto intero lo toglie anche a chi lo ha per ruolo (DIR, ADIR, WM, AWM). La schermata rifiuta il dipartimento
    (**`errors.grant.globalDepartment`**, chiave nuova), il team di un FIR (`firTeamArea`, com'era) e gli altri globali
    (`globalPermission`, con un testo nuovo); il seme salta uno `scope`. **`ModuleGrants` rifiuta ancora ogni globale.**
  - **Il permesso, non il dipartimento**: `EffectivePermission.FromOutside`, vero per un grant di un permesso globale e per la
    `Awards.View` che porta; `HubClaims.BuildIdentity` lo lascia fuori dai claim `dept`. Senza, un grant senza dipartimento mette chi lo
    tiene **dentro tutti i dipartimenti** (l'ha trovato la sessione di E2b; misurato: `user.departments` del coordinatore dell'MD
    passava da `["MD"]` a tutti e nove).
  - **La divisione**: `config/division.json` dà `Awards.Assign` al **coordinatore e all'assistente dell'MD** (primo dei `positionGrants`,
    senza `scope`); lo stesso in `config/division.example.json`, e `docs/FORKING.md` spiega l'eccezione.
  - **Il browser**: `/api/me → registries.permissions[].grantableAlthoughGlobal`; la schermata dei permessi offre `Awards.Assign`.
  - **I test**: `GrantableGlobalPermissionTests` (unità, 17), `AwardsAssignByGrantTests` (integrazione, 4, VID 761080–761083),
    `web/src/features/admin/grants/grantable.test.ts` (2); **tolto il caso `Awards.Assign`** da
    `EffectivePermissionsTests.AGrantCanNeverConferAGlobalPermission` (test di Carmine: il cambio l'ha deciso lui).
- **Che cosa deve sapere la fase dopo**:
  - **Il riepilogo di E10d arriva all'MD da solo**, dal primo avvio dopo il rilascio: il seme nuovo si applica una volta, poi si cambia
    dalla schermata dei permessi. **E14b** non cambia niente.
  - ⚠️ **Nei test d'integrazione l'MD ha `Awards.Assign`**, perché l'host legge `config/division.json`: un coordinatore o un assistente
    dell'MD seminato da un test (i contatti seminano `IT-MC` con un indirizzo) è fra chi assegna. `AwardQueueMailTests` conta per le sue
    persone e non se ne accorge; un test nuovo che conta i destinatari del riepilogo faccia lo stesso.
  - **Le tre scelte della sessione sono confermate da Carmine**
    ([sulla #213](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/213#issuecomment-5926811970)): coordinatore e assistente dell'MD; il
    rifiuto intero che vale anche per chi lo ha per ruolo; la `Awards.View` su ogni dipartimento, perché chi assegna veda ogni award.
  - ⚠️ **`FromOutside` è lo stesso campo di E2b** (PR #212, decisa sulla #209: un grant a una posizione su un dipartimento non suo dà il permesso
    e non il dipartimento), con la stessa forma: ultimo parametro di `EffectivePermission`, la stessa preferenza nella deduplicazione, la
    stessa riga in `BuildIdentity`. **Il significato non è identico** (il revisore sulla #213): in E2b il segno scrive anche un `!` nel
    cookie, e con tutte e due unite la `Awards.View` portata da `Awards.Assign` si scrive `Awards.View@!`. **Chi arriva seconda su `main`**
    tiene una dichiarazione sola, somma le due condizioni del calcolatore e rifà sul codice unito `AwardsAssignByGrantTests`,
    `GrantableGlobalPermissionTests` e le due classi di E2b; il revisore controllerà che quella `Awards.View` apra ancora ogni riga di
    `Award`. E2b porta anche la metà «liste»: fino ad allora **chi non è dentro nessun dipartimento** (il capo di un FIR a cui si desse
    `Awards.Assign` con un grant a un VID) apre la coda ma ha la lista degli award a 403. L'MD non ne è toccato.
  - Un **permesso globale di un modulo** resta chiuso ai grant: dire `GrantableAlthoughGlobal` ferma l'avvio. Un secondo permesso
    concedibile è una decisione con la sua nota, che cambia anche il paletto del catalogo.

### Che cosa ha lasciato E10a (30 settembre – 1° ottobre 2026, branch `m4/e10a-tracker-without-vid`, PR #210, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-09-30-il-tracker-senza-vid.md`, scelta tecnica; le misure del 30 settembre sono lì, §2, e
  la revisione in §6):
  - **La domanda senza VID**: `IvaoSessionQuery(int? Vid, from, to, departure, arrival, IvaoConnectionType? ConnectionType)` con
    `Limit` (`init`, predefinito 200, **al più `MaxLimit` = 1000**, un tetto che tiene il nucleo); `SearchSessionsAsync` è la stessa
    di prima, dalla più recente, senza doppioni, al più `Limit`, `null` quando IVAO non si è potuto chiedere;
    `IvaoTrackerSessionDto.ConnectionType`. Il giro delle pagine e la regola del tracker stanno nel lettore
    (`IvaoTrackerReader.ReadPagesAsync`, `Answers`), per il client vero e per quello delle fixture.
  - **Il client di IVAO aspetta 20 s per tentativo** (l'interruttore campiona su 40, il totale resta 30): con i 10 standard nessun
    aeroporto si leggeva. **Confermato da Carmine** con il `null` per la ricerca dei tour
    ([risposta sulla #210](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/210#issuecomment-5917033792)).
  - **Le fixture**: `tools/record-ivao-fixtures.mjs --sessions-at`; `tests/fixtures/ivao/tracker-airport-LIRF.json` (una sera di
    LIRF: 10 sessioni di 9 membri, la torre) e `tracker-pages-LIRF.json` (le stesse partenze come le pagina IVAO), **spostate sul 1°
    gennaio 2001** (lo stesso giorno delle prenotazioni di E15a) e **senza `rating`, `serverId`, `software*`** (revisione, #210).
    Senza VID, `FixtureIvaoApiClient` risponde dal file dell'aeroporto chiesto. Unita dopo E15a, lo script ha le due modalità
    (`--bookings` e `--sessions-at`) e **un giorno inventato solo**, `standIn` con `movedFrom(day)`, in cima: chi registra altre righe
    con persone lo usa.
  - **I test**: `IvaoTrackerWithoutVidTests` e `IvaoApiTimeoutTests` (unità).
- **Che cosa deve sapere la fase dopo** (E13a, e chiunque legga il tracker senza VID):
  - ⚠️ **Una domanda per aeroporto che trova qualcosa costa ~10,5 s** (la pagina con l'ultima riga, sempre), e **due insieme
    ricevono 504** dal gateway di IVAO: una alla volta, poche per giro del job.
  - ⚠️ **La finestra è sull'inizio della sessione**, estremi compresi: per chi era già connesso, `FromUtc` va allargato.
  - ⚠️ **«Partenza o arrivo» sono due domande**: chiesti insieme, i due aeroporti vogliono la stessa revisione del piano. Una
    sessione può tornare da tutte e due: si conta per `Id`.
  - ⚠️ **Il DTO dice gli aeroporti della prima revisione**: trovata per la partenza da LIRF, una sessione può dire LIPZ.
  - **Il limite si dichiara**: con esattamente `Limit` sessioni la risposta può essere tagliata, e il resto sta prima della più
    vecchia.
  - **Le VID 761020–761028 sono le persone della sera di LIRF** nella fixture (761025 due volte), e **la sera sta sul 1° gennaio
    2001**, 16:00–17:59:59 UTC: un test che la legge chiede quel giorno. 761029 non è usata.
  - Il punto «La richiesta al tracker oggi» di «Per chi prende M4», qui sopra (`PageSize = 50`, `MaxSessions = 200`), è superato:
    le pagine sono da 100 e il tetto è il `Limit` di chi chiama, al più 1000.
- ⚠️ **Per i tour cambiano due cose, nessuna nei loro test**: le pagine sono da 100, e se IVAO non risponde affatto la ricerca dà
  `null` («tracker non disponibile») invece di lanciare.

### Che cosa ha lasciato E15a (30 settembre 2026, branch `m4/e15a-network-atc-bookings`, PR #207, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-09-30-le-prenotazioni-atc-della-rete.md`, scelta tecnica; le misure con il token vero sono
  lì, §2; **nessuna cache e al più sette giorni per richiesta confermati da Carmine** dopo la revisione, [risposta sulla
  #207](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/207#issuecomment-5916738183)):
  - **La domanda del modulo**: `IAtcBookingSource.BookedAsync(fromUtc, toUtc, callsign?)` in `src/IvaoHub.Core/Ivao/AtcBookingSource.cs`
    (namespace `IvaoHub.Core.Ivao`, che il modulo importa come fa il training: nessun nome del file o del tipo nomina IVAO) → le
    prenotazioni della rete che **si sovrappongono** alla finestra, di tutte le postazioni o di un nominativo intero, nell'ordine in cui
    cominciano; **`null` = non disponibile**, mai «nessuno ha prenotato». `AtcBookingDto(Callsign, StartsAt, EndsAt, Vid, Kind)`, con
    `AtcBookingKind` `Controlling`, `Training`, `Exam`. Al più **`IAtcBookingSource.MaxDays` = 7** giorni di UTC (oltre: `null` e un
    avviso nel log). Alla richiesta, **nessuna cache**, nessun job.
  - **Il client**: `IIvaoApiClient.GetDailyAtcBookingsAsync(DateOnly, position?)` (`/v2/atc/bookings/daily`, il giorno di IVAO com'è;
    predefinito «non disponibile» per i doppi dei test), `IvaoApiClient` con il token dell'applicazione, `FixtureIvaoApiClient`, e **un
    lettore solo** per tutti e due, `IvaoAtcBookingReader` (`IvaoAtcBookings.cs`).
  - **Lo strumento**: `tools/record-ivao-fixtures.mjs --bookings <nome> <asVid> <yyyy-mm-dd> <prefisso…>`, con la persona tolta e
    niente che ritrovi la prenotazione attraverso l'API di IVAO: né `id` né `createdAt`, e il giorno spostato sul 1° gennaio 2001.
  - **La fixture** `tests/fixtures/ivao/atc-bookings-day.json` (VID 761070–761079; il giorno vero non è scritto da nessuna parte) e il
    suo paragrafo nel README delle fixture.
  - **I test**: `AtcBookingTests` (unità, 25 casi), provati al contrario (`10`, E15a, «Com'è andata»).
- **Che cosa deve sapere la fase dopo** (E15b):
  - **Una domanda per l'evento, non una per postazione**: `BookedAsync(inizio, fine)` senza nominativo, poi le postazioni dell'evento
    con un filtro: una chiamata a IVAO per giorno di UTC invece di una per postazione. Il nominativo della domanda, quando c'è, è
    **intero** (con `LIRR` non torna niente: per IVAO sarebbero tre settori).
  - ⚠️ **IVAO lento o giù**: prima di «non disponibile» il client aspetta quello che aspetta ogni chiamata a IVAO (i tentativi e il
    tempo massimo della resilienza, 30 secondi per chiamata), una chiamata per giorno. Carmine lo ha accettato così: la pagina aspetta e
    poi dice che le prenotazioni non sono disponibili, **mentre il resto del roster funziona** — quindi **le prenotazioni si caricano a
    parte dal roster** (una richiesta loro), e il roster non aspetta IVAO.
  - **Il VID, non il nome**: un membro dell'hub con `personName`, chi non è mai entrato con il suo numero. `Exam` e `Training` dicono
    che la postazione è occupata da un esame o da un training su IVAO: vale la pena mostrarlo.
  - **Sul banco (fixture) ogni giorno è lo stesso giorno registrato**: `LIRF_TWR` 18–20 (761079), `LIMC_TWR`, `LFPG_APP`,
    `LIMM_WS2_CTR`, `LFFF_CTR`, `LIRR_SU_CTR`, `LIRR_NC_CTR` 19–21, `LIRR_NE_CTR` 19–22, l'esame di `EDDF_APP` 18:30–20:30, `SBGR_TWR`
    23–01 (UTC). I VID
    761070–761079 non sono nessuno sul banco: la pagina mostra i numeri. Una spec che vuole vedere un controllore del roster anche fra
    le prenotazioni ha bisogno di una fixture con il VID di un personaggio del banco (il roster prende solo chi è entrato nell'hub).
  - ⚠️ **E10a tocca gli stessi file nello stesso giorno** (`IIvaoApiClient.cs`, `FixtureIvaoApiClient.cs`, lo script delle fixture):
    chi viene unita per seconda fonde `main` quando il master lo chiede (mai un rebase), e tiene tutte e due le modalità dello script.
- ⚠️ **Trappole trovate** (nota, §2): la `position` di IVAO è **il principio del nominativo** (`LI` = tutte le italiane); una
  prenotazione a cavallo della mezzanotte è nell'elenco di **tutti e due** i giorni, una che finisce alle 00:00 anche del giorno dopo;
  `date` con un'ora che non è 00:00 dà un elenco vuoto; `user` porta anche `rating`, che la documentazione non dice. ⚠️ **Una
  prenotazione registrata ritrova la persona** con l'API di IVAO dal suo `id`, e anche dal suo giorno con il nominativo (rilievo del
  revisore): chi registra altre prenotazioni lo fa con lo strumento, che toglie tutti e due. La prima registrazione, con quei dati, resta
  nella storia del branch (`fbb11ac`): fuori da `main` la tiene solo un merge a squash, che decide il maintainer.

### Che cosa ha lasciato E2 (30 settembre – 1 ottobre 2026, branch `m4/e2-events-skeleton`, PR #209, la prima fase del modulo)

- **Che cosa c'è** (il dettaglio in `10`, E2, «Com'è andata»):
  - **Il modulo** `src/IvaoHub.Modules.Events/` (referenzia solo il nucleo), registrato **per primo** in `IvaoHub.Web/Modules.cs` e in
    `web/src/modules/index.ts` (eventi, tour, training: la sezione Eventi per prima nel back office, **confermata da Carmine**
    [sulla #209](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/209#issuecomment-5917066144)); `EventsDbContext` con
    `__EFMigrationsHistory_events` e la migrazione
    **`Initial`** (`20260930170049_Initial`), che **non si tocca più**: `evt_events` **intera** e `evt_event_airports`.
  - **Le entità** alla radice del progetto: `Event` (maschera, `IVisible`, `IPublishable`, audit, `[Audited]`, scope
    `events:event:{id}` con `Event.ScopeOf`) ed `EventAirport` (maschera, audit, lo scope del suo evento). Nessun endpoint, nessuna
    proiezione, nessun validatore del CRUD ancora.
  - **Il catalogo** `EventsPermissions` (12 permessi, 5 aree, `DeniedToStakeholder` su `EventAtc.Edit` ed `EventReports.Edit`) e **i
    grant** dell'ED (11) e del team di un FIR (2, su `EventAtc.*`) nei due file della divisione.
  - **Le impostazioni** `EventsSettings` (`Settings/EventsSettings.cs`: `kindPresets`, `bookingGapMinutes`, `pilotRetentionMonths`,
    `reminderLeadHours`) con i validatori, e la schermata generata `/staff/events/settings` (`web/src/modules/events/`), l'unica voce
    della sezione «Eventi» del back office.
  - **Il segmento riservato** `events`; le parole `events` in `web/src/modules/events/locales/` (e le copie in `locales/`).
  - **I test**: `EventsArchitectureTests` ed `EventsSettingsTests` (unità), `EventsSkeletonTests` ed `EventsXxDivisionTests`
    (integrazione, VID 761001–761005), `web/src/modules/events/schemas.test.ts`, `web/e2e/full/events-skeleton.spec.ts` (il personaggio
    dell'ED, `?as=events`); le cinque righe `evt_` in `ErasureTests`, con la nota breve
    `2026-09-30-le-colonne-degli-eventi-in-erasuretests`.
- **Che cosa deve sapere la fase dopo**:
  - ⚠️ **I grant di AOD, FOD e MD non ci sono, e aspettano E2b.** Un grant sull'ED fa entrare chi lo tiene nell'ED per tutto quello che
    vede (`HubClaims.BuildIdentity`), e i nove del design sarebbero i primi grant a una posizione fra due dipartimenti: con loro la suite
    d'integrazione intera ha tre rossi del maintainer (`SeveralDepartmentsTests` ×2, `SearchEndpointTests`). **Carmine ha deciso la (b)**
    della nota `2026-09-30-i-grant-di-chi-collabora-sugli-eventi`
    ([sulla #209](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/209#issuecomment-5917066144)): un grant a una posizione su un
    dipartimento che non è il suo dà il permesso, non il dipartimento, in una fase del nucleo a sé, **E2b**, prima di E3a; i nove si
    seminano dopo di lei. `EventsArchitectureTests` li tiene fuori dai due file finché la tabella del design non li riprende: **un grant
    del seme applicato non si toglie più togliendolo dal file**. E3a, che prova FOD e AOD sull'evento, aspetta E2b.
  - ⚠️ **I test d'integrazione degli eventi avviano l'host con `useIvaoFixtures: true`** (l'avviso di E10b qui sotto): senza, un host
    chiede un token a IVAO quando `ref_ivao_centers` è vuota. `EventsSkeletonTests` si riprende alla fine di ogni test i grant e le
    posizioni che dà ai suoi VID; i membri restano, come in ogni classe.
  - **Le colonne di `evt_events`**: `starts_at_utc` ed `ends_at_utc` obbligatorie, `visible_from_utc` e `booking_opens_at_utc`
    facoltative, `shift_minutes` e i tre limiti vuoti = l'impostazione, `visibility` è una colonna (E3a la chiude a `Public` e `Members`
    nel validatore), `kind` lunga come una chiave del calendario (32). Le regole di «Pubblica» sono di E3b.
  - **E3a** scrive l'interfaccia delle righe figlie dell'evento (come `ITourChild`) con `CrudOptions.BeforeAuthorize`, e aggiunge
    `AddValidatorsFromAssemblyContaining` con la prima risorsa del CRUD (come A5 per il training): E2 non li ha.
  - **Le due verifiche del design §6.3** non chiedono il nucleo: l'intestazione per lo staff dei FIR da un endpoint dell'area ATC con
    `EventAtc.View` senza risorsa, le disponibilità per postazione con l'unico handler chiesto sulla postazione (`10`, E2, «Le due
    verifiche»). Le scrive in codice E11a.
  - ⚠️ **Fino a E11a ogni avvio scrive due avvisi** sui grant del team di un FIR («not applied: no row of its area says its FIR»): è
    voluto; il primo avvio con una riga `IHasFir` dell'area `EventAtc` li applica.
  - **La schermata delle impostazioni** offre, accanto ai tipi del calendario, un tipo che un preset nomina e il calendario non ha più
    (con la sua chiave come parola), perché la riga si veda e si tolga: il salvataggio la rifiuta sulla riga.
- ⚠️ **Nessuna mappa di base per `pnpm e2e:full`** in nessun worktree (30 settembre): le spec la tollerano, come in CI.

### Che cosa ha lasciato E10d (30 settembre 2026, branch `m4/e10d-award-assigner-mail`, PR #205, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-09-30-la-mail-a-chi-assegna-gli-award.md`, **decisa da Carmine**, in chat al master e
  pubblicata su sua istruzione [sulla #205](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/205#issuecomment-5916282643)):
  - **Il riepilogo a chi assegna gli award**: `AwardQueueMailJob` (`award-queue-mail`, `src/IvaoHub.Core/Awards/`) gira una volta al
    giorno all'ora di **`division.json → awardDigestTime`** (`HH:mm` nell'ora della divisione, **07:00** se manca; IT non la scrive).
    Legge la coda del nucleo e, **solo se** sono entrati segnali nuovi, manda a chi ha `Awards.Assign` (`IPermissionHolders`,
    superadmin compresi) **una mail**: quanti segnali nuovi, quanti in attesa, una riga per motivo e award proposto con il numero,
    **senza VID**, il link a `/staff/awards/queue`. Tipo del nucleo **`award.toAssign`**: nel profilo da solo, spegnibile.
  - **Il segno** `cms_award_signals.notified_at` (migrazione del nucleo `AddAwardSignalNotifiedAt`; le righe già in coda sono segnate
    come dette): un segnale si racconta una volta sola, nello stesso salvataggio delle righe della mail, e uno gestito o scartato prima
    del giro non si racconta.
  - **Per ogni modulo**: il job non guarda `source_module`, e il modulo dei tour non è cambiato.
  - **I test**: `AwardQueueMailTests` (integrazione: un segnale dei tour e uno di prova; chi l'ha spento e chi non ha il permesso; una
    volta sola; l'ora dell'host), `AwardQueueMailLinesTests` e `AwardDigestTimeTests` (unità).
- **Che cosa deve sapere la fase dopo**:
  - **E14b** proietta i segnali e basta, senza chiamare niente; la mail parte all'ora di `awardDigestTime` dopo il segnale. Il suo
    «fatta quando» sul banco è il segnale in coda: il banco non ha nessuno con `Awards.Assign` e una casella (il web master non ne
    ha), e il job gira solo alla sua ora. La mail la prova `AwardQueueMailTests`.
  - ⚠️ **Oggi l'MD non ha `Awards.Assign`**: è un permesso globale, e un grant non dà mai un permesso globale (nota §5). Coda e mail
    sono di DIR, ADIR, WM, AWM e dei superadmin. **Carmine ha deciso** che diventi concedibile, **in una fase del nucleo sua** (la
    prepara la sessione che coordina, non E10d): quando ci sarà, la mail arriverà all'MD da sola.
  - ⚠️ **Gli snapshot dei contesti dei moduli** vedono `notified_at` solo al loro prossimo `migrations add` (lo scarto innocuo di
    T4b). Una fase che fa nascere o migra un contesto dopo E10d se la trova nello snapshot come tabella esclusa: è giusto così.
  - ⚠️ **In un test d'integrazione un job del nucleo gira da solo nell'host**: un test che lo fa girare lo mette prima in pausa
    (`ISchedulerFactory`, come `TourTests`) e passa `useIvaoFixtures: true` (l'avviso di E10b qui sotto).
  - ⚠️ **E10d migra il contesto del nucleo**: se un'altra fase del nucleo lo migra insieme, la seconda unita rifà la sua migrazione
    sopra `main`.

### Che cosa ha lasciato E10e (30 settembre 2026, branch `m4/e10e-great-circle-core`, PR #206, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-09-30-la-distanza-fra-due-aeroporti-nel-nucleo.md`, scelta tecnica, con una richiesta a
  Carmine):
  - **La distanza fra due aeroporti nel nucleo**: `GreatCircle.DistanceNm(GeoPoint, GeoPoint)`, in miglia nautiche, e
    `GreatCircle.DistanceNmRounded`, al decimo (una metà va al decimo pari), con `GeoPoint(Latitude, Longitude)` in gradi — in
    `src/IvaoHub.Core/Airspace/GreatCircle.cs`, namespace **`IvaoHub.Core.Airspace`**. È il codice dei tour con i loro numeri: la
    distanza fra i due aeroporti, non le miglia volate.
  - **Come la usa un modulo**: chiede a `IAirportDirectory.FindAsync` dove sono gli aeroporti (un `FindAsync` solo per tutte le voci),
    prende le coordinate con un pattern (`is { Latitude: { } …, Longitude: { } … }`: un aeroporto senza coordinate non ha distanza) e
    misura con `GreatCircle`. Nessuna registrazione, nessuna migrazione, nessun endpoint.
  - **I test**: `tests/IvaoHub.UnitTests/GreatCircleTests.cs` (unità, 8): le domande di `LegTests` al nucleo con le stesse risposte,
    gli antipodi e l'antimeridiano, e **il test gemello**, che confronta il nucleo e la copia dei tour bit per bit su 35.721 coppie.
- **Che cosa deve sapere la fase dopo**:
  - ⚠️ **Il namespace è `IvaoHub.Core.Airspace`, non `IvaoHub.Core.Ivao`** (accanto ai contorni dei FIR, geometria che non è di IVAO):
    in `Ivao` gli stessi nomi fanno cadere la build dei tour (`CS0104` su `TrackChecks.cs`, provato; nota §2 punto 3).
  - ⚠️ **La copia dei tour c'è ancora** (`src/IvaoHub.Modules.FlightOps/Legs/GreatCircle.cs`): la toglie una sessione di Carmine
    **dopo l'unione di E10e** ([sua risposta sulla #206](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/206#issuecomment-5916695685),
    alla [richiesta](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/206#issuecomment-5916051005); nota §5). Fino ad allora il test
    gemello tiene le due copie uguali; con il passaggio se ne va anche lui. **Sì anche alla riga in `CLAUDE.md` §2** (la distanza fra
    due aeroporti è `GreatCircle` del nucleo, mai una copia), che aggiunge il master.
  - **E14a ed E14b** la trovano qui: la distanza di una voce di un PIREP con `DistanceNmRounded` se la colonna è al decimo, come
    `fo_legs.distance_nm`, e `MinLegDistance` confrontata con quel numero.
  - Agli antipodi `h` può passare 1 di un'unità nell'ultima cifra, ma la radice lo riporta a 1: nessun NaN, misurato (nota §6).

### Che cosa ha lasciato E10b (30 settembre 2026, branch `m4/e10b-shared-sessions-by-vid`, PR #208, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-09-30-le-sessioni-condivise-per-vid.md`, scelta tecnica, nessuna domanda nuova), tutto in
  `src/IvaoHub.Core/Atc/`:
  - **il VID in ogni presenza**: `AtcPresence.Vid` (`int?`, una proprietà `init` sotto il costruttore), dalla colonna `vid` della
    vista; `null` dove l'archivio non lo dice (un doppio di un test), che non è il VID di nessuno. Non esce dai tour (le loro risposte
    si costruiscono campo per campo): il contratto OpenAPI non cambia;
  - **la domanda nuova** `IAtcActivitySource.SessionsOfAsync(vid, fromUtc, toUtc)`: le connessioni di un controllore aperte
    nell'intervallo, nella forma della domanda dei tour (`AtcActivity`: le presenze e da quando l'archivio è completo, per la divisione
    e per il mondo). Un controllore per domanda: vIPI indicizza `(UserId, StartUtc)`;
  - **«non disponibile»** (`null`) con `atcData.source: none`, con un archivio scritto prima di E10b (la domanda ha un corpo
    predefinito nell'interfaccia, come `IIvaoApiClient.GetAtcPositionsAsync`), con un archivio che non si legge;
  - **corretto**: con `atcData: vipi` e senza `ConnectionStrings:AtcData` **l'hub non partiva** — il contesto della vista si
    costruiva con la sorgente, fuori dal suo `try`, e all'avvio il seeder dei contenuti la costruisce attraverso i blocchi dei tour —;
    ora parte e risponde «non disponibile» (nota §4, un commit a sé);
  - **i test**: `AtcActivitySourceTests` (integrazione), con la vista di vIPI in **un database suo** e all'utente dell'hub solo il
    `SELECT` sulla vista.
- **Che cosa deve sapere la fase dopo**:
  - **E11b** (l'esperienza, design §4.3): una domanda per candidato, `SessionsOfAsync(vid, adesso − experienceMonths, adesso)`, e
    `Of(callsign)` per una postazione. «Più spesso» (sessioni o minuti) e «quel tipo» sono del modulo, e **il tipo di un nominativo lo
    dà la directory di E10c**, non questa interfaccia (la colonna `position` della vista non passa). `null` = il criterio non c'è; una
    lista vuota = nessuna esperienza.
  - **E13a** (la presenza, §4.5): `SessionsOfAsync(vid, turno.da, turno.a).Of(callsign)` per il titolare; chi ha coperto:
    `OnlineAsync(turno.da, turno.a).Of(callsign)` con un `Vid` diverso dal titolare. ⚠️ I minuti dentro il turno li conta il modulo:
    una connessione ancora aperta ha `EndedAt` nullo, una riconnessione sono due connessioni, e la prima può cominciare prima del
    turno. ⚠️ `Covers(callsign, turno.da)` falso vuol dire «non si sa» (`Unknown`), non un no-show. Con `null` il ripiego è il tracker
    per VID con `connectionType=ATC` (E10a): la scelta fra le due fonti è del modulo.
  - ⚠️ **`Vid` è `int?`**: `presence.Vid == turno.ControllerVid` con un `null` non conferma nessuno, ed è voluto.
  - ⚠️ **E10b non tocca `Core/Ivao/`**, a differenza di quello che dice «Per chi prende M4» (con E10a ed E15a): l'archivio è
    `Core/Atc/`, e non è il client di IVAO.
  - ⚠️ **Un host dei test d'integrazione senza `useIvaoFixtures` chiede un token a IVAO** quando `ref_ivao_centers` è vuota (la
    sincronizzazione all'avvio, `HubPipeline`): rifiutato con un 400, ma sono chiamate vere, cinque a ogni avvio finché la tabella
    resta vuota. I test degli eventi che avviano un host passino `useIvaoFixtures: true`, come `AtcPositionTests` e
    `AtcActivitySourceTests`.
  - `docs/FORKING.md` e `config/division.example.json` descrivono l'archivio per i tour, e restano veri: la metà degli eventi la scrive
    E15b.

### Che cosa ha lasciato E1 (30 settembre 2026, branch `m4/e1-calendar-kinds`, PR #200, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-09-30-i-tipi-degli-eventi-e-l-ed-sul-banco.md`, scelta tecnica, nessuna domanda nuova):
  - **I quattro tipi degli eventi nel calendario**: `rfe`, `rfo`, `mse` e **`online-day`** in `seed/calendar-kinds/kinds.json`, blu come
    `event` e subito dopo (`sort` 11–14), con le etichette «RFE», «RFO», «MSE», «Online Day» (`seed.calendarKinds.*` in
    `locales/*/seed.json`). Arrivano anche a un database già avviato, e una chiave scritta a mano prima resta com'è: il seeder lo
    faceva già (A2 di M3), nessun codice cambiato. Il bootstrap (`/api/me` → `calendarKinds`) li porta a chiunque, visitatori compresi.
  - **Il coordinatore degli eventi sul banco**: `POST /e2e/signin?as=events`, VID **999005**, «Bench Events», posizione **`IT-EC`**,
    nessuna casella di Mailpit, nessun rating né ora. Oggi ha solo quello che la matrice dà a un coordinatore sul suo dipartimento
    (contenuti, link, media, calendario dell'ED); **i permessi degli eventi glieli dà il seme dei `positionGrants` di E2**.
  - **I test**: `CalendarKindSeedTests` (integrazione), `CalendarKindsXxDivisionTests` (integrazione, il fork «XX» su
    `ivaohub_xx_kinds`), `CalendarKindSeedFileTests` (unità: ogni tipo del seme passa dal validatore del back office),
    `web/e2e/full/events-bench.spec.ts`.
- **Che cosa deve sapere la fase dopo**:
  - ⚠️ **La chiave dell'Online Day è `online-day`**, non `onlineDay` come scrivono il design, la nota `i-tipi-di-evento` e il piano: la
    chiave di un tipo ha la forma di uno slug, e il back office la rilegge a ogni salvataggio. **Confermata da Carmine** il 30
    settembre ([risposta sulla #200](https://github.com/SkyMistery/Ivao-Italy-Hub/pull/200#issuecomment-5912372176)). **E2** scrive
    `online-day` nei test di `kindPresets` (le chiavi che esistono nei tipi), **E3a** la trova nel bootstrap; nessun codice del modulo
    nomina un tipo.
  - ⚠️ **Le spec degli eventi entrano con `?as=events`**, non come il web master del banco: `IT-WM` raggiunge ogni dipartimento e ha
    ogni permesso di ogni modulo, quindi con lui una spec passa con qualunque `positionGrants`. Dopo E2 il personaggio ha i permessi
    degli eventi dal seme dei grant; ⚠️ un grant scritto fa rientrare il titolare (`CONTRIBUTING.md`, «Traps»): una spec che ne scrive
    uno rifà `/e2e/signin?as=events`. Quando una mail dello staff degli eventi dovrà arrivare a lui, la fase che la manda gli dà una
    casella (`E2EStaffOptions` non ne ha: la classe col campo `Email` è quella del trainer).
  - **E2 tenga d'occhio `web/e2e/full/events-bench.spec.ts`**: afferma `departments: ['ED']` e `hasAllDepartments: false` per il
    personaggio; un grant di E2 con uno `scope` diverso da ED per l'`IT-EC` (la nota `chi-lavora-sugli-eventi` non ne prevede) la
    farebbe cambiare.
  - Il commento di `TrainingSettings.ConflictKinds` («the online day joins when M4 makes its kind») è ora vero a metà: il tipo c'è,
    `online-day`; il predefinito resta `["event"]` e la divisione lo aggiunge dalle impostazioni del training.
- ⚠️ **Nessuna mappa di base per `pnpm e2e:full`**: né la cartella principale né gli altri worktree hanno `tiles/basemap.pmtiles` (30
  settembre); le spec che disegnano una mappa tollerano il 404 di `/tiles/`, come in CI, quindi il giro passa lo stesso.

### Che cosa ha lasciato E0 (29 settembre 2026, branch `m4/e0-decisions`, PR #184)

- **Che cosa c'è**: dieci note in `decisions/`, una per decisione o gruppo coerente di §17 del design, ognuna con il link al
  commento di Carmine sulla #180 che la decide e con «Da portare nel piano» — `i-tre-blocchi-e-che-cosa-resta-fuori-da-m4`,
  `i-tipi-di-evento`, `chi-lavora-sugli-eventi`, `gli-slot-e-le-prenotazioni`, `la-vita-di-un-evento`, `il-roster-atc`,
  `dopo-l-evento-e-gli-award`, `gli-eventi-in-presenza`, `le-impostazioni-degli-eventi`, `i-dati-dei-membri-negli-eventi`. Il piano
  1.24 porta già quasi tutta la §18 del design: ogni nota dice che cosa c'è e che cosa resta. E `10-piano-implementazione-m4.md`: le
  regole di tutte le fasi e le fasi E0–E17, con E3, E6, E11, E13, E14 in due PR, E10 in cinque PR del nucleo (E10a–E10e), E15 in due
  (E15a del nucleo), E8 in due (E8a del nucleo, poi **tolta** nel piano 1.25 dopo A12a di M3); «Com'è andata» di E0 è già scritto.
- **Che cosa deve sapere la fase dopo**: le note di E0 registrano le decisioni, **non** la forma nel codice delle estensioni del
  nucleo: ogni fase del nucleo (E1, E10a–E10e, E15a) porta **la sua nota nuova**. ⚠️ **`core-guard` non giudica le PR di
  `SkyMistery`**, quindi il check è verde anche senza la nota: la regola la tiene chi scrive.
- **La cancellazione dei dati di una persona nasce con M4a** (E8b; E8a poi tolta) invece che in E15: proposta di E0, **decisa da Carmine** il 29 settembre 2026, in chat, come raccomandato («sì, come raccomandi tu», alla domanda della PR #184) (`10`, E0,
  scostamento 4).
- ⚠️ **Quello che il design dava per esistente e non c'è** (`10`, E0, «Trovato», punti 6–10): il tracker vuole il VID e non dice il
  tipo di connessione; le sessioni condivise non hanno il VID; il vocabolario dei rating non conosce `GND`, `DEL`, `DEP` né il minimo
  di una postazione; la directory delle postazioni cerca solo per rating; la distanza sta nel modulo dei tour. Ognuno ha la sua fase
  del nucleo, prima delle fasi di M4b che lo usano.
- ⚠️ **IT gira con `atcData: none`** (`config/division.json` non ha la chiave): senza sessioni condivise il roster perde il criterio
  dell'esperienza. È una scelta di configurazione di Carmine, da chiedere in apertura di E11b.
- ⚠️ **Prima del primo evento vero (E9)** servono la produzione (piano §15) e il recupero dei giri persi dei job nel nucleo (nota
  `2026-09-28-i-job-quando-passenger-spegne-l-hub`, non ancora nel codice).
- ⚠️ **Un worktree non ha `tiles/`**: per `pnpm e2e:full` serve un hard link a `tiles/basemap.pmtiles` della cartella principale.
