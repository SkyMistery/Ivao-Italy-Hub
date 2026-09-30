# HANDOFF — stato di M4 (Events)

> Documento **interno** (italiano). È il punto d'ingresso di ogni sessione che lavora al modulo Events. Lo scrive **la sessione
> di lavoro** di ogni fase, alla fine, con un paragrafo «Che cosa ha lasciato <fase>» in cima alla sezione «Lo stato». Lo stato
> generale del progetto sta in `HANDOFF.md`, che scrive solo il master. Le regole — chi unisce, che cosa non si tocca, come si
> ottiene una decisione — sono in `CLAUDE.md` §0 e in `10-piano-implementazione-m4.md`, «Regole di tutte le fasi», e non si
> ripetono qui.

**Ultimo aggiornamento:** 30 settembre 2026 — **fase E15a** (nucleo: le prenotazioni ATC della rete), sul branch
`m4/e15a-network-atc-bookings`, **PR #207** verso `main`, del nucleo, senza coda.
**Il prossimo passo**: **E2** (lo scheletro), che può andare avanti accanto a E1; **E3a** aspetta tutte e due unite. Le fasi del nucleo
di M4b (**E10a–E10e**) sono partite il 30 settembre insieme a E2 e a E15a, ognuna in una sessione sua (E10b è unita, #208); **E11b** ed
**E13a** trovano in E10b la storia di un controllore e la presenza in un turno; **E15b** aspetta E14b ed E15a.

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
controllore: sotto, «Che cosa ha lasciato E10b»); il rating preferito e minimo, le postazioni della
divisione per nominativo (E10c); la mail a chi assegna (E10d); la distanza nel nucleo (E10e); ~~le prenotazioni ATC della rete
(E15a)~~ **portate da E15a** (`IAtcBookingSource`: sotto, «Che cosa ha lasciato E15a»);
l'helper «persona cancellata» e `ErasureTests` che legge ogni modulo sono già arrivati con A12a di M3 (#187): **E8a è tolta** (piano
1.25), e da E2 ogni fase che crea una colonna di persona scrive la sua riga in `ErasureTests`.

## Per i test

VID `761001–761099`, slug `evt-test-` (il master li scrive in `CONTRIBUTING.md` prima che E2 sia unita). **Attenzione all'ED e
all'MD**: i test dei contatti affermano i destinatari esatti dell'MD e seminano `IT-EC` con un indirizzo; nessuno staff dell'ED o
dell'MD con un indirizzo nei test del modulo, i permessi con grant a un VID. Nessuna chiamata a IVAO: fixture.

## Lo stato

*(Qui, in cima, il paragrafo «Che cosa ha lasciato <fase>» di ogni fase chiusa, la più recente per prima.)*

### Che cosa ha lasciato E15a (30 settembre 2026, branch `m4/e15a-network-atc-bookings`, PR #207, del nucleo, senza coda)

- **Che cosa c'è** (nota `decisions/2026-09-30-le-prenotazioni-atc-della-rete.md`, scelta tecnica, nessuna domanda nuova; le misure
  con il token vero sono lì, §2):
  - **La domanda del modulo**: `IAtcBookingSource.BookedAsync(fromUtc, toUtc, callsign?)` in `src/IvaoHub.Core/Ivao/AtcBookingSource.cs`
    (namespace `IvaoHub.Core.Ivao`, che il modulo importa come fa il training: nessun nome del file o del tipo nomina IVAO) → le
    prenotazioni della rete che **si sovrappongono** alla finestra, di tutte le postazioni o di un nominativo intero, nell'ordine in cui
    cominciano; **`null` = non disponibile**, mai «nessuno ha prenotato». `AtcBookingDto(Callsign, StartsAt, EndsAt, Vid, Kind)`, con
    `AtcBookingKind` `Controlling`, `Training`, `Exam`. Al più **`IAtcBookingSource.MaxDays` = 7** giorni di UTC (oltre: `null` e un
    avviso nel log). Alla richiesta, **nessuna cache**, nessun job.
  - **Il client**: `IIvaoApiClient.GetDailyAtcBookingsAsync(DateOnly, position?)` (`/v2/atc/bookings/daily`, il giorno di IVAO com'è;
    predefinito «non disponibile» per i doppi dei test), `IvaoApiClient` con il token dell'applicazione, `FixtureIvaoApiClient`, e **un
    lettore solo** per tutti e due, `IvaoAtcBookingReader` (`IvaoAtcBookings.cs`).
  - **Lo strumento**: `tools/record-ivao-fixtures.mjs --bookings <nome> <asVid> <yyyy-mm-dd> <prefisso…>`, con la persona tolta.
  - **La fixture** `tests/fixtures/ivao/atc-bookings-day.json` (VID 761070–761079) e il suo paragrafo nel README delle fixture.
  - **I test**: `AtcBookingTests` (unità, 25 casi), provati al contrario (`10`, E15a, «Com'è andata»).
- **Che cosa deve sapere la fase dopo** (E15b):
  - **Una domanda per l'evento, non una per postazione**: `BookedAsync(inizio, fine)` senza nominativo, poi le postazioni dell'evento
    con un filtro: una chiamata a IVAO per giorno di UTC invece di una per postazione. Il nominativo della domanda, quando c'è, è
    **intero** (con `LIRR` non torna niente: per IVAO sarebbero tre settori).
  - ⚠️ **IVAO lento o giù**: prima di «non disponibile» il client aspetta quello che aspetta ogni chiamata a IVAO (i tentativi e il
    tempo massimo della resilienza, 30 secondi per chiamata). **Le prenotazioni si caricano a parte dal roster** (una richiesta loro),
    così il roster non aspetta IVAO.
  - **Il VID, non il nome**: un membro dell'hub con `personName`, chi non è mai entrato con il suo numero. `Exam` e `Training` dicono
    che la postazione è occupata da un esame o da un training su IVAO: vale la pena mostrarlo.
  - **Sul banco (fixture) ogni giorno è il 27 luglio 2026**: `LIRF_TWR` 18–20 (761079), `LIMC_TWR`, `LFPG_APP`, `LIMM_WS2_CTR`, `LFFF_CTR`,
    `LIRR_SU_CTR`, `LIRR_NC_CTR` 19–21, `LIRR_NE_CTR` 19–22, l'esame di `EDDF_APP` 18:30–20:30, `SBGR_TWR` 23–01 (UTC). I VID
    761070–761079 non sono nessuno sul banco: la pagina mostra i numeri. Una spec che vuole vedere un controllore del roster anche fra
    le prenotazioni ha bisogno di una fixture con il VID di un personaggio del banco (il roster prende solo chi è entrato nell'hub).
  - ⚠️ **E10a tocca gli stessi file nello stesso giorno** (`IIvaoApiClient.cs`, `FixtureIvaoApiClient.cs`, lo script delle fixture):
    chi viene unita per seconda fonde `main` quando il master lo chiede (mai un rebase), e tiene tutte e due le modalità dello script.
- ⚠️ **Trappole trovate** (nota, §2): la `position` di IVAO è **il principio del nominativo** (`LI` = tutte le italiane); una
  prenotazione a cavallo della mezzanotte è nell'elenco di **tutti e due** i giorni, una che finisce alle 00:00 anche del giorno dopo;
  `date` con un'ora che non è 00:00 dà un elenco vuoto; `user` porta anche `rating`, che la documentazione non dice.

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
